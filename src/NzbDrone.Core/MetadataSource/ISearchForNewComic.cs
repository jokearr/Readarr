using System.Collections.Generic;
using NzbDrone.Core.Comics;

namespace NzbDrone.Core.MetadataSource
{
    public interface ISearchForNewComic
    {
        List<Comic> SearchForNewComic(string title, string publisher);
        List<Comic> SearchByIsbn(string isbn);
        List<Comic> SearchByAsin(string asin);
        List<Comic> SearchByComicvineId(int comicvineId);
    }
}
