using Blish_HUD;
using rp.spark.Models;
using rp.spark.Models.Api;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace rp.spark.Services
{
    public class ProfileLoader
    {
        private static readonly Logger Logger = Logger.GetLogger<ProfileLoader>();

        private readonly ProfileRepository _profileRepository;
        private readonly PlayerStateService _playerState;
        private readonly PresenceService _presenceService;
        private readonly PresenceLoop _presenceLoop;
        private readonly ServerSync _serverSync;
        private readonly SparkSettings _settings;
        private readonly ProfileActions _profileActions;

        public ProfileLoader(
            ProfileRepository profileRepository,
            PlayerStateService playerState,
            PresenceService presenceService,
            PresenceLoop presenceLoop,
            ServerSync serverSync,
            SparkSettings settings,
            ProfileActions profileActions)
        {
            _profileRepository = profileRepository;
            _playerState = playerState;
            _presenceService = presenceService;
            _presenceLoop = presenceLoop;
            _serverSync = serverSync;
            _settings = settings;
            _profileActions = profileActions;
        }

        public ProfileViewData LoadMyProfile()
        {
            var state = _playerState.GetCached();

            var profile = _profileRepository.LoadActiveForCharacter(
                state.AccountName,
                state.OfficialCharacterName) ?? CreateEmptyProfile(state);

            return BuildLocal(profile, state);
        }

        public ProfileViewData BuildLocal(CharacterProfile profile, PlayerState state)
        {
            if (profile == null)
                profile = CreateEmptyProfile(state);

            if (state == null)
                state = _playerState.GetCached();

            if (string.IsNullOrWhiteSpace(profile.CharacterName) && !string.IsNullOrWhiteSpace(state.OfficialCharacterName))
                profile.CharacterName = state.OfficialCharacterName;

            if (!string.IsNullOrWhiteSpace(state.Race))
                profile.Race = state.Race;

            if (!string.IsNullOrWhiteSpace(state.Profession))
                profile.Profession = state.Profession;

            if (!string.IsNullOrWhiteSpace(state.Specialization))
                profile.Specialization = state.Specialization;

            profile.IsCharacterVerified = state.IsCharacterApiVerified;

            var presence = _presenceService.BuildPresence(state, profile);
            return new ProfileViewData(profile, presence);
        }

        // Adjusting online list to load your own presence without waiting for server.
        public async Task<IReadOnlyList<PlayerPresence>> LoadOnlineAsync(CancellationToken cancellationToken = default)
        {
            var rows = new List<PlayerPresence>();

            try
            {
                if (_serverSync != null)
                    rows.AddRange(await _serverSync.GetOnlinePresenceAsync(_settings.RegionFilter.Value, cancellationToken));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to load SPARK online profiles from the server.");
            }

            cancellationToken.ThrowIfCancellationRequested();

            foreach (var localPresence in await GetOwnPresenceRowsAsync(cancellationToken))
                UpdatePresence(rows, localPresence);

            return NormalizePresenceRows(rows);
        }

        public IReadOnlyList<PlayerPresence> LoadCachedOnlineRows()
        {
            return NormalizePresenceRows(GetOwnPresenceRows());
        }

        private IReadOnlyList<PlayerPresence> NormalizePresenceRows(IEnumerable<PlayerPresence> rows)
        {
            return (rows ?? Enumerable.Empty<PlayerPresence>())
                .Where(CanShowPresence)
                .GroupBy(presence => presence?.Key() ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();
        }

        public async Task<ProfileViewData> LoadOnlineProfileAsync(
            PlayerPresence presence,
            CancellationToken cancellationToken = default)
        {
            if (presence == null)
                return null;

            CharacterProfile profile = null;

            try
            {
                var livePresence = presence;
                var downloadedProfile = await _serverSync.DownloadProfileAsync(
                    presence,
                    cancellationToken,
                    useOfflineMessage: true);

                if (downloadedProfile != null)
                {
                    profile = downloadedProfile.ToCharacterProfile();
                    presence = PreferLivePresence(livePresence, downloadedProfile.Presence, profile);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return null;
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to download SPARK profile before opening viewer.");
            }

            if (profile == null && !string.IsNullOrWhiteSpace(presence.ActiveProfileId))
                profile = _profileRepository.Load(presence.ActiveProfileId);

            if (profile == null)
                profile = PresenceMapper.CreateFromPresence(presence);

            return new ProfileViewData(profile, presence);
        }

        public async Task<ProfileViewData> LoadSavedProfileAsync(
            SavedProfile record,
            CancellationToken cancellationToken = default)
        {
            if (record == null)
                return null;

            var presence = record.Presence ?? new PlayerPresence();
            var profile = record.Profile ?? PresenceMapper.CreateFromPresence(presence);
            var refreshedFromServer = false;
            PlayerPresence livePresence = null;

            PresenceMapper.FillMissingPresence(presence, profile);

            try
            {
                livePresence = await FindLivePresenceAsync(record, cancellationToken);
                var downloadedProfile = livePresence == null
                    ? null
                    : await _serverSync.DownloadProfileAsync(livePresence, cancellationToken);

                if (livePresence != null)
                {
                    presence = livePresence;
                    PresenceMapper.FillMissingPresence(presence, profile);
                }

                if (downloadedProfile != null)
                {
                    profile = downloadedProfile.ToCharacterProfile();
                    presence = PreferLivePresence(livePresence, downloadedProfile.Presence, profile);
                    _profileActions.SaveUpdatedProfile(profile, presence, record);
                    refreshedFromServer = true;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return null;
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to refresh cached SPARK profile before opening viewer.");
            }

            if (!refreshedFromServer && livePresence == null)
                MarkOffline(presence);

            return new ProfileViewData(profile, presence);
        }

        private async Task<IReadOnlyList<PlayerPresence>> GetOwnPresenceRowsAsync(CancellationToken cancellationToken)
        {
            try
            {
                var presence = _presenceLoop == null
                    ? await _presenceService.GetCurrentPresenceAsync(cancellationToken)
                    : await _presenceLoop.RefreshAsync(cancellationToken);

                return ToOwnPresenceRows(presence);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to refresh local SPARK presence for the online list.");
                return GetOwnPresenceRows();
            }
        }

        private IReadOnlyList<PlayerPresence> GetOwnPresenceRows()
        {
            try
            {
                var presence = _presenceService.GetCurrentPresence();

                if (CanShowPresence(presence))
                    return ToOwnPresenceRows(presence);
            }
            catch
            {
                // Fall back to the last presence.
            }

            return ToOwnPresenceRows(_presenceLoop?.CurrentPresence);
        }

        private IReadOnlyList<PlayerPresence> ToOwnPresenceRows(PlayerPresence presence)
        {
            if (!CanShowPresence(presence))
                return new List<PlayerPresence>();

            return new List<PlayerPresence> { presence };
        }

        private bool CanShowPresence(PlayerPresence presence)
        {
            return presence != null
                && presence.Status != RPStatus.Invisible
                && presence.CanShare
                && presence.HasActiveProfile
                && !_settings.IsBlockedAccount(presence.AccountName)
                && !string.IsNullOrWhiteSpace(presence.ActiveProfileId)
                && !string.IsNullOrWhiteSpace(presence.OfficialCharacterName);
        }

        private async Task<PlayerPresence> FindLivePresenceAsync(
            SavedProfile record,
            CancellationToken cancellationToken)
        {
            if (_serverSync == null || record == null)
                return null;

            var accountName = TextUtil.FirstNonEmpty(record.Presence?.AccountName, record.Profile?.AccountName);
            var characterName = TextUtil.FirstNonEmpty(record.Presence?.OfficialCharacterName, record.Profile?.CharacterName);
            var profileId = TextUtil.FirstNonEmpty(record.Presence?.ActiveProfileId, record.Profile?.ProfileId);

            if (string.IsNullOrWhiteSpace(accountName) || string.IsNullOrWhiteSpace(characterName))
                return null;

            var region = record.Presence?.Region ?? record.Profile?.Region ?? _settings.RegionFilter.Value;
            var onlineProfiles = await _serverSync.GetOnlinePresenceAsync(region, cancellationToken);

            return onlineProfiles.FirstOrDefault(presence =>
                IsSameProfilePresence(presence, accountName, characterName, profileId));
        }

        private static bool IsSameProfilePresence(
            PlayerPresence presence,
            string accountName,
            string characterName,
            string profileId)
        {
            if (presence == null)
                return false;

            if (!string.Equals(presence.AccountName?.Trim(), accountName?.Trim(), StringComparison.OrdinalIgnoreCase))
                return false;

            if (!string.Equals(presence.OfficialCharacterName?.Trim(), characterName?.Trim(), StringComparison.OrdinalIgnoreCase))
                return false;

            return string.IsNullOrWhiteSpace(profileId)
                || string.Equals(presence.ActiveProfileId?.Trim(), profileId.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private static void UpdatePresence(List<PlayerPresence> rows, PlayerPresence presence)
        {
            if (rows == null || presence == null)
                return;

            var key = presence.Key();
            var existingIndex = rows.FindIndex(row => string.Equals(row?.Key() ?? string.Empty, key, StringComparison.OrdinalIgnoreCase));

            if (existingIndex >= 0)
                rows[existingIndex] = presence;
            else
                rows.Add(presence);
        }

        private static PlayerPresence PreferLivePresence(
            PlayerPresence livePresence,
            PlayerPresence downloadedPresence,
            CharacterProfile profile)
        {
            var presence = livePresence ?? downloadedPresence ?? new PlayerPresence();

            PresenceMapper.FillPresence(presence, downloadedPresence);
            PresenceMapper.FillMissingPresence(presence, profile);

            return presence;
        }

        private static CharacterProfile CreateEmptyProfile(PlayerState state)
        {
            return new CharacterProfile
            {
                ProfileName = "No Active Profile",
                AccountName = state?.AccountName?.Trim() ?? string.Empty,
                CharacterName = state?.OfficialCharacterName?.Trim() ?? string.Empty,
                Race = state?.Race?.Trim() ?? string.Empty,
                Profession = state?.Profession?.Trim() ?? string.Empty,
                Specialization = state?.Specialization?.Trim() ?? string.Empty,
                IsCharacterVerified = state?.IsCharacterApiVerified ?? false,
                KnownFor = "Having no profile set as active.",
                Description = "Open the Profile Editor, create/choose a profile, then click on Set Active to begin!"
            };
        }

        private static void MarkOffline(PlayerPresence presence)
        {
            if (presence == null)
                return;

            presence.Status = RPStatus.Offline;
            presence.StatusMessage = string.Empty;
            presence.LocationName = "Unknown";
            presence.IsLocationHidden = false;
            presence.LastSeen = default;
        }
    }
}
