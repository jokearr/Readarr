using System;
using System.Collections.Generic;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Comics;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.MediaFiles
{
    public class ComicFile : ModelBase
    {
        // these are model properties
        public string Path { get; set; }
        public long Size { get; set; }
        public DateTime Modified { get; set; }
        public DateTime DateAdded { get; set; }
        public string SceneName { get; set; }
        public string ReleaseGroup { get; set; }
        public QualityModel Quality { get; set; }
        public MediaInfoModel MediaInfo { get; set; }
        public int EditionId { get; set; }
        public int CalibreId { get; set; }
        public int Part { get; set; }

        // These are queried from the database
        public LazyLoaded<Publisher> Publisher { get; set; }
        public LazyLoaded<Edition> Edition { get; set; }

        // Calculated manually
        public int PartCount { get; set; }

        public override string ToString()
        {
            return string.Format("[{0}] {1}", Id, Path);
        }

        public string GetSceneOrFileName()
        {
            if (SceneName.IsNotNullOrWhiteSpace())
            {
                return SceneName;
            }

            if (Path.IsNotNullOrWhiteSpace())
            {
                return System.IO.Path.GetFileName(Path);
            }

            return string.Empty;
        }
    }
}
