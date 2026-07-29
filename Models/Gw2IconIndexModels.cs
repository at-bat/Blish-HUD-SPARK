using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace rp.spark.Models
{

    public class Gw2IconIndexEntry
    {
        [JsonProperty("s")]
        public string Source { get; set; } = string.Empty;

        [JsonProperty("n")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("q")]
        public List<string> Aliases { get; set; } = new List<string>();

        [JsonProperty("d")]
        public string Description { get; set; } = string.Empty;

        [JsonProperty("a")]
        public int AssetId { get; set; }

        [JsonProperty("k")]
        public List<string> Keywords { get; set; } = new List<string>();
    }

    public class Gw2IconSearchResult
    {
        public int AssetId { get; set; }

        public string Source { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public IReadOnlyList<string> Keywords { get; set; } = Array.Empty<string>();
    }
}
