using System;
using System.Collections.Generic;
using Equ;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Profiles.Metadata;
using NzbDrone.Core.Profiles.Qualities;

namespace NzbDrone.Core.Comics
{
    public class Publisher : Entity<Publisher>
    {
        public Publisher()
        {
            Tags = new HashSet<int>();
            Metadata = new PublisherMetadata();
        }

        // These correspond to columns in the Authors table
        public int PublisherMetadataId { get; set; }
        public string CleanName { get; set; }
        public bool Monitored { get; set; }
        public DateTime? LastInfoSync { get; set; }
        public string Path { get; set; }
        public string RootFolderPath { get; set; }
        public DateTime Added { get; set; }
        public int QualityProfileId { get; set; }
        public int MetadataProfileId { get; set; }
        public HashSet<int> Tags { get; set; }
        [MemberwiseEqualityIgnore]
        public AddPublisherOptions AddOptions { get; set; }

        // Dynamically loaded from DB
        [MemberwiseEqualityIgnore]
        public LazyLoaded<PublisherMetadata> Metadata { get; set; }
        [MemberwiseEqualityIgnore]
        public LazyLoaded<QualityProfile> QualityProfile { get; set; }
        [MemberwiseEqualityIgnore]
        public LazyLoaded<MetadataProfile> MetadataProfile { get; set; }
        [MemberwiseEqualityIgnore]
        public LazyLoaded<List<Comic>> Comics { get; set; }
        [MemberwiseEqualityIgnore]
        public LazyLoaded<List<Series>> Series { get; set; }

        //compatibility properties
        [MemberwiseEqualityIgnore]
        public string Name
        {
            get { return Metadata.Value.Name; }
            set { Metadata.Value.Name = value; }
        }

/*        [MemberwiseEqualityIgnore]
        public string ForeignAuthorId
        {
            get { return Metadata.Value.ForeignAuthorId; }
            set { Metadata.Value.ForeignAuthorId = value; }
        }*/

        public override string ToString()
        {
            return string.Format("[{0}][{1}]", Metadata.Value.ForeignPublisherId.NullSafe(), Metadata.Value.Name.NullSafe());
        }

        public override void UseMetadataFrom(Publisher other)
        {
            CleanName = other.CleanName;
        }

        public override void UseDbFieldsFrom(Publisher other)
        {
            Id = other.Id;
            PublisherMetadataId = other.PublisherMetadataId;
            Monitored = other.Monitored;
            LastInfoSync = other.LastInfoSync;
            Path = other.Path;
            RootFolderPath = other.RootFolderPath;
            Added = other.Added;
            QualityProfileId = other.QualityProfileId;
            MetadataProfileId = other.MetadataProfileId;
            Tags = other.Tags;
            AddOptions = other.AddOptions;
        }

        public override void ApplyChanges(Publisher other)
        {
            Path = other.Path;
            QualityProfileId = other.QualityProfileId;
            QualityProfile = other.QualityProfile;
            MetadataProfileId = other.MetadataProfileId;
            MetadataProfile = other.MetadataProfile;

            Comics = other.Comics;
            Tags = other.Tags;
            AddOptions = other.AddOptions;
            RootFolderPath = other.RootFolderPath;
            Monitored = other.Monitored;
        }
    }
}
