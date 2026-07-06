using System;

namespace rp.spark.Models
{
    public class NearbyPresence
    {
        public int SchemaVersion { get; set; } = 1;

        public PlayerPresence Presence { get; set; } = new PlayerPresence();

        public int MapId { get; set; }

        public uint ShardId { get; set; }

        public bool HasPosition { get; set; }

        public double X { get; set; }

        public double Y { get; set; }

        public double Z { get; set; }

        public double DistanceMeters { get; set; } = -1;

        public DateTime LastSeen { get; set; } = DateTime.UtcNow;

        public string Key()
        {
            return Presence?.Key() ?? string.Empty;
        }

        public string VisibleName()
        {
            return Presence?.VisibleName() ?? string.Empty;
        }
    }
}