using NzbDrone.Common.Messaging;

namespace NzbDrone.Core.Comics.Events
{
    public class PublisherDeletedEvent : IEvent
    {
        public Publisher Publisher { get; private set; }
        public bool DeleteFiles { get; private set; }
        public bool AddImportListExclusion { get; private set; }

        public PublisherDeletedEvent(Publisher publisher, bool deleteFiles, bool addImportListExclusion)
        {
            Publisher = publisher;
            DeleteFiles = deleteFiles;
            AddImportListExclusion = addImportListExclusion;
        }
    }
}
