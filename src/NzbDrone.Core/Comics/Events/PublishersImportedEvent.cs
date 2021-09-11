using System.Collections.Generic;
using NzbDrone.Common.Messaging;

namespace NzbDrone.Core.Comics.Events
{
    public class PublishersImportedEvent : IEvent
    {
        public List<int> PublisherIds { get; private set; }
        public bool DoRefresh { get; private set; }

        public PublishersImportedEvent(List<int> publisherIds, bool doRefresh = true)
        {
            PublisherIds = publisherIds;
            DoRefresh = doRefresh;
        }
    }
}
