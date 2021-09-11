using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.Cache;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Comics.Events;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Parser;

namespace NzbDrone.Core.Comics
{
    public interface IPublisherService
    {
        Publisher GetPublisher(int publisherId);
        Publisher GetPublisherByMetadataId(int publisherMetadataId);
        List<Publisher> GetPublishers(IEnumerable<int> publisherIds);
        Publisher AddPublisher(Publisher newPublisher, bool doRefresh);
        List<Publisher> AddPublishers(List<Publisher> newPublishers, bool doRefresh);
        Publisher FindById(string foreignPublisherId);
        Publisher FindByName(string title);
        Publisher FindByNameInexact(string title);
        List<Publisher> GetCandidates(string title);
        List<Publisher> GetReportCandidates(string reportTitle);
        void DeletePublisher(int publisherId, bool deleteFiles, bool addImportListExclusion = false);
        List<Publisher> GetAllPublishers();
        List<Publisher> AllForTag(int tagId);
        Publisher UpdatePublisher(Publisher publisher);
        List<Publisher> UpdatePublishers(List<Publisher> publishers, bool useExistingRelativeFolder);
        Dictionary<int, string> AllPublisherPaths();
        bool PublisherPathExists(string folder);
        void RemoveAddOptions(Publisher publisher);
    }

    public class PublisherService : IPublisherService
    {
        /*private readonly IPublisherRepository _publisherRepository;*/
        private readonly IPublisherRepository _publisherRepository;
        private readonly IEventAggregator _eventAggregator;
        private readonly IBuildPublisherPaths _publisherPathBuilder;
        private readonly Logger _logger;
        private readonly ICached<List<Publisher>> _cache;

        public PublisherService(IPublisherRepository publisherRepository,
                             IEventAggregator eventAggregator,
                             IBuildPublisherPaths publisherPathBuilder,
                             ICacheManager cacheManager,
                             Logger logger)
        {
            _publisherRepository = publisherRepository;
            _eventAggregator = eventAggregator;
            _publisherPathBuilder = publisherPathBuilder;
            _cache = cacheManager.GetCache<List<Publisher>>(GetType());
            _logger = logger;
        }

        public Publisher AddPublisher(Publisher newPublisher, bool doRefresh)
        {
            _cache.Clear();
            _publisherRepository.Insert(newPublisher);
            _eventAggregator.PublishEvent(new PublisherAddedEvent(GetPublisher(newPublisher.Id), doRefresh));

            return newPublisher;
        }

        public List<Publisher> AddPublishers(List<Publisher> newPublishers, bool doRefresh)
        {
            _cache.Clear();
            _publisherRepository.InsertMany(newPublishers);
            _eventAggregator.PublishEvent(new PublishersImportedEvent(newPublishers.Select(s => s.Id).ToList(), doRefresh));

            return newPublishers;
        }

        public bool PublisherPathExists(string folder)
        {
            return _publisherRepository.PublisherPathExists(folder);
        }

        public void DeletePublisher(int publisherId, bool deleteFiles, bool addImportListExclusion = false)
        {
            _cache.Clear();
            var publisher = _publisherRepository.Get(publisherId);
            _publisherRepository.Delete(publisherId);
            _eventAggregator.PublishEvent(new PublisherDeletedEvent(publisher, deleteFiles, addImportListExclusion));
        }

        public Publisher FindById(string foreignPublisherId)
        {
            return _publisherRepository.FindById(foreignPublisherId);
        }

        public Publisher FindByName(string title)
        {
            return _publisherRepository.FindByName(title.CleanAuthorName());
        }

        public List<Tuple<Func<Publisher, string, double>, string>> PublisherScoringFunctions(string title, string cleanTitle)
        {
            Func<Func<Publisher, string, double>, string, Tuple<Func<Publisher, string, double>, string>> tc = Tuple.Create;
            var scoringFunctions = new List<Tuple<Func<Publisher, string, double>, string>>
            {
                tc((a, t) => a.CleanName.FuzzyMatch(t), cleanTitle),
                tc((a, t) => a.Name.FuzzyMatch(t), title),
                tc((a, t) => a.Name.ToLastFirst().FuzzyMatch(t), title),
                tc((a, t) => a.Metadata.Value.Aliases.Concat(new List<string> { a.Name }).Max(x => x.CleanPublisherName().FuzzyMatch(t)), cleanTitle),
            };

            if (title.StartsWith("The ", StringComparison.CurrentCultureIgnoreCase))
            {
                scoringFunctions.Add(tc((a, t) => a.CleanName.FuzzyMatch(t), title.Substring(4).CleanPublisherName()));
            }
            else
            {
                scoringFunctions.Add(tc((a, t) => a.CleanName.FuzzyMatch(t), "the" + cleanTitle));
            }

            return scoringFunctions;
        }

