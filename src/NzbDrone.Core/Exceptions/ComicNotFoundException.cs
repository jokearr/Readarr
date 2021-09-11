using NzbDrone.Common.Exceptions;

namespace NzbDrone.Core.Exceptions
{
    public class ComicNotFoundException : NzbDroneException
    {
        public string MusicBrainzId { get; set; }

        public ComicNotFoundException(string musicbrainzId)
            : base(string.Format("Book with id {0} was not found, it may have been removed from metadata server.", musicbrainzId))
        {
            MusicBrainzId = musicbrainzId;
        }

        public ComicNotFoundException(string musicbrainzId, string message, params object[] args)
            : base(message, args)
        {
            MusicBrainzId = musicbrainzId;
        }

        public ComicNotFoundException(string musicbrainzId, string message)
            : base(message)
        {
            MusicBrainzId = musicbrainzId;
        }
    }
}
