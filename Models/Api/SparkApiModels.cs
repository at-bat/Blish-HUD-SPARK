using rp.spark.Models;
using System;
using System.Collections.Generic;

namespace rp.spark.Models.Api
{
    public class ProfileOwner
    {
        public string AccountName { get; set; } = string.Empty;

        public string OfficialCharacterName { get; set; } = string.Empty;

        public string Race { get; set; } = string.Empty;

        public string Profession { get; set; } = string.Empty;

        public string Specialization { get; set; } = string.Empty;

        public ProfileRegion Region { get; set; } = ProfileRegion.NA;

        public bool IsCharacterApiVerified { get; set; }

        public static ProfileOwner FromProfile(CharacterProfile profile)
        {
            return new ProfileOwner
            {
                AccountName = profile?.AccountName?.Trim() ?? string.Empty,
                OfficialCharacterName = profile?.CharacterName?.Trim() ?? string.Empty,
                Race = profile?.Race?.Trim() ?? string.Empty,
                Profession = profile?.Profession?.Trim() ?? string.Empty,
                Specialization = profile?.Specialization?.Trim() ?? string.Empty,
                Region = profile?.Region ?? ProfileRegion.NA,
                IsCharacterApiVerified = profile?.IsCharacterVerified ?? false
            };
        }

        public static ProfileOwner FromPresence(PlayerPresence presence)
        {
            return new ProfileOwner
            {
                AccountName = presence?.AccountName?.Trim() ?? string.Empty,
                OfficialCharacterName = presence?.OfficialCharacterName?.Trim() ?? string.Empty,
                Race = presence?.Race?.Trim() ?? string.Empty,
                Profession = presence?.Profession?.Trim() ?? string.Empty,
                Region = presence?.Region ?? ProfileRegion.NA,
                IsCharacterApiVerified = presence?.IsVerified ?? false
            };
        }
    }

    // Public profile only contains fields intentionally shared with other SPARK users.
    // Private notes stay local
    // Potentially shared private profile notes could be added but it needs to be kept separate from the profile data
    public class ProfileData
    {
        public int SchemaVersion { get; set; } = 1;

        public string ProfileId { get; set; } = string.Empty;

        public bool IsMature { get; set; }

        public string ProfileName { get; set; } = "Default";

        public string DisplayCharacterName { get; set; } = string.Empty;

        public string Pronouns { get; set; } = string.Empty;

        public string CustomProfession { get; set; } = string.Empty;

        public string Currently { get; set; } = string.Empty;

        public string OutOfCharacterInfo { get; set; } = string.Empty;

        public List<AtAGlanceEntry> AtAGlance { get; set; } = new List<AtAGlanceEntry>();

        public ProfileExperience Experience { get; set; } = ProfileExperience.Hidden;

        public ProfilePreferenceFlags Preferences { get; set; } = ProfilePreferenceFlags.None;

        public ProfileThemeFlags Themes { get; set; } = ProfileThemeFlags.None;

        public ProfileStyleFlags Styles { get; set; } = ProfileStyleFlags.None;

        public string KnownFor { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Probably could make this look nicer but I'm okay with this
        public static ProfileData FromProfile(CharacterProfile profile)
        {
            return new ProfileData
            {
                ProfileId = profile?.ProfileId?.Trim() ?? string.Empty,
                IsMature = profile?.IsMature ?? false,
                ProfileName = string.IsNullOrWhiteSpace(profile?.ProfileName) ? "Default" : profile.ProfileName.Trim(),
                DisplayCharacterName = profile?.DisplayName?.Trim() ?? string.Empty,
                Pronouns = profile?.Pronouns?.Trim() ?? string.Empty,
                CustomProfession = profile?.CustomProfession?.Trim() ?? string.Empty,
                Currently = profile?.Currently?.Trim() ?? string.Empty,
                OutOfCharacterInfo = profile?.OutOfCharacterInfo?.Trim() ?? string.Empty,
                AtAGlance = profile?.AtAGlance ?? new List<AtAGlanceEntry>(),
                Experience = profile?.Experience ?? ProfileExperience.Hidden,
                Preferences = profile?.Preferences ?? ProfilePreferenceFlags.None,
                Themes = profile?.Themes ?? ProfileThemeFlags.None,
                Styles = profile?.Styles ?? ProfileStyleFlags.None,
                KnownFor = profile?.KnownFor?.Trim() ?? string.Empty,
                Description = profile?.Description?.Trim() ?? string.Empty,
                CreatedAt = profile?.CreatedAt ?? DateTime.UtcNow,
                UpdatedAt = profile?.UpdatedAt ?? DateTime.UtcNow
            };
        }

        public CharacterProfile ToProfile(ProfileOwner identity, PlayerPresence presence = null)
        {
            return new CharacterProfile
            {
                ProfileId = ProfileId,
                IsMature = IsMature,
                ProfileName = ProfileName,
                AccountName = TextUtil.FirstNonEmpty(identity?.AccountName, presence?.AccountName),
                CharacterName = TextUtil.FirstNonEmpty(identity?.OfficialCharacterName, presence?.OfficialCharacterName),
                DisplayName = DisplayCharacterName,
                Pronouns = Pronouns,
                Race = TextUtil.FirstNonEmpty(identity?.Race, presence?.Race),
                Profession = TextUtil.FirstNonEmpty(identity?.Profession, presence?.Profession),
                Specialization = identity?.Specialization?.Trim() ?? string.Empty,
                CustomProfession = CustomProfession,
                IsCharacterVerified = identity?.IsCharacterApiVerified ?? presence?.IsVerified ?? false,
                Region = identity?.Region ?? presence?.Region ?? ProfileRegion.NA,
                Currently = Currently,
                OutOfCharacterInfo = OutOfCharacterInfo,
                AtAGlance = AtAGlance ?? new List<AtAGlanceEntry>(),
                Experience = Experience,
                Preferences = Preferences,
                Themes = Themes,
                Styles = Styles,
                KnownFor = KnownFor,
                Description = Description,
                CreatedAt = CreatedAt,
                UpdatedAt = UpdatedAt
            };
        }
    }

