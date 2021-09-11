using NzbDrone.Common.Messaging;

namespace NzbDrone.Core.Comics.Events
{
    public class ComicDeletedEvent : IEvent
    {
        public Comic Comic { get; private set; }
        public bool DeleteFiles { get; private set; }
        public bool AddImportListExclusion { get; private set; }

        public ComicDeletedEvent(Comic comic, bool deleteFiles, bool addImportListExclusion)
        {
            Comic = comic;
            DeleteFiles = deleteFiles;
            AddImportListExclusion = addImportListExclusion;
        }
    }
}
