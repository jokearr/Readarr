using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Comics.Events;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Parser;

namespace NzbDrone.Core.Comics
{
    public interface IComicService
    {
        Comic GetComic(int comicId);
        List<Comic> GetComics(IEnumerable<int> comicIds);
        List<Comic> GetComicsByPublisher(int publisherId);
        List<Comic> GetNextComicsByPublisherMetadataId(IEnumerable<int> publisherMetadataIds);
        List<Comic> GetLastComicsByPublisherMetadataId(IEnumerable<int> publisherMetadataIds);
        List<Comic> GetComicsByPublisherMetadataId(int publisherMetadataId);
        List<Comic> GetComicsForRefresh(int publisherMetadataId, IEnumerable<string> foreignIds);
        List<Comic> GetComicsByFileIds(IEnumerable<int> fileIds);
        Comic AddComic(Comic newComic, bool doRefresh = true);
        Comic FindById(string foreignId);
        Comic FindBySlug(string titleSlug);
        Comic FindByTitle(int publisherMetadataId, string title);
        Comic FindByTitleInexact(int publisherMetadataId, string title);
        List<Comic> GetCandidates(int publisherMetadataId, string title);
        void DeleteComic(int comicId, bool deleteFiles, bool addImportListExclusion = false);
        List<Comic> GetAllComics();
        Comic UpdateComic(Comic comic);
        void SetComicMonitored(int comicId, bool monitored);
        void SetMonitored(IEnumerable<int> ids, bool monitored);
        PagingSpec<Comic> ComicsWithoutFiles(PagingSpec<Comic> pagingSpec);
        List<Comic> ComicsBetweenDates(DateTime start, DateTime end, bool includeUnmonitored);
        List<Comic> PublisherComicsBetweenDates(Publisher publisher, DateTime start, DateTime end, bool includeUnmonitored);
        void InsertMany(List<Comic> comics);
        void UpdateMany(List<Comic> comics);
        void DeleteMany(List<Comic> comics);
        void SetAddOptions(IEnumerable<Comic> comics);
        List<Comic> GetPublisherComicsWithFiles(Publisher publisher);
    }

