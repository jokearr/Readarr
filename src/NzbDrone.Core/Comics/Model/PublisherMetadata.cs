using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Common.Extensions;

namespace NzbDrone.Core.Comics
{
    public class PublisherMetadata : Entity<PublisherMetadata>
    {
        public PublisherMetadata()
        {
            Images = new List<MediaCover.MediaCover>();
            Links = new List<Links>();
            Aliases = new List<string>();
        }

        public string PublisherId { get; set; }
        public string Deck { get; set; }
        public string Description { get; set; }
        public string LocationAddress { get; set; }
        public string LocationState { get; set; }
        public string LocationCity { get; set; }
        public List<MediaCover.MediaCover> Images { get; set; }
        public List<string> Aliases { get; set; }
        public List<Links> Links { get; set; }
        public string Name { get; set; }
        public string ForeignPublisherId { get; set; }
        /*
                public string TitleSlug { get; set; }

                public string SortName { get; set; }
                public string NameLastFirst { get; set; }
                public string SortNameLastFirst { get; set; }
                public string Overview { get; set; }
                public string Disambiguation { get; set; }
                public string Gender { get; set; }
                public string Hometown { get; set; }
                public DateTime? Born { get; set; }
                public DateTime? Died { get; set; }
                public PublisherStatusType Status { get; set; }
                public List<string> Genres { get; set; }
                public Ratings Ratings { get; set; }*/

        public override string ToString()
        {
            return string.Format("[{0}][{1}]", ForeignPublisherId, Name.NullSafe());
        }

        public override void UseMetadataFrom(PublisherMetadata other)
        {
            Name = other.Name;
            Aliases = other.Aliases;
            Links = other.Links;

            ForeignPublisherId = other.ForeignPublisherId;

            //TitleSlug = other.TitleSlug;
            //NameLastFirst = other.NameLastFirst;
            //SortName = other.SortName;
            //SortNameLastFirst = other.SortNameLastFirst;
            //Overview = other.Overview.IsNullOrWhiteSpace() ? Overview : other.Overview;
            //Disambiguation = other.Disambiguation;
            //Gender = other.Gender;
            //Hometown = other.Hometown;
            //Born = other.Born;
            //Died = other.Died;
            //Status = other.Status;
            //Images = other.Images.Any() ? other.Images : Images;
            //Genres = other.Genres;
            //Ratings = other.Ratings.Votes > 0 ? other.Ratings : Ratings;
        }
    }
}
