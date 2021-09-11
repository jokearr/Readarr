using System.Net;
using NzbDrone.Core.Exceptions;

namespace NzbDrone.Core.MetadataSource.Comicvine
{
    public class ComicvineException : NzbDroneClientException
    {
        public ComicvineException(string message)
            : base(HttpStatusCode.ServiceUnavailable, message)
        {
        }

        public ComicvineException(string message, params object[] args)
            : base(HttpStatusCode.ServiceUnavailable, message, args)
        {
        }
    }
}
