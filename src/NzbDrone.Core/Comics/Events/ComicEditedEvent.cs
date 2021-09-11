using NzbDrone.Common.Messaging;

namespace NzbDrone.Core.Comics.Events
{
    public class ComicEditedEvent : IEvent
    {
        public Comic Comic { get; private set; }
        public Comic OldComic { get; private set; }

        public ComicEditedEvent(Comic comic, Comic oldComic)
        {
            Comic = comic;
            OldComic = oldComic;
        }
    }
}
