using System;

namespace rp.spark.Models
{
    public class ProfileNote
    {
        public int SchemaVersion { get; set; } = 1;

        public string CacheKey { get; set; } = string.Empty;

        public string ProfileId { get; set; } = string.Empty;

        public string AccountName { get; set; } = string.Empty;

        public string OfficialCharacterName { get; set; } = string.Empty;

        public string Text { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
