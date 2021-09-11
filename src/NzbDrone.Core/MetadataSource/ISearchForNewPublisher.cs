using System.Collections.Generic;
using NzbDrone.Core.Comics;

namespace NzbDrone.Core.MetadataSource
{
    public interface ISearchForNewPublisher
    {
        List<Publisher> SearchForNewPublisher(string title);
    }
}
