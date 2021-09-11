using Equ;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Comics
{
    public class SeriesComicLink : Entity<SeriesComicLink>
    {
        public string Position { get; set; }
        public int SeriesId { get; set; }
        public int BookId { get; set; }
        public bool IsPrimary { get; set; }

        [MemberwiseEqualityIgnore]
        public LazyLoaded<Series> Series { get; set; }
        [MemberwiseEqualityIgnore]
        public LazyLoaded<Comic> Comic { get; set; }

        public override void UseMetadataFrom(SeriesComicLink other)
        {
            Position = other.Position;
            IsPrimary = other.IsPrimary;
        }

        public override void UseDbFieldsFrom(SeriesComicLink other)
        {
            Id = other.Id;
            SeriesId = other.SeriesId;
            BookId = other.BookId;
        }
    }
}
