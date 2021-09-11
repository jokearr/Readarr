using NzbDrone.Common.Messaging;

namespace NzbDrone.Core.Comics.Events
{
    public class ComicAddedEvent : IEvent
    {
        public Comic Comic { get; private set; }
        public bool DoRefresh { get; private set; }

        public ComicAddedEvent(Comic comic, bool doRefresh = true)
        {
            Comic = comic;
            DoRefresh = doRefresh;
        }
    }
}