    public class ComicService : IComicService,
                                IHandle<PublisherDeletedEvent>
    {
        private readonly IComicRepository _comicRepository;
        private readonly IEditionService _editionService;
        private readonly IEventAggregator _eventAggregator;
        private readonly Logger _logger;

        public ComicService(IComicRepository comicRepository,
                           IEditionService editionService,
                           IEventAggregator eventAggregator,
                           Logger logger)
        {
            _comicRepository = comicRepository;
            _editionService = editionService;
            _eventAggregator = eventAggregator;
            _logger = logger;
        }

        public Comic AddComic(Comic newComic, bool doRefresh = true)
        {
            if (newComic.PublisherMetadataId == 0)
            {
                throw new InvalidOperationException("Cannot insert comic with PublisherMetadataId = 0");
            }

            _comicRepository.Upsert(newComic);

            var editions = newComic.Editions.Value;
            editions.ForEach(x => x.ComicId = newComic.Id);

            _editionService.InsertMany(editions.Where(x => x.Id == 0).ToList());
            _editionService.SetMonitored(editions.FirstOrDefault(x => x.Monitored) ?? editions.First());

            _eventAggregator.PublishEvent(new ComicAddedEvent(GetComic(newComic.Id), doRefresh));

            return newComic;
        }

        public void DeleteComic(int comicId, bool deleteFiles, bool addImportListExclusion = false)
        {
            var comic = _comicRepository.Get(comicId);
            comic.Publisher.LazyLoad();
            _comicRepository.Delete(comicId);
            _eventAggregator.PublishEvent(new ComicDeletedEvent(comic, deleteFiles, addImportListExclusion));
        }

        public Comic FindById(string foreignId)
        {
            return _comicRepository.FindById(foreignId);
        }

        public Comic FindBySlug(string titleSlug)
        {
            return _comicRepository.FindBySlug(titleSlug);
        }

        public Comic FindByTitle(int publisherMetadataId, string title)
        {
            return _comicRepository.FindByTitle(publisherMetadataId, title);
        }

        private List<Tuple<Func<Comic, string, double>, string>> ComicScoringFunctions(string title, string cleanTitle)
        {
            Func<Func<Comic, string, double>, string, Tuple<Func<Comic, string, double>, string>> tc = Tuple.Create;
            var scoringFunctions = new List<Tuple<Func<Comic, string, double>, string>>
            {
                tc((a, t) => a.CleanTitle.FuzzyMatch(t), cleanTitle),
                tc((a, t) => a.Title.FuzzyMatch(t), title),
                tc((a, t) => a.CleanTitle.FuzzyMatch(t), title.RemoveBracketsAndContents().CleanPublisherName()),
                tc((a, t) => a.CleanTitle.FuzzyMatch(t), title.RemoveAfterDash().CleanPublisherName()),
                tc((a, t) => a.CleanTitle.FuzzyMatch(t), title.RemoveBracketsAndContents().RemoveAfterDash().CleanPublisherName()),
                tc((a, t) => t.FuzzyContains(a.CleanTitle), cleanTitle),
                tc((a, t) => t.FuzzyContains(a.Title), title),
                tc((a, t) => a.Title.SplitComicTitle(a.PublisherMetadata.Value.Name).Item1.FuzzyMatch(t), title)
            };

            return scoringFunctions;
        }

        public Comic FindByTitleInexact(int publisherMetadataId, string title)
        {
            var comics = GetComicsByPublisherMetadataId(publisherMetadataId);

            foreach (var func in ComicScoringFunctions(title, title.CleanPublisherName()))
            {
                var results = FindByStringInexact(comics, func.Item1, func.Item2);
                if (results.Count == 1)
                {
                    return results[0];
                }
            }

            return null;
        }

        public List<Comic> GetCandidates(int publisherMetadataId, string title)
        {
            var comics = GetComicsByPublisherMetadataId(publisherMetadataId);
            var output = new List<Comic>();

            foreach (var func in ComicScoringFunctions(title, title.CleanPublisherName()))
            {
                output.AddRange(FindByStringInexact(comics, func.Item1, func.Item2));
            }

            return output.DistinctBy(x => x.Id).ToList();
        }

        private List<Comic> FindByStringInexact(List<Comic> comics, Func<Comic, string, double> scoreFunction, string title)
        {
            const double fuzzThreshold = 0.7;
            const double fuzzGap = 0.4;

            var sortedComics = comics.Select(s => new
            {
                MatchProb = scoreFunction(s, title),
                Comic = s
            })
                .ToList()
                .OrderByDescending(s => s.MatchProb)
                .ToList();

            return sortedComics.TakeWhile((x, i) => i == 0 || sortedComics[i - 1].MatchProb - x.MatchProb < fuzzGap)
                .TakeWhile((x, i) => x.MatchProb > fuzzThreshold || (i > 0 && sortedComics[i - 1].MatchProb > fuzzThreshold))
                .Select(x => x.Comic)
                .ToList();
        }

        public List<Comic> GetAllComics()
        {
            return _comicRepository.All().ToList();
        }

        public Comic GetComic(int comicId)
        {
            return _comicRepository.Get(comicId);
        }

        public List<Comic> GetComics(IEnumerable<int> comicIds)
        {
            return _comicRepository.Get(comicIds).ToList();
        }

        public List<Comic> GetComicsByPublisher(int publisherId)
        {
            return _comicRepository.GetComics(publisherId).ToList();
        }

        public List<Comic> GetNextComicsByPublisherMetadataId(IEnumerable<int> publisherMetadataIds)
        {
            return _comicRepository.GetNextComics(publisherMetadataIds).ToList();
        }

        public List<Comic> GetLastComicsByPublisherMetadataId(IEnumerable<int> publisherMetadataIds)
        {
            return _comicRepository.GetLastComics(publisherMetadataIds).ToList();
        }

        public List<Comic> GetComicsByPublisherMetadataId(int publisherMetadataId)
        {
            return _comicRepository.GetComicsByPublisherMetadataId(publisherMetadataId).ToList();
        }

        public List<Comic> GetComicsForRefresh(int publisherMetadataId, IEnumerable<string> foreignIds)
        {
            return _comicRepository.GetComicsForRefresh(publisherMetadataId, foreignIds);
        }

        public List<Comic> GetComicsByFileIds(IEnumerable<int> fileIds)
        {
            return _comicRepository.GetComicsByFileIds(fileIds);
        }

        public void SetAddOptions(IEnumerable<Comic> comics)
        {
            _comicRepository.SetFields(comics.ToList(), s => s.AddOptions);
        }

        public PagingSpec<Comic> ComicsWithoutFiles(PagingSpec<Comic> pagingSpec)
        {
            var comicResult = _comicRepository.ComicsWithoutFiles(pagingSpec);

            return comicResult;
        }

        public List<Comic> ComicsBetweenDates(DateTime start, DateTime end, bool includeUnmonitored)
        {
            var comics = _comicRepository.ComicsBetweenDates(start.ToUniversalTime(), end.ToUniversalTime(), includeUnmonitored);

            return comics;
        }

        public List<Comic> PublisherComicsBetweenDates(Publisher publisher, DateTime start, DateTime end, bool includeUnmonitored)
        {
            var comics = _comicRepository.PublisherComicsBetweenDates(publisher, start.ToUniversalTime(), end.ToUniversalTime(), includeUnmonitored);

            return comics;
        }

        public List<Comic> GetPublisherComicsWithFiles(Publisher publisher)
        {
            return _comicRepository.GetPublisherComicsWithFiles(publisher);
        }

        public void InsertMany(List<Comic> comics)
        {
            if (comics.Any(x => x.PublisherMetadataId == 0))
            {
                throw new InvalidOperationException("Cannot insert comic with PublisherMetadataId = 0");
            }

            _comicRepository.InsertMany(comics);
        }

        public void UpdateMany(List<Comic> comics)
        {
            _comicRepository.UpdateMany(comics);
        }

        public void DeleteMany(List<Comic> comics)
        {
            _comicRepository.DeleteMany(comics);

            foreach (var comic in comics)
            {
                _eventAggregator.PublishEvent(new ComicDeletedEvent(comic, false, false));
            }
        }

        public Comic UpdateComic(Comic comic)
        {
            var storedComic = GetComic(comic.Id);
            var updatedComic = _comicRepository.Update(comic);

            _eventAggregator.PublishEvent(new ComicEditedEvent(updatedComic, storedComic));

            return updatedComic;
        }

        public void SetComicMonitored(int comicId, bool monitored)
        {
            var comic = _comicRepository.Get(comicId);
            _comicRepository.SetMonitoredFlat(comic, monitored);

            // publish comic edited event so publisher stats update
            _eventAggregator.PublishEvent(new ComicEditedEvent(comic, comic));

            _logger.Debug("Monitored flag for Comic:{0} was set to {1}", comicId, monitored);
        }

        public void SetMonitored(IEnumerable<int> ids, bool monitored)
        {
            _comicRepository.SetMonitored(ids, monitored);

            // publish comic edited event so publisher stats update
            foreach (var comic in _comicRepository.Get(ids))
            {
                _eventAggregator.PublishEvent(new ComicEditedEvent(comic, comic));
            }
        }

        public void Handle(PublisherDeletedEvent message)
        {
            var comics = GetComicsByPublisherMetadataId(message.Publisher.PublisherMetadataId);
            DeleteMany(comics);
        }
    }
}