    // This is basically the tooltip information on the online list
    // A small snippet vs the whole profile sent at once so users don't gotta open every profile to get a gist of things
    public class PresencePublishRequest
    {
        public PlayerPresence Presence { get; set; } = new PlayerPresence();
    }

    public class PresenceListResponse
    {
        public DateTime ServerTime { get; set; } = DateTime.UtcNow;

        public List<PlayerPresence> Entries { get; set; } = new List<PlayerPresence>();
    }

    // This is for publishing your nearby data to players on the same map (not necessarily the same instance) to see nearby RPers.
    // Best thing we can do since we can't replicate Total RP 3 and see who has an RP profile by resolving on a nameplate click, etc.
    // This will be for a window that polls the map for which SPARK users are on it and let you see how close you are (approximately) to them if on the same shard
    public class NearbyPresencePublishRequest
    {
        public NearbyPresence Nearby { get; set; } = new NearbyPresence();
    }

    public class NearbyPresenceSearchRequest
    {
        public ProfileRegion Region { get; set; } = ProfileRegion.NA;

        public bool IncludeMature { get; set; }

        public int MapId { get; set; }

        public uint ShardId { get; set; }

        public string ServerAddress { get; set; } = string.Empty;

        public bool HasPosition { get; set; }

        public double X { get; set; }

        public double Y { get; set; }

        public double Z { get; set; }

        public double MaxDistanceMeters { get; set; } = 600;
    }

    public class NearbyPresenceListResponse
    {
        public DateTime ServerTime { get; set; } = DateTime.UtcNow;

        public List<NearbyPresence> Entries { get; set; } = new List<NearbyPresence>();
    }

    // These fields are validated on the server. Account and character fields aren't trusted until the GW2 subtoken verifies them.
    // Server rejects falsified account/character names if there's some mismatch.
    public class ProfileUploadRequest
    {
        public ProfileOwner Identity { get; set; } = new ProfileOwner();

        public ProfileData Profile { get; set; } = new ProfileData();

        public PlayerPresence Presence { get; set; } = new PlayerPresence();

        public static ProfileUploadRequest FromProfile(CharacterProfile profile, PlayerPresence presence)
        {
            return new ProfileUploadRequest
            {
                Identity = presence != null
                    ? ProfileOwner.FromPresence(presence)
                    : ProfileOwner.FromProfile(profile),
                Profile = ProfileData.FromProfile(profile),
                Presence = presence ?? new PlayerPresence()
            };
        }
    }

    // Fixed this slightly, we use the server verified identity/presence in the downloaded profile in case someone somehow slipped a different name through in the JSON
    // Probably overkill but people are crafty
    public class ProfileDownload
    {
        public ProfileOwner Identity { get; set; } = new ProfileOwner();

        public ProfileData Profile { get; set; } = new ProfileData();

        public PlayerPresence Presence { get; set; } = new PlayerPresence();

        public CharacterProfile ToCharacterProfile()
        {
            return Profile?.ToProfile(Identity, Presence) ?? new CharacterProfile();
        }
    }

    public class AccountBlockRequest
    {
        public string AccountName { get; set; } = string.Empty;
    }

    public class BlocklistRequest
    {
        public List<string> AccountNames { get; set; } = new List<string>();
    }

    public class ProfileReportRequest
    {
        public string AccountName { get; set; } = string.Empty;

        public string OfficialCharacterName { get; set; } = string.Empty;

        public string ProfileId { get; set; } = string.Empty;

        public string Reason { get; set; } = string.Empty;

        public static ProfileReportRequest FromProfile(
            CharacterProfile profile,
            PlayerPresence presence,
            string reason)
        {
            return new ProfileReportRequest
            {
                AccountName = TextUtil.FirstNonEmpty(presence?.AccountName, profile?.AccountName),
                OfficialCharacterName = TextUtil.FirstNonEmpty(presence?.OfficialCharacterName, profile?.CharacterName),
                ProfileId = TextUtil.FirstNonEmpty(presence?.ActiveProfileId, profile?.ProfileId),
                Reason = reason?.Trim() ?? string.Empty
            };
        }
    }

    public class ProfileReportResponse
    {
        public bool Ok { get; set; }

        public string ReportId { get; set; } = string.Empty;
    }
}
