using rp.spark.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace rp.spark.Services
{
    public class ProfileEditorSession
    {
        private readonly ProfileRepository _profiles;
        private readonly PlayerStateService _playerState;
        private List<CharacterProfile> _availableProfiles = new List<CharacterProfile>();
        private List<ProfileImportGroup> _importGroups = new List<ProfileImportGroup>();

        public CharacterProfile Profile { get; private set; }
        public PlayerState State { get; private set; }
        public bool HasImportState =>
            State.CanEditProfile
            && !string.IsNullOrWhiteSpace(State.AccountName)
            && State.IsCharacterApiVerified;
        public AtAGlanceEntry[] Glance { get; }
        public IReadOnlyList<CharacterProfile> Profiles => _availableProfiles;
        public IReadOnlyList<ProfileImportGroup> ImportGroups => _importGroups;
        public string ActiveProfileId => _profiles.GetActiveProfileId(State.AccountName, State.OfficialCharacterName);

        public bool IsSelectedProfileActive =>
            Profile != null
            && !string.IsNullOrWhiteSpace(ActiveProfileId)
            && string.Equals(Profile.ProfileId, ActiveProfileId, StringComparison.OrdinalIgnoreCase);

        public string StatusText { get; private set; } = string.Empty;

        public event Action<string> StatusChanged;
        public event Action ProfileChanged;
        public event Action ImportsChanged;

        public ProfileEditorSession(ProfileRepository profiles, PlayerStateService playerState, PlayerState initialState)
        {
            _profiles = profiles;
            _playerState = playerState;
            State = initialState ?? new PlayerState();
            Glance = new AtAGlanceEntry[ProfileLimits.MaxAtAGlanceEntries];

            RefreshProfiles(preferActive: true);
        }

        public void SelectProfile(string profileId)
        {
            if (string.IsNullOrWhiteSpace(profileId))
                return;

            var selectedProfile = _availableProfiles.FirstOrDefault(profile =>
                string.Equals(profile.ProfileId, profileId, StringComparison.OrdinalIgnoreCase));

            if (selectedProfile == null)
                return;

            Profile = selectedProfile;
            LoadGlanceDraft();
            SetStatus($"Editing {GetProfileName(Profile)}.");
            ProfileChanged?.Invoke();
        }

        public void CreateProfile()
        {
            EnsureEditable();

            var profile = CreateBlankProfile(GetUniqueProfileName("New Profile"));
            _profiles.Save(profile);
            RefreshProfiles(profile.ProfileId);
            SetStatus("New profile created. Rename it and save when ready.");
        }

        public void DuplicateProfile()
        {
            EnsureEditable();
            ApplyGlance();

            var duplicateName = GetUniqueProfileName($"{GetProfileName(Profile)} Copy");
            var duplicate = _profiles.Duplicate(Profile, duplicateName);

            RefreshProfiles(duplicate.ProfileId);
            SetStatus("Profile duplicated.");
        }

        public async Task<bool> ImportAsync(string profileId)
        {
            if (string.IsNullOrWhiteSpace(profileId))
            {
                SetStatus("Choose a profile to import.");
                return false;
            }

            SetStatus("Importing profile...");

            var state = await _playerState.GetCurrentAsync();

            if (!state.CanEditProfile)
            {
                SetStatus("Character info unavailable.");
                return false;
            }

            var source = _profiles.Load(profileId);

            if (source == null)
            {
                RefreshProfiles(Profile?.ProfileId);
                SetStatus("Profile not found.");
                return false;
            }

            try
            {
                var imported = _profiles.Import(source, state);
                RefreshProfiles(imported.ProfileId);
                SetStatus($"Imported {GetProfileName(imported)}.");
                return true;
            }
            catch
            {
                RefreshImportGroups();
                SetStatus("Import failed.");
                return false;
            }
        }

        public void DeleteProfile()
        {
            EnsureEditable();

            if (Profile == null)
                return;

            var deletedProfileName = GetProfileName(Profile);

            _profiles.Delete(Profile.ProfileId);
            RefreshProfiles(preferActive: true);
            SetStatus($"Deleted {deletedProfileName}.");
        }

        public async Task SetActiveAsync()
        {
            if (await SaveAsync(false))
            {
                _profiles.SetActiveProfile(Profile.AccountName, Profile.CharacterName, Profile.ProfileId);
                RefreshProfiles(Profile.ProfileId);
                SetStatus($"{GetProfileName(Profile)} is now active.");
            }
        }

        public async Task<bool> SaveAsync()
        {
            return await SaveAsync(true);
        }

        public async Task<bool> SaveAsync(bool clearStatusAfterSave)
        {
            SetStatus("Checking current character...");

            var state = await _playerState.GetCurrentAsync();

            if (!state.CanEditProfile)
            {
                SetStatus("Cannot save until Mumble Link detects your current character.");
                return false;
            }

            if (!string.IsNullOrWhiteSpace(Profile.CharacterName)
                && !string.Equals(Profile.CharacterName.Trim(), state.OfficialCharacterName.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                SetStatus("Character changed. Reopen the editor before saving.");
                return false;
            }

            ApplyStateToProfile(state);
            ApplyGlance();

            try
            {
                _profiles.Save(Profile);
                RefreshProfiles(Profile.ProfileId);
                SetStatus(string.IsNullOrWhiteSpace(Profile.AccountName)
                    ? "Profile saved. API account unavailable."
                    : "Profile saved!");

                if (clearStatusAfterSave)
                {
                    await Task.Delay(1000);
                    SetStatus(string.Empty);
                }

                return true;
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message);
                return false;
            }
        }

        public string GetHeaderText()
        {
            var characterName = string.IsNullOrWhiteSpace(State.OfficialCharacterName)
                ? "Unknown character"
                : State.OfficialCharacterName.Trim();
            var accountName = string.IsNullOrWhiteSpace(State.AccountName)
                ? "API account unavailable"
                : State.AccountName.Trim();
            var locationName = string.IsNullOrWhiteSpace(State.LocationName)
                ? "Unknown location"
                : State.LocationName.Trim();
            var characterDetails = GetCharacterDetailsText(State);
            var verification = State.IsCharacterApiVerified
                ? "API character verified"
                : State.HasCharactersPermission
                    ? "API character not verified yet"
                    : "Characters API unavailable";

            return $"Editing profile for {characterName} | {characterDetails} | {accountName} | {verification} | {locationName}";
        }

        public void SetStatus(string statusText)
        {
            StatusText = statusText ?? string.Empty;
            StatusChanged?.Invoke(StatusText);
        }

        private void RefreshProfiles(string selectedProfileId = null, bool preferActive = false)
        {
            _availableProfiles = State.CanEditProfile
                ? _profiles.ListForCharacter(State.AccountName, State.OfficialCharacterName).ToList()
                : new List<CharacterProfile>();

            RefreshImportGroups();

            CharacterProfile selectedProfile = null;

            if (!string.IsNullOrWhiteSpace(selectedProfileId))
            {
                selectedProfile = _availableProfiles.FirstOrDefault(profile =>
                    string.Equals(profile.ProfileId, selectedProfileId, StringComparison.OrdinalIgnoreCase));
            }

            if (selectedProfile == null && preferActive)
            {
                var activeProfileId = ActiveProfileId;

                if (!string.IsNullOrWhiteSpace(activeProfileId))
                {
                    selectedProfile = _availableProfiles.FirstOrDefault(profile =>
                        string.Equals(profile.ProfileId, activeProfileId, StringComparison.OrdinalIgnoreCase));
                }
            }

            if (selectedProfile == null)
                selectedProfile = _availableProfiles.OrderByDescending(profile => profile.UpdatedAt).FirstOrDefault();

            Profile = selectedProfile ?? CreateBlankProfile("Default");
            LoadGlanceDraft();
            ProfileChanged?.Invoke();
        }

        private void RefreshImportGroups()
        {
            _importGroups = State.CanEditProfile
                ? _profiles.ListImports(State.AccountName, State.OfficialCharacterName).ToList()
                : new List<ProfileImportGroup>();
        }

        public async Task<bool> RefreshImportsAsync()
        {
            var state = await _playerState.GetCurrentAsync();

            if (!state.CanEditProfile)
                return false;

            State = state;
            RefreshImportGroups();
            ImportsChanged?.Invoke();

            return HasImportState;
        }

        private CharacterProfile CreateBlankProfile(string profileName)
        {
            var profile = new CharacterProfile
            {
                ProfileName = LimitProfileName(string.IsNullOrWhiteSpace(profileName) ? "Default" : profileName.Trim())
            };

            if (State.CanEditProfile)
                ApplyStateToProfile(State, profile);

            return profile;
        }

        private void ApplyStateToProfile(PlayerState state)
        {
            ApplyStateToProfile(state, Profile);
        }

        private static void ApplyStateToProfile(PlayerState state, CharacterProfile profile)
        {
            if (state == null || profile == null || !state.CanEditProfile)
                return;

            profile.CharacterName = state.OfficialCharacterName.Trim();

            if (!string.IsNullOrWhiteSpace(state.AccountName))
                profile.AccountName = state.AccountName.Trim();

            profile.Race = state.Race?.Trim() ?? string.Empty;
            profile.Profession = state.Profession?.Trim() ?? string.Empty;
            profile.Specialization = state.Specialization?.Trim() ?? string.Empty;
            profile.IsCharacterVerified = state.IsCharacterApiVerified;
        }

        private void LoadGlanceDraft()
        {
            if (Profile.AtAGlance == null)
                Profile.AtAGlance = new List<AtAGlanceEntry>();

            for (var i = 0; i < Glance.Length; i++)
            {
                var source = Profile.AtAGlance.Count > i ? Profile.AtAGlance[i] : null;

                Glance[i] = new AtAGlanceEntry
                {
                    AssetId = source?.AssetId ?? 0,
                    Title = source?.Title ?? string.Empty,
                    Description = string.IsNullOrWhiteSpace(source?.Description)
                        ? source?.Tooltip ?? string.Empty
                        : source.Description,
                    Tooltip = string.Empty
                };
            }
        }

        private void ApplyGlance()
        {
            if (Profile.AtAGlance == null)
                Profile.AtAGlance = new List<AtAGlanceEntry>();

            Profile.AtAGlance.Clear();

            foreach (var entry in Glance)
            {
                if (entry.AssetId <= 0)
                    continue;

                Profile.AtAGlance.Add(new AtAGlanceEntry
                {
                    AssetId = entry.AssetId,
                    Title = entry.Title?.Trim() ?? string.Empty,
                    Description = entry.Description?.Trim() ?? string.Empty,
                    Tooltip = string.Empty
                });
            }
        }

        private string GetUniqueProfileName(string baseName)
        {
            var cleanBaseName = LimitProfileName(string.IsNullOrWhiteSpace(baseName) ? "Profile" : baseName.Trim());
            var candidate = cleanBaseName;
            var suffix = 2;

            while (_availableProfiles.Any(profile =>
                string.Equals(GetProfileName(profile), candidate, StringComparison.OrdinalIgnoreCase)))
            {
                var suffixText = $" {suffix}";
                var maxBaseLength = Math.Max(1, ProfileLimits.MaxProfileNameLength - suffixText.Length);
                candidate = $"{LimitProfileName(cleanBaseName, maxBaseLength)}{suffixText}";
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

        private void EnsureEditable()
        {
            if (!State.CanEditProfile)
                throw new InvalidOperationException("Cannot manage profiles until Mumble Link detects your current character.");
        }

        private static string GetProfileName(CharacterProfile profile)
        {
            return string.IsNullOrWhiteSpace(profile?.ProfileName)
                ? "Default"
                : profile.ProfileName.Trim();
        }

        private static string GetCharacterDetailsText(PlayerState state)
        {
            var race = state.Race?.Trim() ?? string.Empty;
            var profession = state.Profession?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(race) && string.IsNullOrWhiteSpace(profession))
                return "Unknown race/profession";

            if (string.IsNullOrWhiteSpace(race))
                return profession;

            if (string.IsNullOrWhiteSpace(profession))
                return race;

            return $"{race} {profession}";
        }
    }
}
