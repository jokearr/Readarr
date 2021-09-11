using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.Comics
{
    public interface IComicRepository : IBasicRepository<Comic>
    {
        List<Comic> GetComics(int publisherId);
        List<Comic> GetLastComics(IEnumerable<int> publisherMetadataIds);
        List<Comic> GetNextComics(IEnumerable<int> publisherMetadataIds);
        List<Comic> GetComicsByPublisherMetadataId(int publisherMetadataId);
        List<Comic> GetComicsForRefresh(int publisherMetadataId, IEnumerable<string> foreignIds);
        List<Comic> GetComicsByFileIds(IEnumerable<int> fileIds);
        Comic FindByTitle(int publisherMetadataId, string title);
        Comic FindById(string foreignComicId);
        Comic FindBySlug(string titleSlug);
        PagingSpec<Comic> ComicsWithoutFiles(PagingSpec<Comic> pagingSpec);
        PagingSpec<Comic> ComicsWhereCutoffUnmet(PagingSpec<Comic> pagingSpec, List<QualitiesBelowCutoff> qualitiesBelowCutoff);
        List<Comic> ComicsBetweenDates(DateTime startDate, DateTime endDate, bool includeUnmonitored);
        List<Comic> PublisherComicsBetweenDates(Publisher publisher, DateTime startDate, DateTime endDate, bool includeUnmonitored);
        void SetMonitoredFlat(Comic comic, bool monitored);
        void SetMonitored(IEnumerable<int> ids, bool monitored);
        List<Comic> GetPublisherComicsWithFiles(Publisher publisher);
    }

    public class ComicRepository : BasicRepository<Comic>, IComicRepository
    {
        public ComicRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        public List<Comic> GetComics(int publisherId)
        {
            return Query(Builder().Join<Comic, Publisher>((l, r) => l.PublisherMetadataId == r.PublisherMetadataId).Where<Publisher>(a => a.Id == publisherId));
        }

        public List<Comic> GetLastComics(IEnumerable<int> publisherMetadataIds)
        {
            var now = DateTime.UtcNow;
            return Query(Builder().Where<Comic>(x => publisherMetadataIds.Contains(x.PublisherMetadataId) && x.ReleaseDate < now)
                         .GroupBy<Comic>(x => x.PublisherMetadataId)
                         .Having("Comics.ReleaseDate = MAX(Comics.ReleaseDate)"));
        }

        public List<Comic> GetNextComics(IEnumerable<int> publisherMetadataIds)
        {
            var now = DateTime.UtcNow;
            return Query(Builder().Where<Comic>(x => publisherMetadataIds.Contains(x.PublisherMetadataId) && x.ReleaseDate > now)
                         .GroupBy<Comic>(x => x.PublisherMetadataId)
                         .Having("Comics.ReleaseDate = MIN(Comics.ReleaseDate)"));
        }

        public List<Comic> GetComicsByPublisherMetadataId(int publisherMetadataId)
        {
            return Query(s => s.PublisherMetadataId == publisherMetadataId);
        }

        public List<Comic> GetComicsForRefresh(int publisherMetadataId, IEnumerable<string> foreignIds)
        {
            return Query(a => a.PublisherMetadataId == publisherMetadataId || foreignIds.Contains(a.ForeignComicId));
        }

        public List<Comic> GetComicsByFileIds(IEnumerable<int> fileIds)
        {
            return Query(new SqlBuilder()
                         .Join<Comic, Edition>((b, e) => b.Id == e.ComicId)
                         .Join<Edition, ComicFile>((l, r) => l.Id == r.EditionId)
                         .Where<ComicFile>(f => fileIds.Contains(f.Id)))
                .DistinctBy(x => x.Id)
                .ToList();
        }

        public Comic FindById(string foreignComicId)
        {
            return Query(s => s.ForeignComicId == foreignComicId).SingleOrDefault();
        }

        public Comic FindBySlug(string titleSlug)
        {
            return Query(s => s.TitleSlug == titleSlug).SingleOrDefault();
        }

        //x.Id == null is converted to SQL, so warning incorrect
#pragma warning disable CS0472
        private SqlBuilder ComicsWithoutFilesBuilder(DateTime currentTime) => Builder()
            .Join<Comic, Publisher>((l, r) => l.PublisherMetadataId == r.PublisherMetadataId)
            .Join<Publisher, PublisherMetadata>((l, r) => l.PublisherMetadataId == r.Id)
            .Join<Comic, Edition>((b, e) => b.Id == e.ComicId)
            .LeftJoin<Edition, ComicFile>((t, f) => t.Id == f.EditionId)
            .Where<ComicFile>(f => f.Id == null)
            .Where<Edition>(e => e.Monitored == true)
            .Where<Comic>(a => a.ReleaseDate <= currentTime);
#pragma warning restore CS0472

        public PagingSpec<Comic> ComicsWithoutFiles(PagingSpec<Comic> pagingSpec)
        {
            var currentTime = DateTime.UtcNow;

            pagingSpec.Records = GetPagedRecords(ComicsWithoutFilesBuilder(currentTime), pagingSpec, PagedQuery);
            pagingSpec.TotalRecords = GetPagedRecordCount(ComicsWithoutFilesBuilder(currentTime).SelectCountDistinct<Comic>(x => x.Id), pagingSpec);

            return pagingSpec;
        }

        private SqlBuilder ComicsWhereCutoffUnmetBuilder(List<QualitiesBelowCutoff> qualitiesBelowCutoff) => Builder()
            .Join<Comic, Publisher>((l, r) => l.PublisherMetadataId == r.PublisherMetadataId)
            .Join<Publisher, PublisherMetadata>((l, r) => l.PublisherMetadataId == r.Id)
            .Join<Comic, Edition>((b, e) => b.Id == e.ComicId)
            .LeftJoin<Edition, ComicFile>((t, f) => t.Id == f.EditionId)
            .Where<Edition>(e => e.Monitored == true)
            .Where(BuildQualityCutoffWhereClause(qualitiesBelowCutoff));

        private string BuildQualityCutoffWhereClause(List<QualitiesBelowCutoff> qualitiesBelowCutoff)
        {
            var clauses = new List<string>();

            foreach (var profile in qualitiesBelowCutoff)
            {
                foreach (var belowCutoff in profile.QualityIds)
                {
                    clauses.Add(string.Format("(Publishers.[QualityProfileId] = {0} AND ComicFiles.Quality LIKE '%_quality_: {1},%')", profile.ProfileId, belowCutoff));
                }
            }

            return string.Format("({0})", string.Join(" OR ", clauses));
        }

        public PagingSpec<Comic> ComicsWhereCutoffUnmet(PagingSpec<Comic> pagingSpec, List<QualitiesBelowCutoff> qualitiesBelowCutoff)
        {
            pagingSpec.Records = GetPagedRecords(ComicsWhereCutoffUnmetBuilder(qualitiesBelowCutoff), pagingSpec, PagedQuery);

            var countTemplate = $"SELECT COUNT(*) FROM (SELECT /**select**/ FROM {TableMapping.Mapper.TableNameMapping(typeof(Comic))} /**join**/ /**innerjoin**/ /**leftjoin**/ /**where**/ /**groupby**/ /**having**/)";
            pagingSpec.TotalRecords = GetPagedRecordCount(ComicsWhereCutoffUnmetBuilder(qualitiesBelowCutoff).Select(typeof(Comic)), pagingSpec, countTemplate);

            return pagingSpec;
        }

        public List<Comic> ComicsBetweenDates(DateTime startDate, DateTime endDate, bool includeUnmonitored)
        {
            var builder = Builder().Where<Comic>(rg => rg.ReleaseDate >= startDate && rg.ReleaseDate <= endDate);

            if (!includeUnmonitored)
            {
                builder = builder.Where<Comic>(e => e.Monitored == true)
                    .Join<Comic, Publisher>((l, r) => l.PublisherMetadataId == r.PublisherMetadataId)
                    .Where<Publisher>(e => e.Monitored == true);
            }

            return Query(builder);
        }

        public List<Comic> PublisherComicsBetweenDates(Publisher publisher, DateTime startDate, DateTime endDate, bool includeUnmonitored)
        {
            var builder = Builder().Where<Comic>(rg => rg.ReleaseDate >= startDate &&
                                                 rg.ReleaseDate <= endDate &&
                                                 rg.PublisherMetadataId == publisher.PublisherMetadataId);

            if (!includeUnmonitored)
            {
                builder = builder.Where<Comic>(e => e.Monitored == true)
                    .Join<Comic, Publisher>((l, r) => l.PublisherMetadataId == r.PublisherMetadataId)
                    .Where<Publisher>(e => e.Monitored == true);
            }

            return Query(builder);
        }

        public void SetMonitoredFlat(Comic comic, bool monitored)
        {
            comic.Monitored = monitored;
            SetFields(comic, p => p.Monitored);

            ModelUpdated(comic, true);
        }

        public void SetMonitored(IEnumerable<int> ids, bool monitored)
        {
            var comics = ids.Select(x => new Comic { Id = x, Monitored = monitored }).ToList();
            SetFields(comics, p => p.Monitored);
        }

        public Comic FindByTitle(int publisherMetadataId, string title)
        {
            var cleanTitle = Parser.Parser.CleanPublisherName(title);

            if (string.IsNullOrEmpty(cleanTitle))
            {
                cleanTitle = title;
            }

            return Query(s => (s.CleanTitle == cleanTitle || s.Title == title) && s.PublisherMetadataId == publisherMetadataId)
                .ExclusiveOrDefault();
        }

        public List<Comic> GetPublisherComicsWithFiles(Publisher publisher)
        {
            return Query(Builder()
                         .Join<Comic, Edition>((b, e) => b.Id == e.ComicId)
                         .Join<Edition, ComicFile>((t, f) => t.Id == f.EditionId)
                         .Where<Comic>(x => x.PublisherMetadataId == publisher.PublisherMetadataId)
                         .Where<Edition>(e => e.Monitored == true));
        }
    }
}
