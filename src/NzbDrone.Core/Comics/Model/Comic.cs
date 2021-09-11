using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Equ;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.MediaFiles;

namespace NzbDrone.Core.Comics
{
    public class Comic : Entity<Comic>
    {
        public Comic()
        {
            Links = new List<Links>();
            Genres = new List<string>();
            Publisher = new Publisher();
            AddOptions = new AddComicOptions();

            //Ratings = new Ratings();
        }

        // These correspond to columns in the Books table
        // These are metadata entries
        public int PublisherMetadataId { get; set; }
        public string ForeignComicId { get; set; }
        public string TitleSlug { get; set; }
        public string Title { get; set; }
        public DateTime? ReleaseDate { get; set; }
        public List<Links> Links { get; set; }
        public List<string> Genres { get; set; }

        //public Ratings Ratings { get; set; }

        // These are Readarr generated/config
        public string CleanTitle { get; set; }
        public bool Monitored { get; set; }
        public bool AnyEditionOk { get; set; }
        public DateTime? LastInfoSync { get; set; }
        public DateTime Added { get; set; }
        [MemberwiseEqualityIgnore]
        public AddComicOptions AddOptions { get; set; }

        // These are dynamically queried from other tables
        [MemberwiseEqualityIgnore]
        public LazyLoaded<PublisherMetadata> PublisherMetadata { get; set; }
        [MemberwiseEqualityIgnore]
        public LazyLoaded<Publisher> Publisher { get; set; }
        [MemberwiseEqualityIgnore]
        public LazyLoaded<List<Edition>> Editions { get; set; }
        [MemberwiseEqualityIgnore]
        public LazyLoaded<List<BookFile>> BookFiles { get; set; }
        [MemberwiseEqualityIgnore]
/*        public LazyLoaded<List<SeriesBookLink>> SeriesLinks { get; set; }*/

        //compatibility properties with old version of Book
/*        [MemberwiseEqualityIgnore]*/
        [JsonIgnore]
        public int PublisherId
        {
            get { return Publisher?.Value?.Id ?? 0; }
            set { Publisher.Value.Id = value; }
        }

        public override string ToString()
        {
            return string.Format("[{0}][{1}]", ForeignComicId, Title.NullSafe());
        }

        public override void UseMetadataFrom(Comic other)
        {
            ForeignComicId = other.ForeignComicId;
            TitleSlug = other.TitleSlug;
            Title = other.Title;
            ReleaseDate = other.ReleaseDate;
            Links = other.Links;
            Genres = other.Genres;

            //Ratings = other.Ratings;
            CleanTitle = other.CleanTitle;
        }

        public override void UseDbFieldsFrom(Comic other)
        {
            Id = other.Id;
            PublisherMetadataId = other.PublisherMetadataId;
            Monitored = other.Monitored;
            AnyEditionOk = other.AnyEditionOk;
            LastInfoSync = other.LastInfoSync;
            Added = other.Added;
            AddOptions = other.AddOptions;
        }

        public override void ApplyChanges(Comic other)
        {
            ForeignComicId = other.ForeignComicId;
            AddOptions = other.AddOptions;
            Monitored = other.Monitored;
            AnyEditionOk = other.AnyEditionOk;
        }
    }
}
