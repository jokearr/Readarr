using Newtonsoft.Json;

namespace NzbDrone.Core.MetadataSource.Comicvine
{
    public class SearchJsonResource
    {
        [JsonProperty("imageUrl")]
        public string ImageUrl { get; set; }

        [JsonProperty("comicId")]
        public int ComicId { get; set; }

        [JsonProperty("workId")]
        public int WorkId { get; set; }

        [JsonProperty("comicUrl")]
        public string ComicUrl { get; set; }

        [JsonProperty("from_search")]
        public bool FromSearch { get; set; }

        [JsonProperty("from_srp")]
        public bool FromSrp { get; set; }

        [JsonProperty("qid")]
        public string Qid { get; set; }

        [JsonProperty("rank")]
        public int Rank { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("comicTitleBare")]
        public string ComicTitleBare { get; set; }

        [JsonProperty("numPages")]
        public int PageCount { get; set; }

        [JsonProperty("avgRating")]
        public decimal AverageRating { get; set; }

        [JsonProperty("ratingsCount")]
        public int RatingsCount { get; set; }

        [JsonProperty("publisher")]
        public PublisherJsonResource Publisher { get; set; }

        [JsonProperty("kcrPreviewUrl")]
        public string KcrPreviewUrl { get; set; }

        [JsonProperty("description")]
        public DescriptionJsonResource Description { get; set; }
    }

    public class PublisherJsonResource
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("isGoodreadsPublisher")]
        public bool IsGoodreadsPublisher { get; set; }

        [JsonProperty("profileUrl")]
        public string ProfileUrl { get; set; }

        [JsonProperty("worksListUrl")]
        public string WorksListUrl { get; set; }
    }

    public class DescriptionJsonResource
    {
        [JsonProperty("html")]
        public string Html { get; set; }

        [JsonProperty("truncated")]
        public bool Truncated { get; set; }

        [JsonProperty("fullContentUrl")]
        public string FullContentUrl { get; set; }
    }
}
