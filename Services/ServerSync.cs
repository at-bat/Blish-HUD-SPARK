using Blish_HUD;
using rp.spark.Models;
using rp.spark.Models.Api;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace rp.spark.Services
{
    public class ServerSync : IDisposable
    {
        private static readonly Logger Logger = Logger.GetLogger<ServerSync>();

        // Back off after failed sync attempts in case the server is down or GW2 API is having issues
        private static readonly TimeSpan FirstRetryDelay = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromMinutes(5);

        // Presence refreshes locally more often than it is published to reduce server load and for better UX (even if it's slightly false)
        // Any meaningful changes (updating profile, status, etc.) still publish immediately when they pass the sync checks though.
        private static readonly TimeSpan PresenceHeartbeatInterval = TimeSpan.FromSeconds(90);

        private readonly SparkClient _apiClient;
        private readonly SparkSettings _settings;
        private readonly PresenceLoop _presenceLoop;
        private readonly ProfileRepository _profileRepository;
        private readonly GW2TokenVerification _tokens;
        private readonly SemaphoreSlim _syncGate = new SemaphoreSlim(1, 1);
        private readonly object _syncWorkerLock = new object();
        private Func<CancellationToken, Task<bool>> _privacyReadyAsync;

        private CancellationTokenSource _cancellation;
        private Task _syncWorker;
        private bool _syncQueued;
        private bool _isStarted;
        private bool _isDisposed;
        private int _failureCount;
        private DateTime _nextSyncAttempt = DateTime.MinValue;
        private string _lastUploadedProfileId = string.Empty;
        private DateTime _lastProfileUpdatedAt = DateTime.MinValue;
        private string _lastRemovedPresenceKey = string.Empty;
        private bool _presencePublished;
        private PlayerPresence _lastPublishedPresence;

        public ServerSync(
            SparkClient apiClient,
            SparkSettings settings,
            PresenceLoop presenceLoop,
            ProfileRepository profileRepository,
            GW2TokenVerification tokens)
        {
            _apiClient = apiClient;
            _settings = settings;
            _presenceLoop = presenceLoop;
            _profileRepository = profileRepository;
            _tokens = tokens;
            CurrentStatus = ServerSyncStatus.Disconnected("Server sync is unavailable.");
        }

        public ServerSyncStatus CurrentStatus { get; private set; }

        public DateTime LastPublishedAt { get; private set; }

        public DateTime LastProfileUploadedAt { get; private set; }

        public event Action<ServerSyncStatus> StatusChanged;

        public void SetPrivacyCheck(Func<CancellationToken, Task<bool>> privacyReadyAsync)
        {
            _privacyReadyAsync = privacyReadyAsync;
        }

        public void Start()
        {
            if (_isStarted || _isDisposed)
                return;

            _isStarted = true;
            _cancellation = new CancellationTokenSource();

            _presenceLoop.PresenceUpdated += HandlePresenceUpdated;
            _profileRepository.ProfileSaved += HandleProfileSaved;
            _profileRepository.ActiveProfileChanged += HandleActiveProfileChanged;

            SetStatus(GetOfflineStatus());
            QueueSync();
        }

        public void Stop()
        {
            StopWorker();
        }

        private Task StopWorker()
        {
            if (!_isStarted)
                return TakeSyncWorker();

            _isStarted = false;

            _presenceLoop.PresenceUpdated -= HandlePresenceUpdated;
            _profileRepository.ProfileSaved -= HandleProfileSaved;
            _profileRepository.ActiveProfileChanged -= HandleActiveProfileChanged;

            var cancellation = _cancellation;
            _cancellation = null;
            var worker = TakeSyncWorker();
            _syncQueued = false;

            if (cancellation == null)
                return worker;

            cancellation.Cancel();
            DisposeCancellation(cancellation, worker);

            return worker;
        }

        public void RefreshConfig()
        {
            _failureCount = 0;
            _nextSyncAttempt = DateTime.MinValue;
            SetStatus(GetOfflineStatus());
            QueueSync();
        }

        public void SyncSoon()
        {
            _nextSyncAttempt = DateTime.MinValue;
            QueueSync();
        }

        public async Task<ProfileDownload> DownloadProfileAsync(
            PlayerPresence presence,
            CancellationToken cancellationToken = default,
            bool useOfflineMessage = false)
        {
            if (presence == null || string.IsNullOrWhiteSpace(presence.ActiveProfileId))
                return null;

            if (!_apiClient.IsConfigured)
            {
                SetStatus(GetOfflineStatus());
                return null;
            }

            var verificationToken = await GetVerificationTokenAsync(cancellationToken);
            var result = await _apiClient.DownloadProfileResultAsync(
                presence.AccountName,
                presence.OfficialCharacterName,
                presence.ActiveProfileId,
                verificationToken,
                cancellationToken);

            if (result.Succeeded)
            {
                Success("Profile downloaded.");
                return result.Value;
            }

            // Technically, this also could be if someone is blocked, but we don't inform the user this whatsoever and pretend they are offline on purpose
            if (IsProfileUnavailable(result) || useOfflineMessage)
            {
                if (useOfflineMessage)
                    SetInfoStatus("User not online, loading local copy.");

                return null;
            }

            Fail(result, "Could not download profile.");
            return null;
        }

        public async Task<IReadOnlyList<PlayerPresence>> GetOnlinePresenceAsync(
            ProfileRegion region,
            CancellationToken cancellationToken = default)
        {
            if (!_apiClient.IsConfigured)
            {
                SetStatus(GetOfflineStatus());
                return new List<PlayerPresence>();
            }

            var verificationToken = await GetVerificationTokenAsync(cancellationToken);
            var result = await _apiClient.ListPresenceResultAsync(region, verificationToken, cancellationToken);

            if (result.Succeeded)
            {
                Success("Online list updated.");
                return result.Value?.Entries ?? new List<PlayerPresence>();
            }

            Fail(result, "Could not refresh online list.");
            return new List<PlayerPresence>();
        }

        private void HandlePresenceUpdated(PlayerPresence presence)
        {
            QueueSync();
        }

        private void HandleProfileSaved(CharacterProfile profile)
        {
            SyncSoon();
        }

        private void HandleActiveProfileChanged(string accountName, string officialCharacterName, string profileId)
        {
            _lastUploadedProfileId = string.Empty;
            _lastProfileUpdatedAt = DateTime.MinValue;
            _lastRemovedPresenceKey = string.Empty;
            SyncSoon();
        }

        private void QueueSync()
        {
            if (!_isStarted || _isDisposed || _cancellation == null || _cancellation.IsCancellationRequested)
                return;

            lock (_syncWorkerLock)
            {
                if (!_isStarted || _isDisposed || _cancellation == null || _cancellation.IsCancellationRequested)
                    return;

                if (_syncWorker != null && !_syncWorker.IsCompleted)
                {
                    _syncQueued = true;
                    return;
                }

                _syncQueued = false;
                _syncWorker = RunQueueAsync(_cancellation.Token);
            }
        }

        private async Task RunQueueAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await SyncAsync(cancellationToken);

                lock (_syncWorkerLock)
                {
                    if (!_syncQueued)
                        return;

                    _syncQueued = false;
                }
            }
        }

        // Going invisible, disabling profile sharing, and swapping characters clears presence first
        // This prevents wrong info from populating to players
        private async Task SyncAsync(CancellationToken cancellationToken)
        {
            if (!await _syncGate.WaitAsync(0, cancellationToken))
                return;

            try
            {
                var presence = _presenceLoop.CurrentPresence;
                var removalPresence = FindPresenceToRemove(presence);

                if (removalPresence != null)
                {
                    if (DateTime.UtcNow < _nextSyncAttempt)
                        return;

                    await RemovePresenceAsync(removalPresence, cancellationToken);

                    if (presence == null || !presence.CanShare)
                        return;
                }

                if (!CanSync(presence))
                    return;

                if (DateTime.UtcNow < _nextSyncAttempt)
                    return;

                if (!await CheckPrivacyAsync(cancellationToken))
                    return;

                var profileNeedsUpload = NeedsProfileUpload(presence);

                if (!await UploadIfNeededAsync(presence, cancellationToken))
                    return;

                if (ShouldPublishPresence(presence, profileNeedsUpload))
                    await PublishPresenceAsync(presence, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Module was turned off in this case or restarting the sync, so this *should* be fine
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "SPARK server sync failed.");
                _failureCount++;
                _nextSyncAttempt = DateTime.UtcNow + GetRetryDelay(_failureCount);
                SetStatus(new ServerSyncStatus(
                    ServerSyncState.ApiUnavailable,
                    "Server sync failed. Retrying later.",
                    DateTime.UtcNow,
                    CurrentStatus?.LastSuccess ?? default));
            }
            finally
            {
                _syncGate.Release();
            }
        }

        private bool CanSync(PlayerPresence presence)
        {
            if (!_apiClient.IsConfigured)
            {
                SetStatus(GetOfflineStatus());
                return false;
            }

            if (presence == null)
            {
                SetStatus(ServerSyncStatus.Disconnected("Connecting, please wait."));
                return false;
            }

            if (!presence.CanShare)
            {
                SetStatus(ServerSyncStatus.Disconnected(presence.ShareBlockReason));
                return false;
            }

            return true;
        }

        // Player presence isn't published until the local block list has been pushed to the server
        // Prevents cases where the player shows online for a moment to someone they blocked before the server catches up on the block list
        private async Task<bool> CheckPrivacyAsync(CancellationToken cancellationToken)
        {
            if (_privacyReadyAsync == null)
                return true;

            var isReady = await _privacyReadyAsync(cancellationToken);

            if (!isReady)
                SetInfoStatus("Waiting for block list to sync with SPARK.");

            return isReady;
        }

        private async Task<bool> UploadIfNeededAsync(PlayerPresence presence, CancellationToken cancellationToken)
        {
            if (!NeedsProfileUpload(presence))
                return true;

            var profile = _profileRepository.Load(presence.ActiveProfileId);

            if (profile == null)
            {
                SetStatus(ServerSyncStatus.Disconnected("Active profile not detected. Please set a profile to be your active one."));
                return false;
            }

            var verificationToken = await GetVerificationTokenAsync(cancellationToken);
            var result = await _apiClient.UploadProfileResultAsync(profile, presence, verificationToken, cancellationToken);

            if (!result.Succeeded)
            {
                Fail(result, "Could not upload profile.");
                return false;
            }

            _lastUploadedProfileId = presence.ActiveProfileId?.Trim() ?? string.Empty;
            _lastProfileUpdatedAt = presence.ProfileUpdatedAtTime;
            LastProfileUploadedAt = DateTime.UtcNow;
            Success("Profile uploaded.");
            return true;
        }

        private async Task PublishPresenceAsync(PlayerPresence presence, CancellationToken cancellationToken)
        {
            var verificationToken = await GetVerificationTokenAsync(cancellationToken);
            var result = await _apiClient.PublishPresenceResultAsync(presence, verificationToken, cancellationToken);

            if (!result.Succeeded)
            {
                Fail(result, "Could not establish connection.");
                return;
            }

            _presencePublished = true;
            _lastPublishedPresence = PresenceMapper.ClonePresence(presence);
            _lastRemovedPresenceKey = string.Empty;
            LastPublishedAt = DateTime.UtcNow;
            Success("Connected to SPARK.");
        }

        // Clear the player's public online entries when needed (invisible, offline)
        private async Task RemovePresenceAsync(PlayerPresence presence, CancellationToken cancellationToken)
        {
            var removalPresence = PresenceMapper.CreateOfflinePresence(presence);
            var verificationToken = await GetVerificationTokenAsync(cancellationToken);
            var result = await _apiClient.PublishPresenceResultAsync(removalPresence, verificationToken, cancellationToken);

            if (!result.Succeeded)
            {
                Fail(result, "Could not remove presence.");
                return;
            }

            _presencePublished = false;
            _lastRemovedPresenceKey = removalPresence.Key();

            if (IsPresenceOwner(_lastPublishedPresence, removalPresence))
                _lastPublishedPresence = null;

            LastPublishedAt = DateTime.UtcNow;
            Success("Presence removed.");
        }

        private bool NeedsProfileUpload(PlayerPresence presence)
        {
            if (presence == null || !presence.HasActiveProfile)
                return false;

            var activeProfileId = presence.ActiveProfileId?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(activeProfileId))
                return false;

            return !string.Equals(activeProfileId, _lastUploadedProfileId, StringComparison.OrdinalIgnoreCase)
                || presence.ProfileUpdatedAtTime > _lastProfileUpdatedAt;
        }

        private bool ShouldPublishPresence(PlayerPresence presence, bool profileWasUploaded)
        {
            if (presence == null)
                return false;

            if (!_presencePublished || _lastPublishedPresence == null)
                return true;

            if (profileWasUploaded)
                return true;

            if (DateTime.UtcNow - LastPublishedAt >= PresenceHeartbeatInterval)
                return true;

            return PresenceChanged(_lastPublishedPresence, presence);
        }

        private static bool PresenceChanged(PlayerPresence previous, PlayerPresence current)
        {
            if (previous == null || current == null)
                return true;

            return !SameTrimmed(previous.AccountName, current.AccountName)
                || !SameTrimmed(previous.OfficialCharacterName, current.OfficialCharacterName)
                || !SameTrimmed(previous.DisplayCharacterName, current.DisplayCharacterName)
                || !SameTrimmed(previous.Race, current.Race)
                || !SameTrimmed(previous.Profession, current.Profession)
                || !SameTrimmed(previous.CustomProfession, current.CustomProfession)
                || !SameTrimmed(previous.ActiveProfileId, current.ActiveProfileId)
                || !SameTrimmed(previous.ActiveProfileName, current.ActiveProfileName)
                || previous.ProfileUpdatedAtTime != current.ProfileUpdatedAtTime
                || previous.Status != current.Status
                || !SameTrimmed(previous.StatusMessage, current.StatusMessage)
                || !SameTrimmed(previous.Currently, current.Currently)
                || !SameTrimmed(previous.OutOfCharacterInfo, current.OutOfCharacterInfo)
                || !SameTrimmed(previous.LocationName, current.LocationName)
                || previous.IsLocationHidden != current.IsLocationHidden
                || previous.Region != current.Region
                || previous.IsVerified != current.IsVerified
                || previous.IsInGame != current.IsInGame
                || previous.HasActiveProfile != current.HasActiveProfile
                || previous.ShareEnabled != current.ShareEnabled
                || previous.CanShare != current.CanShare
                || !SameTrimmed(previous.ShareBlockReason, current.ShareBlockReason);
        }

        private static bool SameTrimmed(string left, string right)
        {
            return string.Equals(
                left?.Trim() ?? string.Empty,
                right?.Trim() ?? string.Empty,
                StringComparison.Ordinal);
        }

        // When the player turns on invisible, turns off broadcasting, or switches characters, we need to remove this info from the server quickly
        // By the time they swap to a new character it should be fine and broadcast just their newest character so we aren't showing duplicates of a single account at once
        private PlayerPresence FindPresenceToRemove(PlayerPresence presence)
        {
            if (!_apiClient.IsConfigured)
                return null;

            if (presence?.CanShare == true)
            {
                if (_lastPublishedPresence != null && !IsPresenceOwner(_lastPublishedPresence, presence))
                    return _lastPublishedPresence;

                return null;
            }

            var removalPresence = _lastPublishedPresence;

            if (removalPresence == null && HasPresenceIdentity(presence))
                removalPresence = presence;

            if (!HasPresenceIdentity(removalPresence))
                return null;

            var key = removalPresence.Key();

            if (!_presencePublished
                && string.Equals(_lastRemovedPresenceKey, key, StringComparison.OrdinalIgnoreCase))
                return null;

            return removalPresence;
        }

        private async Task<string> GetVerificationTokenAsync(CancellationToken cancellationToken)
        {
            return _tokens == null
                ? string.Empty
                : await _tokens.GetTokenAsync(cancellationToken);
        }

        private static bool HasPresenceIdentity(PlayerPresence presence)
        {
            return !string.IsNullOrWhiteSpace(presence?.AccountName)
                && !string.IsNullOrWhiteSpace(presence?.OfficialCharacterName);
        }

        private static bool IsPresenceOwner(PlayerPresence left, PlayerPresence right)
        {
            return string.Equals(
                left?.Key() ?? string.Empty,
                right?.Key() ?? string.Empty,
                StringComparison.OrdinalIgnoreCase);
        }

        // Forbidden may mean the viewer was blocked by the profile owner.
        // Treat it like offline/unavailable so the UI can't reveal who you have been blocked by
        private static bool IsProfileUnavailable<T>(ApiResult<T> result)
        {
            return result?.StatusCode == HttpStatusCode.NotFound
                || result?.StatusCode == HttpStatusCode.Forbidden;
        }

        private void Fail<T>(ApiResult<T> result, string fallbackMessage)
        {
            _failureCount++;
            _nextSyncAttempt = DateTime.UtcNow + GetRetryDelay(_failureCount);

            var state = GetFailureState(result);
            var message = result?.StatusCode == HttpStatusCode.Forbidden
                && !string.IsNullOrWhiteSpace(result.ErrorMessage)
                    ? result.ErrorMessage
                    : fallbackMessage;

            SetStatus(new ServerSyncStatus(
                state,
                message,
                DateTime.UtcNow,
                CurrentStatus?.LastSuccess ?? default));
        }

        private static ServerSyncState GetFailureState<T>(ApiResult<T> result)
        {
            if (result?.FailureKind == ApiFailure.BlockedByWindows)
                return ServerSyncState.BlockedByWindows;

            if (result?.FailureKind == ApiFailure.NotConfigured)
                return ServerSyncState.Disconnected;

            if (result?.FailureKind == ApiFailure.Timeout
                || result?.FailureKind == ApiFailure.Network)
                return ServerSyncState.ApiUnavailable;

            if (result?.StatusCode >= HttpStatusCode.InternalServerError)
                return ServerSyncState.ServerError;

            return ServerSyncState.ApiUnavailable;
        }

        private void Success(string message)
        {
            _failureCount = 0;
            _nextSyncAttempt = DateTime.MinValue;

            SetStatus(new ServerSyncStatus(
                ServerSyncState.Connected,
                message,
                DateTime.UtcNow,
                DateTime.UtcNow));
        }

        private void SetInfoStatus(string message)
        {
            _failureCount = 0;
            _nextSyncAttempt = DateTime.MinValue;

            SetStatus(new ServerSyncStatus(
                ServerSyncState.Info,
                message,
                DateTime.UtcNow,
                CurrentStatus?.LastSuccess ?? default));
        }

        private ServerSyncStatus GetOfflineStatus()
        {
            if (!_apiClient.IsConfigured)
                return ServerSyncStatus.Disconnected("Server URL is invalid or unable to be found.");

            if (_settings?.BroadcastProfile?.Value != true)
                return ServerSyncStatus.Disconnected("Profile sharing is disabled.");

            return ServerSyncStatus.Disconnected("Waiting for Spark...");
        }

        private static TimeSpan GetRetryDelay(int failureCount)
        {
            var multiplier = Math.Max(1, Math.Min(8, failureCount));
            var delay = TimeSpan.FromTicks(FirstRetryDelay.Ticks * multiplier);

            return delay > MaxRetryDelay
                ? MaxRetryDelay
                : delay;
        }

        private void SetStatus(ServerSyncStatus status)
        {
            if (CurrentStatus != null
                && status != null
                && status.State != ServerSyncState.Connected
                && CurrentStatus.State == status.State
                && string.Equals(CurrentStatus.Message, status.Message, StringComparison.Ordinal))
                return;

            CurrentStatus = status ?? ServerSyncStatus.Disconnected("Unable to connect to SPARK.");
            StatusChanged?.Invoke(CurrentStatus);
        }

        private Task TakeSyncWorker()
        {
            lock (_syncWorkerLock)
            {
                var worker = _syncWorker;
                _syncWorker = null;
                _syncQueued = false;
                return worker;
            }
        }

        private static void DisposeCancellation(CancellationTokenSource cancellation, Task worker)
        {
            TaskCleanup.DisposeWhenComplete(worker, cancellation);
        }

        private void DisposeSyncGate(Task worker)
        {
            TaskCleanup.DisposeWhenComplete(worker, _syncGate);
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            var worker = StopWorker();
            DisposeSyncGate(worker);
        }
    }
}
