using System;
using System.Collections.Generic;
using NzbDrone.Core.Comics;

namespace NzbDrone.Core.MetadataSource
{
    public interface IProvideComicInfo
    {
        Tuple<string, Comic, List<PublisherMetadata>> GetComicInfo(string id, bool useCache = true);
        HashSet<string> GetChangedComics(DateTime startTime);
    }
}
