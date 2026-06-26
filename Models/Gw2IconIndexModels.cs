using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace rp.spark.Models
{
    public class Gw2IconIndexDocument
    {
        [JsonProperty("schema")]
        public int Schema { get; set; }

        [JsonProperty("generatedAt")]
        public DateTime? GeneratedAt { get; set; }

        [JsonProperty("gw2BuildId")]
        public int Gw2BuildId { get; set; }

        [JsonProperty("count")]
        public int Count { get; set; }

        [JsonProperty("entries")]
        public List<Gw2IconIndexEntry> Entries { get; set; } = new List<Gw2IconIndexEntry>();
    }

    public class Gw2IconIndexEntry
    {
        [JsonProperty("s")]
        public string Source { get; set; } = string.Empty;

        [JsonProperty("n")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("d")]
        public string Description { get; set; } = string.Empty;

        [JsonProperty("a")]
        public int AssetId { get; set; }

        [JsonProperty("k")]
        public List<string> Keywords { get; set; } = new List<string>();
    }

    public class GW2IconIndexManifest
    {
        [JsonProperty("schema")]
        public int Schema { get; set; }

        [JsonProperty("version")]
        public string Version { get; set; } = string.Empty;

        [JsonProperty("generatedAt")]
        public DateTime? GeneratedAt { get; set; }

        [JsonProperty("gw2BuildId")]
        public int Gw2BuildId { get; set; }

        [JsonProperty("count")]
        public int Count { get; set; }

        [JsonProperty("indexUrl")]
        public string IndexUrl { get; set; } = string.Empty;

        [JsonProperty("indexEncoding")]
        public string IndexEncoding { get; set; } = string.Empty;

        [JsonProperty("sha256")]
        public string Sha256 { get; set; } = string.Empty;

        [JsonProperty("uncompressedSha256")]
        public string UncompressedSha256 { get; set; } = string.Empty;

        [JsonProperty("uncompressedUrl")]
        public string UncompressedUrl { get; set; } = string.Empty;
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
