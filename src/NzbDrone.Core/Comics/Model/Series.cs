using System.Collections.Generic;
using Equ;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Comics
{
    public class Series : Entity<Series>
    {
        public string ForeignSeriesId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public bool Numbered { get; set; }
        public int WorkCount { get; set; }
        public int PrimaryWorkCount { get; set; }

        [MemberwiseEqualityIgnore]
        public LazyLoaded<List<SeriesComicLink>> LinkItems { get; set; }

        [MemberwiseEqualityIgnore]
        public LazyLoaded<List<Comic>> Comics { get; set; }

        // A placeholder used in refresh only
        public string ForeignAuthorId { get; set; }

        public override string ToString()
        {
            return string.Format("[{0}][{1}]", ForeignSeriesId.NullSafe(), Title.NullSafe());
        }

        public override void UseMetadataFrom(Series other)
        {
            ForeignSeriesId = other.ForeignSeriesId;
            Title = other.Title;
            Description = other.Description;
            Numbered = other.Numbered;
            WorkCount = other.WorkCount;
            PrimaryWorkCount = other.PrimaryWorkCount;
        }

        public override void UseDbFieldsFrom(Series other)
        {
            Id = other.Id;
        }
    }
}
