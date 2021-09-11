using System.Collections.Generic;
using System.Linq;
using Dapper;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Comics
{
    public interface IPublisherRepository : IBasicRepository<Publisher>
    {
        bool PublisherPathExists(string path);
        Publisher FindByName(string cleanName);
        Publisher FindById(string foreignPublisherId);
        Dictionary<int, string> AllPublisherPaths();
        Publisher GetPublisherByMetadataId(int publisherMetadataId);
        List<Publisher> GetPublishersByMetadataId(IEnumerable<int> publisherMetadataId);
    }

    public class PublisherRepository : BasicRepository<Publisher>, IPublisherRepository
    {
        public PublisherRepository(IMainDatabase database,
                                IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        protected override SqlBuilder Builder() => new SqlBuilder()
            .Join<Publisher, PublisherMetadata>((a, m) => a.PublisherMetadataId == m.Id);

        protected override List<Publisher> Query(SqlBuilder builder) => Query(_database, builder).ToList();

        public static IEnumerable<Publisher> Query(IDatabase database, SqlBuilder builder)
        {
            return database.QueryJoined<Publisher, PublisherMetadata>(builder, (publisher, metadata) =>
            {
                publisher.Metadata = metadata;
                return publisher;
            });
        }

        public bool PublisherPathExists(string path)
        {
            return Query(c => c.Path == path).Any();
        }

        public Publisher FindById(string foreignPublisherId)
        {
            return Query(Builder().Where<PublisherMetadata>(m => m.PublisherId == foreignPublisherId)).SingleOrDefault();
        }

        public Publisher FindByName(string cleanName)
        {
            cleanName = cleanName.ToLowerInvariant();

            return Query(s => s.CleanName == cleanName).ExclusiveOrDefault();
        }

        public Dictionary<int, string> AllPublisherPaths()
        {
            using (var conn = _database.OpenConnection())
            {
                var strSql = "SELECT Id AS [Key], Path AS [Value] FROM Publishers";
                return conn.Query<KeyValuePair<int, string>>(strSql).ToDictionary(x => x.Key, x => x.Value);
            }
        }

        public Publisher GetPublisherByMetadataId(int publisherMetadataId)
        {
            return Query(s => s.PublisherMetadataId == publisherMetadataId).SingleOrDefault();
        }

        public List<Publisher> GetPublishersByMetadataId(IEnumerable<int> publisherMetadataIds)
        {
            return Query(s => publisherMetadataIds.Contains(s.PublisherMetadataId));
        }
    }
}
