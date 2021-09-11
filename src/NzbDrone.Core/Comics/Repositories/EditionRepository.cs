using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Common.EnsureThat;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Comics
{
    public interface IEditionRepository : IBasicRepository<Edition>
    {
        Edition FindByForeignEditionId(string foreignEditionId);
        List<Edition> FindByComic(int id);
        List<Edition> FindByPublisher(int id);
        List<Edition> FindByPublisherMetadataId(int id, bool onlyMonitored);
        Edition FindByTitle(int publisherMetadataId, string title);
        List<Edition> GetEditionsForRefresh(int comicId, IEnumerable<string> foreignEditionIds);
        List<Edition> SetMonitored(Edition edition);
    }

    public class EditionRepository : BasicRepository<Edition>, IEditionRepository
    {
        public EditionRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        public Edition FindByForeignEditionId(string foreignEditionId)
        {
            var edition = Query(x => x.ForeignEditionId == foreignEditionId).SingleOrDefault();

            return edition;
        }

        public List<Edition> GetEditionsForRefresh(int comicId, IEnumerable<string> foreignEditionIds)
        {
            return Query(r => r.ComicId == comicId || foreignEditionIds.Contains(r.ForeignEditionId));
        }

        public List<Edition> FindByComic(int id)
        {
            // populate the comics and publisher metadata also
            // this hopefully speeds up the track matching a lot
            var builder = new SqlBuilder()
                .LeftJoin<Edition, Comic>((e, b) => e.ComicId == b.Id)
                .LeftJoin<Comic, PublisherMetadata>((b, a) => b.PublisherMetadataId == a.Id)
                .Where<Edition>(r => r.ComicId == id);

            return _database.QueryJoined<Edition, Comic, PublisherMetadata>(builder, (edition, comic, metadata) =>
            {
                if (comic != null)
                {
                    comic.PublisherMetadata = metadata;
                    edition.Comic = comic;
                }

                return edition;
            }).ToList();
        }

        public List<Edition> FindByPublisher(int id)
        {
            return Query(Builder().Join<Edition, Comic>((e, b) => e.ComicId == b.Id)
                         .Join<Comic, Publisher>((b, a) => b.PublisherMetadataId == a.PublisherMetadataId)
                         .Where<Publisher>(a => a.Id == id));
        }

        public List<Edition> FindByPublisherMetadataId(int publisherMetadataId, bool onlyMonitored)
        {
            var builder = Builder().Join<Edition, Comic>((e, b) => e.ComicId == b.Id)
                .Where<Comic>(b => b.PublisherMetadataId == publisherMetadataId);

            if (onlyMonitored)
            {
                builder = builder.Where<Edition>(e => e.Monitored == true);
            }

            return Query(builder);
        }

        public Edition FindByTitle(int publisherMetadataId, string title)
        {
            return Query(Builder().Join<Edition, Comic>((e, b) => e.ComicId == b.Id)
                .Where<Comic>(b => b.PublisherMetadataId == publisherMetadataId)
                .Where<Edition>(e => e.Monitored == true)
                .Where<Edition>(e => e.Title == title))
                .FirstOrDefault();
        }

        public List<Edition> SetMonitored(Edition edition)
        {
            var allEditions = FindByComic(edition.ComicId);
            allEditions.ForEach(r => r.Monitored = r.Id == edition.Id);
            Ensure.That(allEditions.Count(x => x.Monitored) == 1).IsTrue();
            UpdateMany(allEditions);
            return allEditions;
        }
    }
}