        public Publisher FindByNameInexact(string title)
        {
            var publishers = GetAllPublishers();

            foreach (var func in PublisherScoringFunctions(title, title.CleanPublisherName()))
            {
                var results = FindByStringInexact(publishers, func.Item1, func.Item2);
                if (results.Count == 1)
                {
                    return results[0];
                }
            }

            return null;
        }

        public List<Publisher> GetCandidates(string title)
        {
            var publishers = GetAllPublishers();
            var output = new List<Publisher>();

            foreach (var func in PublisherScoringFunctions(title, title.CleanPublisherName()))
            {
                output.AddRange(FindByStringInexact(publishers, func.Item1, func.Item2));
            }

            return output.DistinctBy(x => x.Id).ToList();
        }

        public List<Tuple<Func<Publisher, string, double>, string>> ReportPublisherScoringFunctions(string reportTitle, string cleanReportTitle)
        {
            Func<Func<Publisher, string, double>, string, Tuple<Func<Publisher, string, double>, string>> tc = Tuple.Create;
            var scoringFunctions = new List<Tuple<Func<Publisher, string, double>, string>>
            {
                tc((a, t) => t.FuzzyContains(a.CleanName), cleanReportTitle),
                tc((a, t) => t.FuzzyContains(a.Metadata.Value.Name), reportTitle),
                tc((a, t) => t.FuzzyContains(a.Metadata.Value.Name.ToLastFirst()), reportTitle)
            };

            return scoringFunctions;
        }

        public List<Publisher> GetReportCandidates(string reportTitle)
        {
            var publishers = GetAllPublishers();
            var output = new List<Publisher>();

            foreach (var func in ReportPublisherScoringFunctions(reportTitle, reportTitle.CleanPublisherName()))
            {
                output.AddRange(FindByStringInexact(publishers, func.Item1, func.Item2));
            }

            return output.DistinctBy(x => x.Id).ToList();
        }

        private List<Publisher> FindByStringInexact(List<Publisher> publishers, Func<Publisher, string, double> scoreFunction, string title)
        {
            const double fuzzThreshold = 0.8;
            const double fuzzGap = 0.2;

            var sortedPublishers = publishers.Select(s => new
            {
                MatchProb = scoreFunction(s, title),
                Publisher = s
            })
                .ToList()
                .OrderByDescending(s => s.MatchProb)
                .ToList();

            return sortedPublishers.TakeWhile((x, i) => i == 0 || sortedPublishers[i - 1].MatchProb - x.MatchProb < fuzzGap)
                .TakeWhile((x, i) => x.MatchProb > fuzzThreshold || (i > 0 && sortedPublishers[i - 1].MatchProb > fuzzThreshold))
                .Select(x => x.Publisher)
                .ToList();
        }

        public List<Publisher> GetAllPublishers()
        {
            return _cache.Get("GetAllPublishers", () => _publisherRepository.All().ToList(), TimeSpan.FromSeconds(30));
        }

        public Dictionary<int, string> AllPublisherPaths()
        {
            return _publisherRepository.AllPublisherPaths();
        }

        public List<Publisher> AllForTag(int tagId)
        {
            return GetAllPublishers().Where(s => s.Tags.Contains(tagId))
                                 .ToList();
        }

        public Publisher GetPublisher(int publisherId)
        {
            return _publisherRepository.Get(publisherId);
        }

        public Publisher GetPublisherByMetadataId(int publisherMetadataId)
        {
            return _publisherRepository.GetPublisherByMetadataId(publisherMetadataId);
        }

        public List<Publisher> GetPublishers(IEnumerable<int> publisherIds)
        {
            return _publisherRepository.Get(publisherIds).ToList();
        }

        public void RemoveAddOptions(Publisher publisher)
        {
            _publisherRepository.SetFields(publisher, s => s.AddOptions);
        }

        public Publisher UpdatePublisher(Publisher publisher)
        {
            _cache.Clear();
            var storedPublisher = GetPublisher(publisher.Id);
            var updatedPublisher = _publisherRepository.Update(publisher);
            _eventAggregator.PublishEvent(new PublisherEditedEvent(updatedPublisher, storedPublisher));

            return updatedPublisher;
        }

        public List<Publisher> UpdatePublishers(List<Publisher> publisher, bool useExistingRelativeFolder)
        {
            _cache.Clear();
            _logger.Debug("Updating {0} publisher", publisher.Count);

            foreach (var s in publisher)
            {
                _logger.Trace("Updating: {0}", s.Name);

                if (!s.RootFolderPath.IsNullOrWhiteSpace())
                {
                    s.Path = _publisherPathBuilder.BuildPath(s, useExistingRelativeFolder);

                    _logger.Trace("Changing path for {0} to {1}", s.Name, s.Path);
                }
                else
                {
                    _logger.Trace("Not changing path for: {0}", s.Name);
                }
            }

            _publisherRepository.UpdateMany(publisher);
            _logger.Debug("{0} publishers updated", publisher.Count);

            return publisher;
        }
    }
}
