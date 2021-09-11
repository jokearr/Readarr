using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NLog;
using NzbDrone.Common.Cache;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Core.Comics;
using NzbDrone.Core.Exceptions;
using NzbDrone.Core.Http;
using NzbDrone.Core.MediaCover;
using NzbDrone.Core.Parser;

namespace NzbDrone.Core.MetadataSource.Comicvine
{
    public class ComicvineSearchProxy : ISearchForNewPublisher, ISearchForNewComic, ISearchForNewEntity
    {
        private static readonly RegexReplace FullSizeImageRegex = new RegexReplace(@"\._[SU][XY]\d+_.jpg$",
            ".jpg",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex DuplicateSpacesRegex = new Regex(@"\s{2,}", RegexOptions.Compiled);

        private static readonly Regex NoPhotoRegex = new Regex(@"/nophoto/(comic|user)/",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly List<Regex> SeriesRegex = new List<Regex>
        {
            new Regex(@"\((?<series>[^,]+),\s+#(?<position>[\w\d\.]+)\)$", RegexOptions.Compiled),
            new Regex(@"(The\s+(?<series>.+)\s+Series\s+Comic\s+(?<position>[\w\d\.]+)\)$)", RegexOptions.Compiled)
        };

        private readonly ICachedHttpResponseService _cachedHttpClient;
        private readonly Logger _logger;
        private readonly IProvideComicInfo _comicInfo;
        private readonly IPublisherService _publisherService;
        private readonly IComicService _comicService;
        private readonly IEditionService _editionService;
        private readonly IHttpRequestBuilderFactory _searchBuilder;
        private readonly ICached<HashSet<string>> _cache;

        public ComicvineSearchProxy(ICachedHttpResponseService cachedHttpClient,
            IProvideComicInfo comicInfo,
            IPublisherService publisherService,
            IComicService comicService,
            IEditionService editionService,
            Logger logger,
            ICacheManager cacheManager)
        {
            _cachedHttpClient = cachedHttpClient;
            _comicInfo = comicInfo;
            _publisherService = publisherService;
            _publisherService = publisherService;
            _comicService = comicService;
            _editionService = editionService;
            _cache = cacheManager.GetCache<HashSet<string>>(GetType());
            _logger = logger;

            _searchBuilder = new HttpRequestBuilder("https://www.comicvine.com/comic/auto_complete")
                .AddQueryParam("format", "json")
                .SetHeader("User-Agent",
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/83.0.4103.116 Safari/537.36")
                .KeepAlive()
                .CreateFactory();
        }

        public List<Publisher> SearchForNewPublisher(string title)
        {
            var comics = SearchForNewComic(title, null);

            return comics.Select(x => x.Publisher.Value).ToList();
        }

        public List<Comic> SearchForNewComic(string title, string publisher)
        {
            try
            {
                var lowerTitle = title.ToLowerInvariant();

                var split = lowerTitle.Split(':');
                var prefix = split[0];

                if (split.Length == 2 && new[] { "readarr", "readarrid", "comicvine", "isbn", "asin" }.Contains(prefix))
                {
                    var slug = split[1].Trim();

                    if (slug.IsNullOrWhiteSpace() || slug.Any(char.IsWhiteSpace))
                    {
                        return new List<Comic>();
                    }

                    if (prefix == "comicvine" || prefix == "readarr" || prefix == "readarrid")
                    {
                        var isValid = int.TryParse(slug, out var searchId);
                        if (!isValid)
                        {
                            return new List<Comic>();
                        }

                        return SearchByComicvineId(searchId);
                    }
                    else if (prefix == "isbn")
                    {
                        return SearchByIsbn(slug);
                    }
                    else if (prefix == "asin")
                    {
                        return SearchByAsin(slug);
                    }
                }

                var q = title.ToLower().Trim();
                if (publisher != null)
                {
                    q += " " + publisher;
                }

                return SearchByField("all", q);
            }
            catch (HttpException)
            {
                throw new ComicvineException("Search for '{0}' failed. Unable to communicate with Comicvine.", title);
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, ex.Message);
                throw new ComicvineException("Search for '{0}' failed. Invalid response received from Comicvine.",
                    title);
            }
        }

        public List<Comic> SearchByIsbn(string isbn)
        {
            return SearchByField("isbn", isbn, e => e.Isbn13 = isbn);
        }

        public List<Comic> SearchByAsin(string asin)
        {
            return SearchByField("asin", asin, e => e.Asin = asin);
        }

        public List<Comic> SearchByComicvineId(int id)
        {
            try
            {
                var remote = _comicInfo.GetComicInfo(id.ToString());

                var comic = _comicService.FindById(remote.Item2.ForeignComicId);
                var result = comic ?? remote.Item2;

                // at this point, comic could have the wrong edition.
                // Check if we already have the correct edition.
                var remoteEdition = remote.Item2.Editions.Value.Single(x => x.Monitored);
                var localEdition = _editionService.GetEditionByForeignEditionId(remoteEdition.ForeignEditionId);
                if (localEdition != null)
                {
                    result.Editions = new List<Edition> { localEdition };
                }

                // If we don't have the correct edition in the response, add it in.
                if (!result.Editions.Value.Any(x => x.ForeignEditionId == remoteEdition.ForeignEditionId))
                {
                    result.Editions.Value.ForEach(x => x.Monitored = false);
                    result.Editions.Value.Add(remoteEdition);
                }

                var publisher = _publisherService.FindById(remote.Item1);
                if (publisher == null)
                {
                    publisher = new Publisher
                    {
                        CleanName = Parser.Parser.CleanPublisherName(remote.Item2.PublisherMetadata.Value.Name),
                        Metadata = remote.Item2.PublisherMetadata.Value
                    };
                }

                result.Publisher = publisher;

                return new List<Comic> { result };
            }
            catch (ComicNotFoundException)
            {
                return new List<Comic>();
            }
        }

        public List<Comic> SearchByField(string field, string query, Action<Edition> applyData = null)
        {
            try
            {
                var httpRequest = _searchBuilder.Create()
                    .AddQueryParam("q", query)
                    .Build();

                var response = _cachedHttpClient.Get<List<SearchJsonResource>>(httpRequest, true, TimeSpan.FromDays(5));

                return response.Resource.SelectList(x =>
                    MapJsonSearchResult(x, response.Resource.Count == 1 ? applyData : null));
            }
            catch (HttpException)
            {
                throw new ComicvineException("Search for {0} '{1}' failed. Unable to communicate with Comicvine.", field, query);
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, ex.Message);
                throw new ComicvineException("Search for {0} '{1}' failed. Invalid response received from Comicvine.", field, query);
            }
        }

        public List<object> SearchForNewEntity(string title)
        {
            var comics = SearchForNewComic(title, null);

            var result = new List<object>();
            foreach (var comic in comics)
            {
                var publisher = comic.Publisher.Value;

                if (!result.Contains(publisher))
                {
                    result.Add(publisher);
                }

                result.Add(comic);
            }

            return result;
        }

        private Comic MapJsonSearchResult(SearchJsonResource resource, Action<Edition> applyData = null)
        {
            var comic = _comicService.FindById(resource.WorkId.ToString());
            var edition = _editionService.GetEditionByForeignEditionId(resource.ComicId.ToString());

            if (edition == null)
            {
                edition = new Edition
                {
                    ForeignEditionId = resource.ComicId.ToString(),
                    Title = resource.ComicTitleBare,
                    TitleSlug = resource.ComicId.ToString(),
                    /*Ratings = new Ratings { Votes = resource.RatingsCount, Value = resource.AverageRating },*/
                    PageCount = resource.PageCount,
                    Overview = resource.Description?.Html ?? string.Empty
                };

                if (applyData != null)
                {
                    applyData(edition);
                }
            }

            edition.Monitored = true;
            edition.ManualAdd = true;

            if (resource.ImageUrl.IsNotNullOrWhiteSpace() && !NoPhotoRegex.IsMatch(resource.ImageUrl))
            {
                edition.Images.Add(new MediaCover.MediaCover
                {
                    Url = FullSizeImageRegex.Replace(resource.ImageUrl),
                    CoverType = MediaCoverTypes.Cover
                });
            }

            if (comic == null)
            {
                comic = new Comic
                {
                    ForeignComicId = resource.WorkId.ToString(),
                    Title = resource.ComicTitleBare,
                    TitleSlug = resource.WorkId.ToString(),
                    /*Ratings = new Ratings { Votes = resource.RatingsCount, Value = resource.AverageRating },*/
                    AnyEditionOk = true
                };
            }

            if (comic.Editions != null)
            {
                if (comic.Editions.Value.Any())
                {
                    edition.Monitored = false;
                }

                comic.Editions.Value.Add(edition);
            }
            else
            {
                comic.Editions = new List<Edition> { edition };
            }

            var publisherId = resource.Publisher.Id.ToString();
            var publisher = _publisherService.FindById(publisherId);

            if (publisher == null)
            {
                publisher = new Publisher
                {
                    CleanName = Parser.Parser.CleanPublisherName(resource.Publisher.Name),
                    Metadata = new PublisherMetadata()
                    {
                        ForeignPublisherId = resource.Publisher.Id.ToString(),
                        Name = DuplicateSpacesRegex.Replace(resource.Publisher.Name, " "),
                        /*TitleSlug = resource.Publisher.Id.ToString()*/
                    }
                };
            }

            comic.Publisher = publisher;
            comic.PublisherMetadata = comic.Publisher.Value.Metadata.Value;
            comic.PublisherMetadataId = publisher.PublisherMetadataId;
            comic.CleanTitle = comic.Title.CleanPublisherName();
            /*comic.SeriesLinks = ComicvineProxy.MapSearchSeries(resource.Title, resource.ComicTitleBare);*/

            return comic;
        }
    }
}
