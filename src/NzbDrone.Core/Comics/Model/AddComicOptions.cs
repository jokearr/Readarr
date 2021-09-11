using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Comics
{
    public class AddComicOptions : IEmbeddedDocument
    {
        public AddComicOptions()
        {
            // default in case not set in db
            AddType = ComicAddType.Automatic;
        }

        public ComicAddType AddType { get; set; }
        public bool SearchForNewBook { get; set; }
    }

    public enum ComicAddType
    {
        Automatic,
        Manual
    }
}
