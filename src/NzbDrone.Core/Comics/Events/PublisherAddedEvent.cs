using NzbDrone.Common.Messaging;

namespace NzbDrone.Core.Comics.Events
{
    public class PublisherAddedEvent : IEvent
    {
        public Publisher Publisher { get; private set; }
        public bool DoRefresh { get; private set; }

        public PublisherAddedEvent(Publisher publisher, bool doRefresh = true)
        {
            Publisher = publisher;
            DoRefresh = doRefresh;
        }
    }
}
