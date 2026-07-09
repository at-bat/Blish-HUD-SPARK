using Blish_HUD.Modules.Managers;
using Newtonsoft.Json;
using Blish_HUD;
using rp.spark.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace rp.spark.Services
{
    // Small class for importing profiles from your other characters on your account with profiles
    public class ProfileImportGroup
    {
        public string CharacterName { get; set; } = string.Empty;

        public List<CharacterProfile> Profiles { get; set; } = new List<CharacterProfile>();
    }

    public class ProfileRepository
    {
        private static readonly Logger Logger = Logger.GetLogger<ProfileRepository>();

        private readonly string _sparkDirectory;
        private readonly string _profilesDirectory;
        private readonly string _activeProfilesPath;
        private readonly ProfileValidator _validator;
        private readonly object _cacheLock = new object();
        private Dictionary<string, CharacterProfile> _profilesByIdCache;
        private Dictionary<string, string> _activeProfilesCache;

        public event Action<CharacterProfile> ProfileSaved;
        public event Action<string, string, string> ActiveProfileChanged;

        public ProfileRepository(DirectoriesManager directoriesManager, ProfileValidator validator)
        {
            _validator = validator;
            _sparkDirectory = directoriesManager.GetFullDirectoryPath("spark");
            _profilesDirectory = Path.Combine(_sparkDirectory, "profiles");
            _activeProfilesPath = Path.Combine(_sparkDirectory, "active-profiles.json");

            FileStore.EnsureDirectory(_sparkDirectory, Logger, "SPARK");
            FileStore.EnsureDirectory(_profilesDirectory, Logger, "SPARK profile");
        }

        public IReadOnlyList<CharacterProfile> LoadAll()
        {
            lock (_cacheLock)
            {
                return GetProfileCacheLocked()
                    .Values
                    .Select(CloneProfile)
                    .Where(profile => profile != null)
                    .ToList();
            }
        }

        public IReadOnlyList<CharacterProfile> ListForCharacter(string accountName, string officialCharacterName)
        {
            if (string.IsNullOrWhiteSpace(officialCharacterName))
                return new List<CharacterProfile>();

            var normalizedCharacterName = officialCharacterName.Trim();
            var normalizedAccountName = accountName?.Trim() ?? string.Empty;

            return LoadAll()
                .Where(profile => IsSameCharacter(profile, normalizedAccountName, normalizedCharacterName))
                .OrderBy(profile => profile.ProfileName)
                .ThenByDescending(profile => profile.UpdatedAt)
                .ToList();
        }

        public IReadOnlyList<ProfileImportGroup> ListImports(string accountName, string currentCharacterName)
        {
            if (string.IsNullOrWhiteSpace(accountName) || string.IsNullOrWhiteSpace(currentCharacterName))
                return new List<ProfileImportGroup>();

            var cleanAccount = accountName.Trim();
            var cleanCharacter = currentCharacterName.Trim();

            return LoadAll()
                .Where(profile => CanImport(profile, cleanAccount, cleanCharacter))
                .GroupBy(profile => profile.CharacterName.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(group => new ProfileImportGroup
                {
                    CharacterName = group.Key,
                    Profiles = group
                        .OrderBy(profile => ProfileName(profile), StringComparer.OrdinalIgnoreCase)
                        .ThenByDescending(profile => profile.UpdatedAt)
                        .Select(CloneProfile)
                        .ToList()
                })
                .OrderBy(group => group.CharacterName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public CharacterProfile Load(string profileId)
        {
            if (string.IsNullOrWhiteSpace(profileId))
                return null;

            lock (_cacheLock)
            {
                return GetProfileCacheLocked().TryGetValue(profileId.Trim(), out var profile)
                    ? CloneProfile(profile)
                    : null;
            }
        }

        public CharacterProfile LoadForCharacter(string officialCharacterName)
        {
            if (string.IsNullOrWhiteSpace(officialCharacterName))
                return null;

            return LoadAll()
                .Where(profile => string.Equals(
                    profile.CharacterName?.Trim(),
                    officialCharacterName.Trim(),
                    StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(profile => profile.UpdatedAt)
                .FirstOrDefault();
        }

        public CharacterProfile LoadActiveForCharacter(string accountName, string officialCharacterName)
        {
            var activeProfileId = GetActiveProfileId(accountName, officialCharacterName);

            if (string.IsNullOrWhiteSpace(activeProfileId))
                return null;

            var profile = Load(activeProfileId);

            return IsSameCharacter(profile, accountName, officialCharacterName)
                ? profile
                : null;
        }

        // Store profiles by account+character whenever possible.
        // Character only is a fallback for moments where account API data isn't loaded or unavailable.
        public string GetActiveProfileId(string accountName, string officialCharacterName)
        {
            if (string.IsNullOrWhiteSpace(officialCharacterName))
                return string.Empty;

            var activeProfiles = LoadActiveProfiles();
            var exactKey = GetActiveProfileKey(accountName, officialCharacterName);
            var characterOnlyKey = CharacterKey(officialCharacterName);

            if (!string.IsNullOrWhiteSpace(accountName) && activeProfiles.TryGetValue(exactKey, out var exactProfileId))
                return exactProfileId;

            return activeProfiles.TryGetValue(characterOnlyKey, out var fallbackProfileId)
                ? fallbackProfileId
                : string.Empty;
        }

        public void SetActiveProfile(string accountName, string officialCharacterName, string profileId)
        {
            if (string.IsNullOrWhiteSpace(officialCharacterName))
                throw new InvalidOperationException("Cannot set an active profile without a character name.");

            var profile = Load(profileId);

            if (!IsSameCharacter(profile, accountName, officialCharacterName))
                throw new InvalidOperationException("Cannot set an active profile for a different character.");

            UpdateActive(activeProfiles =>
            {
                activeProfiles[CharacterKey(officialCharacterName)] = profileId;

                if (!string.IsNullOrWhiteSpace(accountName))
                    activeProfiles[GetActiveProfileKey(accountName, officialCharacterName)] = profileId;

                return true;
            });

            ActiveProfileChanged?.Invoke(accountName, officialCharacterName, profileId);
        }

        public void ClearActiveProfile(string accountName, string officialCharacterName)
        {
            if (string.IsNullOrWhiteSpace(officialCharacterName))
                return;

            var removed = UpdateActive(activeProfiles =>
            {
                var didRemove = activeProfiles.Remove(GetActiveProfileKey(accountName, officialCharacterName));
                didRemove = activeProfiles.Remove(CharacterKey(officialCharacterName)) || didRemove;
                return didRemove;
            });

            if (!removed)
                return;

            ActiveProfileChanged?.Invoke(accountName, officialCharacterName, string.Empty);
        }

        public CharacterProfile Duplicate(CharacterProfile sourceProfile, string profileName)
        {
            if (sourceProfile == null)
                throw new InvalidOperationException("No profile selected to duplicate.");

            var duplicate = CloneProfile(sourceProfile);

            duplicate.ProfileId = Guid.NewGuid().ToString();
            duplicate.ProfileName = string.IsNullOrWhiteSpace(profileName)
                ? $"{ProfileName(sourceProfile)} Copy"
                : profileName.Trim();
            duplicate.CreatedAt = DateTime.UtcNow;
            duplicate.UpdatedAt = DateTime.UtcNow;

            Save(duplicate);

            return duplicate;
        }

        public CharacterProfile Import(CharacterProfile sourceProfile, PlayerState targetState)
        {
            if (sourceProfile == null)
                throw new InvalidOperationException("No profile selected to import.");

            if (targetState == null 
                || !targetState.CanEditProfile
                || string.IsNullOrWhiteSpace(targetState.AccountName)
                || !targetState.IsCharacterApiVerified)
                throw new InvalidOperationException("Cannot import profiles until SPARK syncs your current character.");

            if (!CanImport(sourceProfile, targetState.AccountName, targetState.OfficialCharacterName))
                throw new InvalidOperationException("This profile cannot be imported for the current account.");

            var imported = CloneProfile(sourceProfile);
            imported.ProfileId = Guid.NewGuid().ToString();
            imported.ProfileName = GetImportName(ProfileName(sourceProfile), targetState);
            ApplyImportTarget(imported, targetState);
            imported.CreatedAt = DateTime.UtcNow;
            imported.UpdatedAt = DateTime.UtcNow;

            Save(imported);
            return imported;
        }

        public void Delete(string profileId)
        {
            if (string.IsNullOrWhiteSpace(profileId))
                return;

            var normalizedProfileId = profileId.Trim();
            var profile = Load(normalizedProfileId);

            try
            {
                var path = GetProfilePath(normalizedProfileId);

                if (!string.IsNullOrWhiteSpace(path))
                    File.Delete(path);
            }
            catch (UnauthorizedAccessException ex)
            {
                BlishWarnings.FileSaveBlocked(ex, profileId, "delete a SPARK profile");
                Logger.Warn(ex, "Failed to delete SPARK profile {profileId}.", profileId);
                throw new InvalidOperationException("Could not delete the profile file. Check file permissions and try again.");
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to delete SPARK profile {profileId}.", profileId);
                throw new InvalidOperationException("Could not delete the profile file. Check file permissions and try again.");
            }

            lock (_cacheLock)
            {
                _profilesByIdCache?.Remove(normalizedProfileId);
            }

            if (profile == null)
                return;

            var activeProfileId = GetActiveProfileId(profile.AccountName, profile.CharacterName);

            if (string.Equals(activeProfileId, profile.ProfileId, StringComparison.OrdinalIgnoreCase))
                ClearActiveProfile(profile.AccountName, profile.CharacterName);
        }


        public void Save(CharacterProfile profile)
        {
            if (profile == null)
                throw new InvalidOperationException("Cannot save a missing profile.");

            profile.ProfileId = string.IsNullOrWhiteSpace(profile.ProfileId)
                ? Guid.NewGuid().ToString()
                : profile.ProfileId.Trim();

            profile.ProfileName = ProfileName(profile);

            if (profile.CreatedAt == default)
                profile.CreatedAt = DateTime.UtcNow;

            profile.UpdatedAt = DateTime.UtcNow;

            var validation = _validator.Validate(profile);

            if (!validation.IsValid)
                throw new InvalidOperationException(string.Join(Environment.NewLine, validation.Errors));

            var profilePath = GetProfilePath(profile.ProfileId);

            if (!FileStore.TryWrite(profilePath, profile, Logger, "SPARK profile"))
                throw new InvalidOperationException("Could not save the profile file. Check file permissions and try again.");

            lock (_cacheLock)
            {
                if (_profilesByIdCache != null)
                    _profilesByIdCache[profile.ProfileId.Trim()] = CloneProfile(profile);
            }

            ProfileSaved?.Invoke(profile);
        }

        private CharacterProfile LoadFromFile(string path)
        {
            var profile = FileStore.ReadFile<CharacterProfile>(path, Logger, "SPARK profile");

            if (profile == null)
                return null;

            NormalizeProfile(profile);

            var validation = _validator.Validate(profile);

            if (validation.IsValid)
                return profile;

            Logger.Warn("Skipping invalid SPARK profile file at {path}: {errors}", path, string.Join("; ", validation.Errors));
            return null;
        }

        private string GetProfilePath(string profileId)
        {
            return FileStore.GetSafePath(_profilesDirectory, profileId?.Trim());
        }

        private Dictionary<string, CharacterProfile> GetProfileCacheLocked()
        {
            if (_profilesByIdCache == null)
                _profilesByIdCache = ReadProfiles();

            return _profilesByIdCache;
        }

        private Dictionary<string, CharacterProfile> ReadProfiles()
        {
            var cache = new Dictionary<string, CharacterProfile>(StringComparer.OrdinalIgnoreCase);

            foreach (var profile in FileStore.GetFiles(_profilesDirectory, Logger, "SPARK profile")
                         .Select(LoadFromFile)
                         .Where(profile => profile != null))
            {
                if (string.IsNullOrWhiteSpace(profile.ProfileId))
                    continue;

                cache[profile.ProfileId.Trim()] = profile;
            }

            return cache;
        }

        private Dictionary<string, string> LoadActiveProfiles()
        {
            lock (_cacheLock)
            {
                if (_activeProfilesCache == null)
                    _activeProfilesCache = ReadActiveProfiles();

                return new Dictionary<string, string>(_activeProfilesCache, StringComparer.OrdinalIgnoreCase);
            }
        }

        private Dictionary<string, string> ReadActiveProfiles()
        {
            var activeProfiles = FileStore.ReadFile<Dictionary<string, string>>(
                _activeProfilesPath,
                Logger,
                "SPARK active profile");

            if (activeProfiles == null)
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            return new Dictionary<string, string>(activeProfiles, StringComparer.OrdinalIgnoreCase);
        }

        // Moved to a helper so update/clear goes through instead of colliding with race conditions, seems much more stable now
        private bool UpdateActive(Func<Dictionary<string, string>, bool> update)
        {
            lock (_cacheLock)
            {
                if (_activeProfilesCache == null)
                    _activeProfilesCache = ReadActiveProfiles();

                var activeProfiles = new Dictionary<string, string>(_activeProfilesCache, StringComparer.OrdinalIgnoreCase);

                if (update == null || !update(activeProfiles))
                    return false;

                if (!FileStore.TryWrite(_activeProfilesPath, activeProfiles, Logger, "SPARK active profile"))
                    throw new InvalidOperationException("Could not save the active profile selection. Check file permissions and try again.");

                _activeProfilesCache = new Dictionary<string, string>(activeProfiles, StringComparer.OrdinalIgnoreCase);
                return true;
            }
        }

        private static void NormalizeProfile(CharacterProfile profile)
        {
            if (profile == null)
                return;

            profile.ProfileId = string.IsNullOrWhiteSpace(profile.ProfileId)
                ? Guid.NewGuid().ToString()
                : profile.ProfileId.Trim();

            profile.ProfileName = ProfileName(profile);

            if (profile.CreatedAt == default)
            {
                profile.CreatedAt = profile.UpdatedAt == default
                    ? DateTime.UtcNow
                    : profile.UpdatedAt;
            }

            if (profile.UpdatedAt == default)
                profile.UpdatedAt = profile.CreatedAt;
        }

        // Match by account but use character-specific matching for local profiles if we don't know the account name at the time for some reason
        private static bool IsSameCharacter(CharacterProfile profile, string accountName, string officialCharacterName)
        {
            if (profile == null || string.IsNullOrWhiteSpace(officialCharacterName))
                return false;

            if (!string.Equals(
                    profile.CharacterName?.Trim(),
                    officialCharacterName.Trim(),
                    StringComparison.OrdinalIgnoreCase))
                return false;

            if (string.IsNullOrWhiteSpace(accountName) || string.IsNullOrWhiteSpace(profile.AccountName))
                return true;

            return string.Equals(
                profile.AccountName.Trim(),
                accountName.Trim(),
                StringComparison.OrdinalIgnoreCase);
        }

        private static bool CanImport(CharacterProfile profile, string accountName, string currentCharacterName)
        {
            if (profile == null
                || string.IsNullOrWhiteSpace(profile.ProfileId)
                || string.IsNullOrWhiteSpace(profile.CharacterName)
                || string.IsNullOrWhiteSpace(profile.AccountName)
                || string.IsNullOrWhiteSpace(accountName)
                || string.IsNullOrWhiteSpace(currentCharacterName))
                return false;

            if (string.Equals(profile.CharacterName.Trim(), currentCharacterName.Trim(), StringComparison.OrdinalIgnoreCase))
                return false;

            if (!string.Equals(profile.AccountName.Trim(), accountName.Trim(), StringComparison.OrdinalIgnoreCase))
                return false;

            return profile.IsCharacterVerified;
        }

        private static void ApplyImportTarget(CharacterProfile profile, PlayerState targetState)
        {
            profile.CharacterName = targetState.OfficialCharacterName.Trim();
            profile.AccountName = targetState.AccountName.Trim();
            profile.Race = targetState.Race?.Trim() ?? string.Empty;
            profile.Profession = targetState.Profession?.Trim() ?? string.Empty;
            profile.Specialization = targetState.Specialization?.Trim() ?? string.Empty;
            profile.IsCharacterVerified = targetState.IsCharacterApiVerified;
        }

        private static string ProfileName(CharacterProfile profile)
        {
            return string.IsNullOrWhiteSpace(profile?.ProfileName)
                ? "Default"
                : profile.ProfileName.Trim();
        }

        // Use a clone instead of the actual file so we don't mutate something unintentionally
        private static CharacterProfile CloneProfile(CharacterProfile profile)
        {
            return profile == null
                ? null
                : JsonConvert.DeserializeObject<CharacterProfile>(JsonConvert.SerializeObject(profile));
        }

        // Prevent duplicate profile names from imports, so it appends (2), (3), etc. at the end of them as needed.
        private string GetImportName(string sourceName, PlayerState targetState)
        {
            var baseName = LimitProfileName(sourceName);
            var candidate = baseName;
            var suffix = 2;

            var existingNames = ListForCharacter(targetState.AccountName, targetState.OfficialCharacterName)
                .Select(ProfileName)
                .ToList();

            while (existingNames.Any(name => string.Equals(name, candidate, StringComparison.OrdinalIgnoreCase)))
            {
                var suffixText = $" ({suffix})";
                var maxBaseLength = Math.Max(1, ProfileLimits.MaxProfileNameLength - suffixText.Length);

                candidate = $"{LimitProfileName(baseName, maxBaseLength)}{suffixText}";
                suffix++;
            }

            return candidate;
        }

        private static string LimitProfileName(string value, int maxLength = ProfileLimits.MaxProfileNameLength)
        {
            var name = string.IsNullOrWhiteSpace(value) ? "Profile" : value.Trim();

            if (name.Length <= maxLength)
                return name;

            return name.Substring(0, maxLength).TrimEnd();
        }

        private static string GetActiveProfileKey(string accountName, string officialCharacterName)
        {
            if (string.IsNullOrWhiteSpace(accountName))
                return CharacterKey(officialCharacterName);

            return $"{NormalizeKey(accountName)}|{NormalizeKey(officialCharacterName)}";
        }

        private static string CharacterKey(string officialCharacterName)
        {
            return $"*|{NormalizeKey(officialCharacterName)}";
        }

        private static string NormalizeKey(string value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant();
        }
    }
}
