using Blish_HUD;
using Blish_HUD.Modules.Managers;
using Gw2Sharp.WebApi.V2.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace rp.spark.Services
{
    public class PlayerStateService
    {
        private static readonly Logger Logger = Logger.GetLogger<PlayerStateService>();
        private const string HiddenLocationName = "Hidden";
        private static readonly TimeSpan AccountRefreshInterval = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan CharacterRefreshInterval = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan ApiFailureRetryInterval = TimeSpan.FromMinutes(2);

        private readonly Gw2ApiManager _gw2ApiManager;
        private readonly SparkSettings _settings;
        private readonly Dictionary<int, string> _mapNameCache = new Dictionary<int, string>();
        private readonly Dictionary<int, DateTime> _mapRetryAfter = new Dictionary<int, DateTime>();
        private readonly Dictionary<string, CharacterApiSnapshot> _characterCache = new Dictionary<string, CharacterApiSnapshot>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DateTime> _characterRetryAfter = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private readonly object _lastStateLock = new object();
        private readonly object _accountCacheLock = new object();
        private PlayerState _lastState = new PlayerState();
        private string _cachedAccountName = string.Empty;
        private DateTime _accountFetchedAt = DateTime.MinValue;
        private DateTime _nextAccountLookup = DateTime.MinValue;

        public PlayerStateService(Gw2ApiManager gw2ApiManager, SparkSettings settings)
        {
            _gw2ApiManager = gw2ApiManager;
            _settings = settings;
        }

        public async Task<PlayerState> GetCurrentAsync(CancellationToken cancellationToken = default)
        {
            var state = ReadMumble();

            state.AccountName = await GetAccountNameAsync(cancellationToken);
            await TryVerifyCharacterAsync(state, cancellationToken);
            state.LocationName = IsLocationHidden()
                ? HiddenLocationName
                : await GetLocationNameAsync(state.MapId, cancellationToken);
            SetLastState(state);

            return state;
        }

        // Pull fresh game info as it is now so we can keep the UI responsive
        // Background will catch up after, this just feels better UX-wise and feels less slow/clunky
        public PlayerState GetCached()
        {
            var state = ReadMumble();
            var lastState = GetLastState();
            var sameCharacter = !string.IsNullOrWhiteSpace(state.OfficialCharacterName)
                             && string.Equals(
                                 state.OfficialCharacterName,
                                 lastState?.OfficialCharacterName,
                                 StringComparison.OrdinalIgnoreCase);

            if (lastState != null)
            {
                state.AccountName = lastState.AccountName;
                state.HasCharactersPermission = lastState.HasCharactersPermission;
                state.IsCharacterApiVerified = sameCharacter && lastState.IsCharacterApiVerified;

                if (sameCharacter)
                {
                    if (string.IsNullOrWhiteSpace(state.Race))
                        state.Race = lastState.Race;

                    if (string.IsNullOrWhiteSpace(state.Profession))
                        state.Profession = lastState.Profession;

                    if (string.IsNullOrWhiteSpace(state.Specialization))
                        state.Specialization = lastState.Specialization;
                }

                if (state.MapId > 0 && state.MapId == lastState.MapId)
                    state.LocationName = lastState.LocationName;
            }

            if (IsLocationHidden())
            {
                state.MapId = 0;
                state.LocationName = HiddenLocationName;
                return state;
            }

            if (string.IsNullOrWhiteSpace(state.LocationName))
                state.LocationName = GetLocationFallback(state.MapId);

            return state;
        }

        private PlayerState GetLastState()
        {
            lock (_lastStateLock)
            {
                return CloneState(_lastState);
            }
        }

        private void SetLastState(PlayerState state)
        {
            lock (_lastStateLock)
            {
                _lastState = CloneState(state) ?? new PlayerState();
            }
        }

        private bool TryGetCachedAccountName(out string accountName)
        {
            lock (_accountCacheLock)
            {
                if (IsFresh(_accountFetchedAt, AccountRefreshInterval)
                    || DateTime.UtcNow < _nextAccountLookup)
                {
                    accountName = _cachedAccountName;
                    return true;
                }
            }

            accountName = string.Empty;
            return false;
        }

        private void CacheAccountName(string accountName)
        {
            lock (_accountCacheLock)
            {
                _cachedAccountName = accountName ?? string.Empty;
                _accountFetchedAt = DateTime.UtcNow;
                _nextAccountLookup = DateTime.MinValue;
            }
        }

        private void SetAccountRetry()
        {
            lock (_accountCacheLock)
            {
                _nextAccountLookup = DateTime.UtcNow + ApiFailureRetryInterval;
            }
        }

        private PlayerState ReadMumble()
        {
            var state = new PlayerState
            {
                IsMumbleAvailable = GameService.Gw2Mumble.IsAvailable,
                IsInGame = GameService.GameIntegration.Gw2Instance.IsInGame
            };

            if (!state.IsMumbleAvailable || !state.IsInGame)
                return state;

            state.OfficialCharacterName = GameService.Gw2Mumble.PlayerCharacter.Name?.Trim() ?? string.Empty;
            state.Race = FormatGameValue(GameService.Gw2Mumble.PlayerCharacter.Race);
            state.Profession = FormatGameValue(GameService.Gw2Mumble.PlayerCharacter.Profession);
            state.Specialization = FormatGameValue(GameService.Gw2Mumble.PlayerCharacter.Specialization);
            if (IsLocationHidden())
            {
                state.LocationName = HiddenLocationName;
            }
            else
            {
                state.MapId = GameService.Gw2Mumble.CurrentMap.Id;
            }

            return state;
        }

        private async Task<string> GetAccountNameAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (!HasApiPermissions(TokenPermission.Account))
                {
                    Logger.Debug("Skipping this, account API permission is unavailable.");
                    return string.Empty;
                }

                if (TryGetCachedAccountName(out var cachedAccountName))
                    return cachedAccountName;

                var account = await _gw2ApiManager.Gw2ApiClient.V2.Account.GetAsync(cancellationToken);
                var accountName = account?.Name?.Trim() ?? string.Empty;
                CacheAccountName(accountName);

                return accountName;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                SetAccountRetry();
                BlishWarnings.HttpBlocked(ex, "load your GW2 account name");
                Logger.Warn(ex, "Failed to load account name from the GW2 API.");
                return string.Empty;
            }
        }

        // Fix for new presence cache so location doesn't cache bad map names (Map 139 instead of Rata Sum, etc.)
        private async Task<string> GetLocationNameAsync(int mapId, CancellationToken cancellationToken)
        {
            if (mapId <= 0)
                return GetLocationFallback(mapId);

            lock (_mapNameCache)
            {
                if (_mapNameCache.TryGetValue(mapId, out var cachedName))
                    return cachedName;
            }

            try
            {
                if (IsMapRetryCoolingDown(mapId))
                    return GetLocationFallback(mapId);

                var map = await GameService.Gw2WebApi.AnonymousConnection.Client.V2.Maps.GetAsync(mapId, cancellationToken);
                var mapName = map?.Name?.Trim() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(mapName))
                {
                    SetMapRetry(mapId);
                    return GetLocationFallback(mapId);
                }

                lock (_mapNameCache)
                {
                    _mapNameCache[mapId] = mapName;
                }

                ClearMapRetry(mapId);
                return mapName;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                SetMapRetry(mapId);
                BlishWarnings.HttpBlocked(ex, "resolve your current GW2 map");
                Logger.Warn(ex, "Failed to resolve GW2 map id {mapId}.", mapId);
                return GetLocationFallback(mapId);
            }
        }

        private static string GetLocationFallback(int mapId)
        {
            return mapId > 0
                ? $"Map {mapId}"
                : "Unknown";
        }

        // Character MUST be owned by the account and match
        private async Task TryVerifyCharacterAsync(PlayerState state, CancellationToken cancellationToken)
        {
            state.HasCharactersPermission = HasApiPermissions(TokenPermission.Account, TokenPermission.Characters);

            if (!state.CanEditProfile || !state.HasCharactersPermission)
            {
                if (state.CanEditProfile)
                    Logger.Debug("Skipping, characters API permission is unavailable.");

                return;
            }

            try
            {
                if (TryApplyCachedCharacter(state))
                    return;

                if (IsCharacterRetryCoolingDown(state.OfficialCharacterName))
                    return;

                var character = await _gw2ApiManager.Gw2ApiClient.V2.Characters.GetAsync(
                    state.OfficialCharacterName,
                    cancellationToken);

                if (character == null)
                    return;

                state.IsCharacterApiVerified = true;

                if (!string.IsNullOrWhiteSpace(character.Race))
                    state.Race = character.Race.Trim();

                if (!string.IsNullOrWhiteSpace(character.Profession))
                    state.Profession = character.Profession.Trim();

                CacheCharacter(state);
                ClearCharacterRetry(state.OfficialCharacterName);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                SetCharacterRetry(state.OfficialCharacterName);
                BlishWarnings.HttpBlocked(ex, "verify your GW2 character");
                Logger.Warn(ex, "Failed to verify character {characterName} with the GW2 API.", state.OfficialCharacterName);
                state.IsCharacterApiVerified = false;
            }
        }

        private bool TryApplyCachedCharacter(PlayerState state)
        {
            if (state == null || string.IsNullOrWhiteSpace(state.OfficialCharacterName))
                return false;

            lock (_characterCache)
            {
                if (!_characterCache.TryGetValue(state.OfficialCharacterName.Trim(), out var cached)
                    || !IsFresh(cached.FetchedAt, CharacterRefreshInterval))
                    return false;

                state.IsCharacterApiVerified = cached.IsVerified;

                if (!string.IsNullOrWhiteSpace(cached.Race))
                    state.Race = cached.Race;

                if (!string.IsNullOrWhiteSpace(cached.Profession))
                    state.Profession = cached.Profession;

                return true;
            }
        }

        private void CacheCharacter(PlayerState state)
        {
            if (state == null || string.IsNullOrWhiteSpace(state.OfficialCharacterName))
                return;

            lock (_characterCache)
            {
                _characterCache[state.OfficialCharacterName.Trim()] = new CharacterApiSnapshot
                {
                    Race = state.Race?.Trim() ?? string.Empty,
                    Profession = state.Profession?.Trim() ?? string.Empty,
                    IsVerified = state.IsCharacterApiVerified,
                    FetchedAt = DateTime.UtcNow
                };
            }
        }

        private bool IsCharacterRetryCoolingDown(string characterName)
        {
            if (string.IsNullOrWhiteSpace(characterName))
                return false;

            lock (_characterRetryAfter)
            {
                return _characterRetryAfter.TryGetValue(characterName.Trim(), out var retryAfter)
                    && DateTime.UtcNow < retryAfter;
            }
        }

        private void SetCharacterRetry(string characterName)
        {
            if (string.IsNullOrWhiteSpace(characterName))
                return;

            lock (_characterRetryAfter)
            {
                _characterRetryAfter[characterName.Trim()] = DateTime.UtcNow + ApiFailureRetryInterval;
            }
        }

        private void ClearCharacterRetry(string characterName)
        {
            if (string.IsNullOrWhiteSpace(characterName))
                return;

            lock (_characterRetryAfter)
            {
                _characterRetryAfter.Remove(characterName.Trim());
            }
        }

        private bool IsMapRetryCoolingDown(int mapId)
        {
            lock (_mapRetryAfter)
            {
                return _mapRetryAfter.TryGetValue(mapId, out var retryAfter)
                    && DateTime.UtcNow < retryAfter;
            }
        }

        private void SetMapRetry(int mapId)
        {
            lock (_mapRetryAfter)
            {
                _mapRetryAfter[mapId] = DateTime.UtcNow + ApiFailureRetryInterval;
            }
        }

        private void ClearMapRetry(int mapId)
        {
            lock (_mapRetryAfter)
            {
                _mapRetryAfter.Remove(mapId);
            }
        }

        private bool HasApiPermissions(params TokenPermission[] permissions)
        {
            return _gw2ApiManager != null
                && _gw2ApiManager.HasSubtoken
                && _gw2ApiManager.HasPermissions(permissions);
        }

        private bool IsLocationHidden()
        {
            return _settings?.HideLocation?.Value ?? false;
        }

        private static string FormatGameValue(object value)
        {
            var text = value?.ToString() ?? string.Empty;

            return string.Equals(text, "None", StringComparison.OrdinalIgnoreCase)
                || string.Equals(text, "Unknown", StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : text;
        }

        private static PlayerState CloneState(PlayerState state)
        {
            if (state == null)
                return null;

            return new PlayerState
            {
                IsMumbleAvailable = state.IsMumbleAvailable,
                IsInGame = state.IsInGame,
                OfficialCharacterName = state.OfficialCharacterName,
                Race = state.Race,
                Profession = state.Profession,
                Specialization = state.Specialization,
                MapId = state.MapId,
                LocationName = state.LocationName,
                AccountName = state.AccountName,
                IsCharacterApiVerified = state.IsCharacterApiVerified,
                HasCharactersPermission = state.HasCharactersPermission
            };
        }

        private static bool IsFresh(DateTime fetchedAt, TimeSpan interval)
        {
            return fetchedAt != default && DateTime.UtcNow - fetchedAt < interval;
        }

        private class CharacterApiSnapshot
        {
            public string Race { get; set; } = string.Empty;

            public string Profession { get; set; } = string.Empty;

            public bool IsVerified { get; set; }

            public DateTime FetchedAt { get; set; }
        }
    }
}
