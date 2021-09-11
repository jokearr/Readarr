using System.Collections.Generic;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Comics
{
    public class MonitoringOptions : IEmbeddedDocument
    {
        public MonitoringOptions()
        {
            ComicsToMonitor = new List<string>();
        }

        public MonitorTypes Monitor { get; set; }
        public List<string> ComicsToMonitor { get; set; }
        public bool Monitored { get; set; }
    }

    public enum MonitorTypes
    {
        All,
        Future,
        Missing,
        Existing,
        Latest,
        First,
        None,
        Unknown
    }
}
