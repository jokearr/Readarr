using Equ;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Comics
{
    public class Links : MemberwiseEquatable<Links>, IEmbeddedDocument
    {
        public string Url { get; set; }
        public string Name { get; set; }
    }
}
