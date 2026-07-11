using rp.spark.Models;
using System;
using System.Linq;

namespace rp.spark.UI.Views
{
    internal static class ProfileText
    {
        public static string Clean(string value)
        {
            return value?.Trim() ?? string.Empty;
        }

        public static string DisplayName(CharacterProfile profile, string fallback = "Unnamed Character")
        {
            if (!string.IsNullOrWhiteSpace(profile?.DisplayName))
                return profile.DisplayName.Trim();

            if (!string.IsNullOrWhiteSpace(profile?.CharacterName))
                return profile.CharacterName.Trim();

            return fallback;
        }

        public static string AccountName(CharacterProfile profile, PlayerPresence presence, string fallback = "Unknown")
        {
            if (!string.IsNullOrWhiteSpace(presence?.AccountName))
                return presence.AccountName.Trim();

            return string.IsNullOrWhiteSpace(profile?.AccountName)
                ? fallback
                : profile.AccountName.Trim();
        }

        public static string PresenceLocation(PlayerPresence presence)
        {
            return string.IsNullOrWhiteSpace(presence?.LocationName)
                ? "Unknown"
                : presence.LocationName.Trim();
        }

        public static string PresenceRace(PlayerPresence presence, string fallback = "Unknown")
        {
            var race = presence?.VisibleRace();

            return string.IsNullOrWhiteSpace(race)
                ? fallback
                : race.Trim();
        }

        public static string PresenceCharacterDetails(PlayerPresence presence)
        {
            return CharacterDetails(PresenceRace(presence, string.Empty), presence?.VisibleProfession());
        }

        public static string ProfileProfession(CharacterProfile profile)
        {
            if (!string.IsNullOrWhiteSpace(profile?.CustomProfession))
                return profile.CustomProfession.Trim();

            return Clean(profile?.Profession);
        }

        public static string ProfileRace(CharacterProfile profile)
        {
            if (!string.IsNullOrWhiteSpace(profile?.CustomRace))
                return profile.CustomRace.Trim();

            return Clean(profile?.Race);
        }

        public static string ProfileCharacterDetails(CharacterProfile profile)
        {
            return CharacterDetails(profile?.Race, ProfileProfession(profile));
        }

        public static string SavedCharacterName(SavedProfile record)
        {
            if (!string.IsNullOrWhiteSpace(record?.Presence?.DisplayCharacterName))
                return record.Presence.DisplayCharacterName.Trim();

            if (!string.IsNullOrWhiteSpace(record?.Profile?.DisplayName))
                return record.Profile.DisplayName.Trim();

            if (!string.IsNullOrWhiteSpace(record?.Presence?.OfficialCharacterName))
                return record.Presence.OfficialCharacterName.Trim();

            if (!string.IsNullOrWhiteSpace(record?.Profile?.CharacterName))
                return record.Profile.CharacterName.Trim();

            return "Unknown character";
        }

        public static string SavedCharacterName(SavedProfileSummary summary)
        {
            if (!string.IsNullOrWhiteSpace(summary?.DisplayCharacterName))
                return summary.DisplayCharacterName.Trim();

            if (!string.IsNullOrWhiteSpace(summary?.OfficialCharacterName))
                return summary.OfficialCharacterName.Trim();

            return "Unknown character";
        }

        public static string SavedAccountName(SavedProfile record, string fallback = "Unknown")
        {
            if (!string.IsNullOrWhiteSpace(record?.Presence?.AccountName))
                return record.Presence.AccountName.Trim();

            return string.IsNullOrWhiteSpace(record?.Profile?.AccountName)
                ? fallback
                : record.Profile.AccountName.Trim();
        }

        public static string SavedAccountName(SavedProfileSummary summary, string fallback = "Unknown")
        {
            return string.IsNullOrWhiteSpace(summary?.AccountName)
                ? fallback
                : summary.AccountName.Trim();
        }

        public static string SavedRace(SavedProfile record, string fallback = "Unknown")
        {
            if (!string.IsNullOrWhiteSpace(record?.Presence?.Race))
                return record.Presence.Race.Trim();

            return string.IsNullOrWhiteSpace(record?.Profile?.Race)
                ? fallback
                : record.Profile.Race.Trim();
        }

        public static string SavedRace(SavedProfileSummary summary, string fallback = "Unknown")
        {
            return string.IsNullOrWhiteSpace(summary?.Race)
                ? fallback
                : summary.Race.Trim();
        }

        public static string SavedProfession(SavedProfile record)
        {
            var profession = record?.Presence?.VisibleProfession();

            if (string.IsNullOrWhiteSpace(profession))
                profession = string.IsNullOrWhiteSpace(record?.Profile?.CustomProfession)
                    ? record?.Profile?.Profession
                    : record.Profile.CustomProfession;

            return Clean(profession);
        }

        public static string SavedProfession(SavedProfileSummary summary)
        {
            return Clean(string.IsNullOrWhiteSpace(summary?.CustomProfession)
                ? summary?.Profession
                : summary.CustomProfession);
        }

        public static string SavedCharacterDetails(SavedProfile record)
        {
            return CharacterDetails(SavedRace(record, string.Empty), SavedProfession(record));
        }

        public static string SavedCharacterDetails(SavedProfileSummary summary)
        {
            return CharacterDetails(SavedRace(summary, string.Empty), SavedProfession(summary));
        }

        public static DateTime SavedLastSeen(SavedProfile record)
        {
            var lastSeen = record?.Presence?.LastSeen ?? default;

            return lastSeen == default
                ? record?.CachedAt ?? default
                : lastSeen.ToUniversalTime();
        }

        public static DateTime SavedLastSeen(SavedProfileSummary summary)
        {
            var lastSeen = summary?.LastSeen ?? default;

            return lastSeen == default
                ? summary?.CachedAt ?? default
                : lastSeen.ToUniversalTime();
        }

        public static string CharacterDetails(string race, string profession)
        {
            race = Clean(race);
            profession = Clean(profession);

            if (string.IsNullOrWhiteSpace(race) && string.IsNullOrWhiteSpace(profession))
                return string.Empty;

            if (string.IsNullOrWhiteSpace(race))
                return profession;

            if (string.IsNullOrWhiteSpace(profession))
                return race;

            return $"{race} | {profession}";
        }

        public static string JoinSearchText(params string[] parts)
        {
            return string.Join(
                " ",
                (parts ?? new string[0])
                    .Select(part => Clean(part))
                    .Where(part => !string.IsNullOrWhiteSpace(part)));
        }

        public static string FormatShortTime(DateTime dateTime, string fallback = "")
        {
            if (dateTime == default)
                return fallback;

            return dateTime.ToLocalTime().ToString("MMM d h:mm tt");
        }
    }
}
