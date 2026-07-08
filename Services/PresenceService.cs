using rp.spark.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace rp.spark.Services
{
    public class PresenceService
    {
        private const string HiddenLocationName = "Hidden";
        private const string UnknownLocationName = "Unknown";

        private readonly ProfileRepository _profileRepository;
        private readonly PlayerStateService _playerState;
        private readonly SparkSettings _settings;

        public PresenceService(
            ProfileRepository profileRepository,
            PlayerStateService playerState,
            SparkSettings settings)
        {
            _profileRepository = profileRepository;
            _playerState = playerState;
            _settings = settings;
        }

        public PlayerPresence GetCurrentPresence()
        {
            var state = _playerState.GetCached();
            var activeProfile = _profileRepository.LoadActiveForCharacter(
                state.AccountName,
                state.OfficialCharacterName);

            return BuildPresence(state, activeProfile);
        }

        public async Task<PlayerPresence> GetCurrentPresenceAsync(CancellationToken cancellationToken = default)
        {
            var state = await _playerState.GetCurrentAsync(cancellationToken);
            var activeProfile = _profileRepository.LoadActiveForCharacter(
                state.AccountName,
                state.OfficialCharacterName);

            return BuildPresence(state, activeProfile);
        }

        public PlayerPresence BuildPresence(PlayerState state, CharacterProfile activeProfile)
        {
            state = state ?? _playerState.GetCached();

            var accountName = TextUtil.FirstNonEmpty(state.AccountName, activeProfile?.AccountName);
            var officialCharacterName = TextUtil.FirstNonEmpty(state.OfficialCharacterName, activeProfile?.CharacterName);
            var activeProfileId = _profileRepository.GetActiveProfileId(accountName, officialCharacterName);
            var hasActiveProfile = activeProfile != null
                                && !string.IsNullOrWhiteSpace(activeProfileId)
                                && string.Equals(activeProfile.ProfileId, activeProfileId, StringComparison.OrdinalIgnoreCase);
            var status = NormalizeStatus(_settings?.CurrentStatus?.Value ?? RPStatus.Online);
            var broadcastEnabled = _settings?.BroadcastProfile?.Value ?? false;
            var locationHidden = _settings?.HideLocation?.Value ?? false;
            var locationResolved = locationHidden || state.IsLocationResolved;
            var isInGame = state.CanEditProfile;

            var snapshot = new PlayerPresence
            {
                AccountName = accountName,
                OfficialCharacterName = officialCharacterName,
                DisplayCharacterName = activeProfile?.DisplayName?.Trim() ?? string.Empty,
                Race = TextUtil.FirstNonEmpty(state.Race, activeProfile?.Race),
                Profession = TextUtil.FirstNonEmpty(state.Profession, activeProfile?.Profession),
                CustomProfession = activeProfile?.CustomProfession?.Trim() ?? string.Empty,
                ActiveProfileId = hasActiveProfile ? activeProfile.ProfileId : string.Empty,
                ActiveProfileName = hasActiveProfile ? activeProfile.ProfileName?.Trim() ?? string.Empty : string.Empty,
                IsMature = hasActiveProfile && activeProfile.IsMature,
                ProfileUpdatedAtTime = hasActiveProfile ? activeProfile.UpdatedAt : default,
                Status = status,
                Currently = hasActiveProfile ? activeProfile.Currently?.Trim() ?? string.Empty : string.Empty,
                OutOfCharacterInfo = hasActiveProfile ? activeProfile.OutOfCharacterInfo?.Trim() ?? string.Empty : string.Empty,
                LocationName = GetLocationName(state, locationHidden, locationResolved),
                IsLocationHidden = locationHidden,
                Region = _settings?.RegionFilter?.Value ?? ProfileRegion.NA,
                IsVerified = state.IsCharacterApiVerified,
                IsInGame = isInGame,
                HasActiveProfile = hasActiveProfile,
                ShareEnabled = broadcastEnabled && isInGame,
                LastSeen = DateTime.UtcNow
            };

            snapshot.CanShare = CanShare(snapshot);
            snapshot.ShareBlockReason = GetShareBlockReason(snapshot);

            return snapshot;
        }

        private static bool CanShare(PlayerPresence snapshot)
        {
            return snapshot.ShareEnabled
                && snapshot.Status != RPStatus.Invisible
                && snapshot.HasActiveProfile
                && !string.IsNullOrWhiteSpace(snapshot.AccountName)
                && !string.IsNullOrWhiteSpace(snapshot.OfficialCharacterName);
        }

        private static string GetShareBlockReason(PlayerPresence snapshot)
        {
            if (!snapshot.ShareEnabled)
                return snapshot.IsInGame ? "Profile sharing is disabled." : "No character detected.";

            if (snapshot.Status == RPStatus.Invisible)
                return "Status is invisible.";

            if (!snapshot.HasActiveProfile)
                return "No active profile selected.";

            if (string.IsNullOrWhiteSpace(snapshot.AccountName))
                return "Account name unavailable.";

            if (string.IsNullOrWhiteSpace(snapshot.OfficialCharacterName))
                return "Character name unavailable.";

            return string.Empty;
        }

        private static string GetLocationName(PlayerState state, bool locationHidden, bool locationResolved)
        {
            if (locationHidden)
                return HiddenLocationName;

            if (!locationResolved)
                return UnknownLocationName;

            return string.IsNullOrWhiteSpace(state.LocationName)
                ? UnknownLocationName
                : state.LocationName.Trim();
        }

        private static RPStatus NormalizeStatus(RPStatus status)
        {
            return status == RPStatus.Offline
                ? RPStatus.Online
                : status;
        }
    }
}
