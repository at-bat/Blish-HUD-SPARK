using rp.spark.Models;
using System;

namespace rp.spark.Services
{
    internal static class PresenceMapper
    {
        public static CharacterProfile CreateFromPresence(PlayerPresence presence)
        {
            presence = presence ?? new PlayerPresence();

            return new CharacterProfile
            {
                ProfileName = string.IsNullOrWhiteSpace(presence.ActiveProfileName)
                    ? "Profile Loading"
                    : presence.ActiveProfileName.Trim(),
                ProfileId = presence.ActiveProfileId,
                AccountName = Clean(presence.AccountName),
                CharacterName = Clean(presence.OfficialCharacterName),
                DisplayName = Clean(presence.DisplayCharacterName),
                Race = Clean(presence.Race),
                Profession = Clean(presence.Profession),
                CustomProfession = Clean(presence.CustomProfession),
                Currently = Clean(presence.Currently),
                OutOfCharacterInfo = Clean(presence.OutOfCharacterInfo),
                KnownFor = "Not loading fully.",
                Description = "If you see this, something didn't load right or the SPARK webserver is down possibly."
            };
        }

        public static void FillPresence(PlayerPresence target, PlayerPresence source)
        {
            if (target == null || source == null)
                return;

            if (string.IsNullOrWhiteSpace(target.AccountName))
                target.AccountName = Clean(source.AccountName);

            if (string.IsNullOrWhiteSpace(target.OfficialCharacterName))
                target.OfficialCharacterName = Clean(source.OfficialCharacterName);

            if (string.IsNullOrWhiteSpace(target.ActiveProfileId))
                target.ActiveProfileId = Clean(source.ActiveProfileId);

            if (string.IsNullOrWhiteSpace(target.ActiveProfileName))
                target.ActiveProfileName = Clean(source.ActiveProfileName);

            if (target.ProfileUpdatedAtTime == default)
                target.ProfileUpdatedAtTime = source.ProfileUpdatedAtTime;
        }

        public static void FillMissingPresence(PlayerPresence presence, CharacterProfile profile)
        {
            if (presence == null || profile == null)
                return;

            if (string.IsNullOrWhiteSpace(presence.ActiveProfileId))
                presence.ActiveProfileId = Clean(profile.ProfileId);

            if (string.IsNullOrWhiteSpace(presence.ActiveProfileName))
                presence.ActiveProfileName = Clean(profile.ProfileName);

            if (string.IsNullOrWhiteSpace(presence.AccountName))
                presence.AccountName = Clean(profile.AccountName);

            if (string.IsNullOrWhiteSpace(presence.OfficialCharacterName))
                presence.OfficialCharacterName = Clean(profile.CharacterName);

            if (string.IsNullOrWhiteSpace(presence.DisplayCharacterName))
                presence.DisplayCharacterName = Clean(profile.DisplayName);

            if (string.IsNullOrWhiteSpace(presence.Race))
                presence.Race = Clean(profile.Race);

            if (string.IsNullOrWhiteSpace(presence.Profession))
                presence.Profession = Clean(profile.Profession);

            if (string.IsNullOrWhiteSpace(presence.CustomProfession))
                presence.CustomProfession = Clean(profile.CustomProfession);

            if (string.IsNullOrWhiteSpace(presence.Currently))
                presence.Currently = Clean(profile.Currently);

            if (string.IsNullOrWhiteSpace(presence.OutOfCharacterInfo))
                presence.OutOfCharacterInfo = Clean(profile.OutOfCharacterInfo);
        }

        public static void FillProfileFromPresence(CharacterProfile profile, PlayerPresence presence)
        {
            if (profile == null || presence == null)
                return;

            if (string.IsNullOrWhiteSpace(profile.ProfileId) && !string.IsNullOrWhiteSpace(presence.ActiveProfileId))
                profile.ProfileId = presence.ActiveProfileId.Trim();

            if (string.IsNullOrWhiteSpace(profile.AccountName))
                profile.AccountName = Clean(presence.AccountName);

            if (string.IsNullOrWhiteSpace(profile.CharacterName))
                profile.CharacterName = Clean(presence.OfficialCharacterName);

            if (string.IsNullOrWhiteSpace(profile.DisplayName))
                profile.DisplayName = Clean(presence.DisplayCharacterName);

            if (string.IsNullOrWhiteSpace(profile.Race))
                profile.Race = Clean(presence.Race);

            if (string.IsNullOrWhiteSpace(profile.Profession))
                profile.Profession = Clean(presence.Profession);

            if (string.IsNullOrWhiteSpace(profile.CustomProfession))
                profile.CustomProfession = Clean(presence.CustomProfession);
        }

        public static PlayerPresence CreateOfflinePresence(PlayerPresence presence)
        {
            var offlinePresence = CopyProfileFields(presence);

            offlinePresence.Status = RPStatus.Invisible;
            offlinePresence.LocationName = "Hidden";
            offlinePresence.IsLocationHidden = true;
            offlinePresence.IsInGame = false;
            offlinePresence.ShareEnabled = false;
            offlinePresence.CanShare = false;
            offlinePresence.ShareBlockReason = presence?.ShareBlockReason ?? string.Empty;
            offlinePresence.LastSeen = DateTime.UtcNow;

            return offlinePresence;
        }

        public static PlayerPresence ClonePresence(PlayerPresence presence)
        {
            if (presence == null)
                return null;

            var clone = CopyProfileFields(presence);

            clone.Status = presence.Status;
            clone.Currently = Clean(presence.Currently);
            clone.OutOfCharacterInfo = Clean(presence.OutOfCharacterInfo);
            clone.LocationName = Clean(presence.LocationName);
            clone.IsLocationHidden = presence.IsLocationHidden;
            clone.IsInGame = presence.IsInGame;
            clone.ShareEnabled = presence.ShareEnabled;
            clone.CanShare = presence.CanShare;
            clone.LastSeen = presence.LastSeen;

            return clone;
        }

        private static PlayerPresence CopyProfileFields(PlayerPresence presence)
        {
            presence = presence ?? new PlayerPresence();

            return new PlayerPresence
            {
                AccountName = Clean(presence.AccountName),
                OfficialCharacterName = Clean(presence.OfficialCharacterName),
                DisplayCharacterName = Clean(presence.DisplayCharacterName),
                Race = Clean(presence.Race),
                Profession = Clean(presence.Profession),
                CustomProfession = Clean(presence.CustomProfession),
                ActiveProfileId = Clean(presence.ActiveProfileId),
                ActiveProfileName = Clean(presence.ActiveProfileName),
                ProfileUpdatedAtTime = presence.ProfileUpdatedAtTime,
                Region = presence.Region,
                IsVerified = presence.IsVerified,
                HasActiveProfile = presence.HasActiveProfile,
                ShareBlockReason = Clean(presence.ShareBlockReason)
            };
        }

        private static string Clean(string value)
        {
            return value?.Trim() ?? string.Empty;
        }
    }
}
