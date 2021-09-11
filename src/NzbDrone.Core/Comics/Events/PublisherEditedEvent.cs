using NzbDrone.Common.Messaging;

namespace NzbDrone.Core.Comics.Events
{
    public class PublisherEditedEvent : IEvent
    {
        public Publisher Publisher { get; private set; }
        public Publisher OldPublisher { get; private set; }

        public PublisherEditedEvent(Publisher publisher, Publisher oldPublisher)
        {
            Publisher = publisher;
            OldPublisher = oldPublisher;
        }
    }
}
