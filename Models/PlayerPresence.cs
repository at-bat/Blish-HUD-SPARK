using System;

namespace rp.spark.Models
{
    public class PlayerPresence
    {
        public int SchemaVersion { get; set; } = 1;

        public string AccountName { get; set; } = string.Empty;

        public string OfficialCharacterName { get; set; } = string.Empty;

        public string DisplayCharacterName { get; set; } = string.Empty;

        public string Race { get; set; } = string.Empty;

        public string Profession { get; set; } = string.Empty;

        public string CustomProfession { get; set; } = string.Empty;

        public string ActiveProfileId { get; set; } = string.Empty;

        public string ActiveProfileName { get; set; } = string.Empty;

        public DateTime ProfileUpdatedAtTime { get; set; }

        public RPStatus Status { get; set; } = RPStatus.Online;

        public string Currently { get; set; } = string.Empty;

        public string OutOfCharacterInfo { get; set; } = string.Empty;

        public string LocationName { get; set; } = string.Empty;

        public bool IsLocationHidden { get; set; }

        public ProfileRegion Region { get; set; } = ProfileRegion.NA;

        public bool IsVerified { get; set; }

        public bool IsInGame { get; set; }

        public bool HasActiveProfile { get; set; }

        public bool ShareEnabled { get; set; }

        public bool CanShare { get; set; }

        public string ShareBlockReason { get; set; } = string.Empty;

        public DateTime LastSeen { get; set; } = DateTime.UtcNow;

        public string VisibleName()
        {
            if (!string.IsNullOrWhiteSpace(DisplayCharacterName))
                return DisplayCharacterName.Trim();

            return OfficialCharacterName?.Trim() ?? string.Empty;
        }

        public string VisibleProfession()
        {
            if (!string.IsNullOrWhiteSpace(CustomProfession))
                return CustomProfession.Trim();

            return Profession?.Trim() ?? string.Empty;
        }

        public string Key()
        {
            return $"{AccountName?.Trim() ?? string.Empty}|{OfficialCharacterName?.Trim() ?? string.Empty}";
        }
    }
}
