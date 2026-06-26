using System;
using System.Collections.Generic;

namespace rp.spark.Models
{
    public enum ProfileExperience
    {
        Hidden = 0,
        New = 1,
        Returning = 2,
        Proficient = 3,
        Experienced = 4
    }

    [Flags]
    public enum ProfilePreferenceFlags
    {
        None = 0,
        Casual = 1 << 0,
        OneShot = 1 << 1,
        LongTerm = 1 << 2,
        EventRoleplay = 1 << 3,
        SmallGroup = 1 << 4,
        LargeGroup = 1 << 5,
        All = Casual | OneShot | LongTerm | EventRoleplay | SmallGroup | LargeGroup
    }

    [Flags]
    public enum ProfileThemeFlags
    {
        None = 0,
        Comedy = 1 << 0,
        Combat = 1 << 1,
        Romance = 1 << 2,
        SliceOfLife = 1 << 3,
        All = Comedy | Combat | Romance | SliceOfLife
    }

    [Flags]
    public enum ProfileStyleFlags
    {
        None = 0,
        WalkUpFriendly = 1 << 0,
        WhisperFirst = 1 << 1,
        OpenToNewContacts = 1 << 2,
        LoreFriendly = 1 << 3,
        FlexibleLore = 1 << 4,
        All = WalkUpFriendly | WhisperFirst | OpenToNewContacts | LoreFriendly | FlexibleLore
    }

    public static class ProfileLabels
    {
        public static readonly string[] RpStatusOptions =
        {
            "Online",
            "Invisible",
            "Looking for RP",
            "Busy"
        };

        public static readonly string[] ExperienceOptions =
        {
            "Don't show",
            "New",
            "Returning",
            "Proficient",
            "Experienced"
        };

        public static readonly KeyValuePair<ProfilePreferenceFlags, string>[] PreferenceOptions =
        {
            new KeyValuePair<ProfilePreferenceFlags, string>(ProfilePreferenceFlags.Casual, "Casual"),
            new KeyValuePair<ProfilePreferenceFlags, string>(ProfilePreferenceFlags.OneShot, "One-Shot"),
            new KeyValuePair<ProfilePreferenceFlags, string>(ProfilePreferenceFlags.LongTerm, "Long-Term"),
            new KeyValuePair<ProfilePreferenceFlags, string>(ProfilePreferenceFlags.EventRoleplay, "Event RP"),
            new KeyValuePair<ProfilePreferenceFlags, string>(ProfilePreferenceFlags.SmallGroup, "Small Group"),
            new KeyValuePair<ProfilePreferenceFlags, string>(ProfilePreferenceFlags.LargeGroup, "Large Group")
        };

        public static readonly KeyValuePair<ProfileThemeFlags, string>[] ThemeOptions =
        {
            new KeyValuePair<ProfileThemeFlags, string>(ProfileThemeFlags.Comedy, "Comedy"),
            new KeyValuePair<ProfileThemeFlags, string>(ProfileThemeFlags.Combat, "Combat"),
            new KeyValuePair<ProfileThemeFlags, string>(ProfileThemeFlags.Romance, "Romance"),
            new KeyValuePair<ProfileThemeFlags, string>(ProfileThemeFlags.SliceOfLife, "Slice of Life")
        };

        public static readonly KeyValuePair<ProfileStyleFlags, string>[] StyleOptions =
        {
            new KeyValuePair<ProfileStyleFlags, string>(ProfileStyleFlags.WalkUpFriendly, "Walk-Up Friendly"),
            new KeyValuePair<ProfileStyleFlags, string>(ProfileStyleFlags.WhisperFirst, "Whisper First"),
            new KeyValuePair<ProfileStyleFlags, string>(ProfileStyleFlags.OpenToNewContacts, "Open to New Contacts"),
            new KeyValuePair<ProfileStyleFlags, string>(ProfileStyleFlags.LoreFriendly, "Lore Friendly"),
            new KeyValuePair<ProfileStyleFlags, string>(ProfileStyleFlags.FlexibleLore, "Flexible Lore")
        };

        public static string GetExperienceLabel(ProfileExperience experience)
        {
            switch (experience)
            {
                case ProfileExperience.New:
                    return "New";
                case ProfileExperience.Returning:
                    return "Returning";
                case ProfileExperience.Proficient:
                    return "Proficient";
                case ProfileExperience.Experienced:
                    return "Experienced";
                default:
                    return "Don't show";
            }
        }

        public static ProfileExperience ParseExperience(string label)
        {
            var value = (label ?? string.Empty).Trim();

            if (string.Equals(value, "New", StringComparison.OrdinalIgnoreCase))
                return ProfileExperience.New;

            if (string.Equals(value, "Returning", StringComparison.OrdinalIgnoreCase))
                return ProfileExperience.Returning;

            if (string.Equals(value, "Proficient", StringComparison.OrdinalIgnoreCase))
                return ProfileExperience.Proficient;

            if (string.Equals(value, "Experienced", StringComparison.OrdinalIgnoreCase))
                return ProfileExperience.Experienced;

            return ProfileExperience.Hidden;
        }

        public static string StatusLabel(RPStatus status)
        {
            switch (status)
            {
                case RPStatus.Invisible:
                    return "Invisible";
                case RPStatus.Looking:
                    return "Looking for RP";
                case RPStatus.Busy:
                    return "Busy";
                case RPStatus.Offline:
                    return "Offline";
                default:
                    return "Online";
            }
        }

        public static RPStatus ParseStatus(string label)
        {
            var value = (label ?? string.Empty).Trim();

            if (string.Equals(value, "Invisible", StringComparison.OrdinalIgnoreCase))
                return RPStatus.Invisible;

            if (string.Equals(value, "Looking for RP", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "Looking", StringComparison.OrdinalIgnoreCase))
                return RPStatus.Looking;

            if (string.Equals(value, "Busy", StringComparison.OrdinalIgnoreCase))
                return RPStatus.Busy;

            if (string.Equals(value, "Offline", StringComparison.OrdinalIgnoreCase))
                return RPStatus.Offline;

            return RPStatus.Online;
        }
    }

    public class CharacterProfile
    {
        public int SchemaVersion { get; set; } = 1;

        public string ProfileId { get; set; } = Guid.NewGuid().ToString();

        public string ProfileName { get; set; } = "Default";

        public bool IsMature { get; set; }

        public string AccountName { get; set; } = string.Empty;

        public string CharacterName { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public string Pronouns { get; set; } = string.Empty;

        public string Race { get; set; } = string.Empty;

        public string Profession { get; set; } = string.Empty;

        public string Specialization { get; set; } = string.Empty;

        public string CustomProfession { get; set; } = string.Empty;

        public bool IsCharacterVerified { get; set; }

        public ProfileRegion Region { get; set; } = ProfileRegion.NA;

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
    }
}
