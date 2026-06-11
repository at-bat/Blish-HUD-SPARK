using System;

namespace rp.spark.Models
{
    public class SavedProfile
    {
        public int SchemaVersion { get; set; } = 1;

        public string CacheKey { get; set; } = string.Empty;

        public CharacterProfile Profile { get; set; } = new CharacterProfile();

        public PlayerPresence Presence { get; set; } = new PlayerPresence();

        public DateTime CachedAt { get; set; }

        public bool IsBookmarked { get; set; }

        public DateTime? BookmarkedAt { get; set; }
    }
}
