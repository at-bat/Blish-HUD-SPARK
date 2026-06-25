using Blish_HUD;
using rp.spark.Models;
using rp.spark.Models.Api;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace rp.spark.Services
{
    public sealed class ProfileActions : IDisposable
    {
        private static readonly Logger Logger = Logger.GetLogger<ProfileActions>();
        private const string BlockRemovalHelp = "You can remove blocks using the 'Manage Blocks' in the settings menu.";
        private const string ReportUnavailableMessage = "Report failed. Profile not found on SPARK server. Profiles only exist for 24h on the server at a time.";

        private readonly ProfileCache _profileCache;
        private readonly SparkSettings _settings;
        private readonly PlayerStateService _playerState;
        private readonly SparkClient _apiClient;
        private readonly GW2TokenVerification _tokens;
        private readonly ServerSync _serverSync;
        private readonly object _blockSyncLock = new object();
        private readonly SemaphoreSlim _fullBlockSyncGate = new SemaphoreSlim(1, 1);

        private CancellationTokenSource _blockSyncCancellation;
        private Task _blockSyncWorker;
        private bool _needsFullBlockSync;
        private bool _blocklistSynced;
        private string _lastSyncedBlocklistKey = string.Empty;
        private bool _isStarted;
        private bool _isDisposed;

        public ProfileActions(
            ProfileCache profileCache,
            SparkSettings settings,
            PlayerStateService playerState,
            SparkClient apiClient,
            GW2TokenVerification tokens,
            ServerSync serverSync)
        {
            _profileCache = profileCache;
            _settings = settings;
            _playerState = playerState;
            _apiClient = apiClient;
            _tokens = tokens;
            _serverSync = serverSync;
        }

        public event Action SavedProfilesChanged;

        public event Action BlockedAccountsChanged;

        public void Start()
        {
            if (_isStarted || _isDisposed)
                return;

            _isStarted = true;
            _blockSyncCancellation = new CancellationTokenSource();

            if (_serverSync != null)
                _serverSync.StatusChanged += HandleServerSyncStatusChanged;

            SyncBlocks();
        }

        public void SyncBlocks()
        {
            if (_isDisposed)
                return;

            lock (_blockSyncLock)
            {
                _needsFullBlockSync = true;
            }

            StartBlockSyncWorker();
        }

        public async Task<bool> EnsureBlocksSyncedAsync(CancellationToken cancellationToken)
        {
            if (_isDisposed)
                return false;

            var accountNames = LocalBlocks();

            if (accountNames.Count == 0)
                return true;

            var blocklistKey = GetBlocklistKey(accountNames);

            if (IsBlocklistSynced(blocklistKey))
                return true;

            var succeeded = await PushBlocksAsync(accountNames, blocklistKey, cancellationToken);

            if (!succeeded)
            {
                SyncBlocks();
                return false;
            }

            var currentBlocklistKey = GetBlocklistKey(LocalBlocks());

            if (IsBlocklistSynced(currentBlocklistKey))
                return true;

            SyncBlocks();
            return false;
        }

        public string ToggleProfileBookmark(CharacterProfile profile, PlayerPresence presence)
        {
            try
            {
                if (IsProfileBookmarked(profile, presence))
                {
                    var cacheKey = ProfileCache.GetProfileCacheKey(profile, presence);
                    _profileCache.RemoveBookmark(cacheKey);
                    NotifySavedChanged();
                    return "Bookmark removed.";
                }

                _profileCache.Bookmark(profile, presence);
                NotifySavedChanged();
                return "Profile bookmarked locally.";
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to update SPARK profile bookmark.");
                return "Couldn't update this bookmark.";
            }
        }

        public bool IsProfileBookmarked(CharacterProfile profile, PlayerPresence presence)
        {
            var cacheKey = ProfileCache.GetProfileCacheKey(profile, presence);

            return !string.IsNullOrWhiteSpace(cacheKey)
                && _profileCache.IsBookmarked(cacheKey);
        }

        public bool IsPresenceBookmarked(PlayerPresence presence)
        {
            var cacheKey = ProfileCache.GetProfileCacheKey(null, presence);

            return !string.IsNullOrWhiteSpace(cacheKey)
                && _profileCache.IsBookmarked(cacheKey);
        }

        public void RemoveBookmark(SavedProfileSummary savedProfile)
        {
            if (savedProfile == null || string.IsNullOrWhiteSpace(savedProfile.CacheKey))
                return;

            _profileCache.RemoveBookmark(savedProfile.CacheKey);
            NotifySavedChanged();
        }

        public bool IsSavedProfileBookmarked(SavedProfile record)
        {
            if (record == null)
                return false;

            if (string.IsNullOrWhiteSpace(record.CacheKey))
                return record.IsBookmarked;

            return _profileCache.IsBookmarked(record.CacheKey);
        }

        public void SaveUpdatedProfile(CharacterProfile profile, PlayerPresence presence, SavedProfile record)
        {
            _profileCache.Save(profile, presence, IsSavedProfileBookmarked(record));
            NotifySavedChanged();
        }

        public void SaveToRecent(CharacterProfile profile, PlayerPresence presence)
        {
            if (!CanSaveToRecent(profile, presence))
                return;

            try
            {
                _profileCache.Save(profile, presence, false);
                NotifySavedChanged();
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to cache viewed SPARK profile.");
            }
        }

        public string ToggleProfileBlock(CharacterProfile profile, PlayerPresence presence)
        {
            var accountName = TextUtil.FirstNonEmpty(presence?.AccountName, profile?.AccountName);

            if (IsProfileBlocked(profile, presence))
                return UnblockAccount(accountName);

            var result = BlockAccount(accountName);

            return _settings.IsBlockedAccount(accountName)
                ? BlockedMessage(GetProfileBlockSubject(profile, presence, accountName), true)
                : result;
        }

        public bool IsProfileBlocked(CharacterProfile profile, PlayerPresence presence)
        {
            var accountName = TextUtil.FirstNonEmpty(presence?.AccountName, profile?.AccountName);

            return _settings.IsBlockedAccount(accountName);
        }

        public string BlockAccount(string accountName)
        {
            accountName = accountName?.Trim() ?? string.Empty;

            if (!SparkSettings.IsValidAccountName(accountName))
                return "No account name available to block.";

            var currentAccountName = _playerState?.GetCached()?.AccountName ?? string.Empty;

            if (string.Equals(accountName, currentAccountName, StringComparison.OrdinalIgnoreCase))
                return "You can't block your own account.";

            try
            {
                var added = _settings.AddBlockedAccount(accountName);

                if (added)
                {
                    NotifyBlockedAccountsChanged();
                    QueueBlockChange(accountName, isBlocked: true);
                    SyncBlocks();
                }

                return BlockedMessage(accountName);
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to block SPARK account {accountName}.", accountName);
                return "Couldn't update the block list.";
            }
        }

        public async Task<string> ReportProfile(
            CharacterProfile profile,
            PlayerPresence presence,
            string reason)
        {
            reason = reason?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(reason))
                return "Please add a short reason for the report.";

            if (reason.Length > ProfileLimits.MaxReportReasonLength)
                reason = reason.Substring(0, ProfileLimits.MaxReportReasonLength);

            if (!CanReportProfile(profile, presence))
                return ReportUnavailableMessage;

            try
            {
                var verificationToken = await GetVerificationTokenAsync(CancellationToken.None);

                if (string.IsNullOrWhiteSpace(verificationToken))
                    return "Report failed. Add a GW2 API key in Blish HUD first.";

                var result = await _apiClient.ReportProfileResultAsync(
                    ProfileReportRequest.FromProfile(profile, presence, reason),
                    verificationToken);

                if (result.Succeeded)
                {
                    var reportId = result.Value?.ReportId?.Trim() ?? string.Empty;

                    return string.IsNullOrWhiteSpace(reportId)
                        ? "Report submitted. Thank you."
                        : $"Report submitted. Report #{reportId}.";
                }

                if (result.StatusCode == HttpStatusCode.NotFound
                    || result.StatusCode == HttpStatusCode.Forbidden)
                    return ReportUnavailableMessage;

                if (result.StatusCode == HttpStatusCode.Unauthorized)
                    return "Report failed. Add a GW2 API key in Blish HUD first.";

                return string.IsNullOrWhiteSpace(result.ErrorMessage)
                    ? "Report failed."
                    : result.ErrorMessage;
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to report SPARK profile.");
                return "Report failed.";
            }
        }

        public string UnblockAccount(string accountName)
        {
            accountName = accountName?.Trim() ?? string.Empty;

            if (!SparkSettings.IsValidAccountName(accountName))
                return "No account name available to unblock.";

            try
            {
                var removed = _settings.RemoveBlockedAccount(accountName);

                if (removed)
                {
                    NotifyBlockedAccountsChanged();
                    QueueBlockChange(accountName, isBlocked: false);
                    SyncBlocks();
                }

                return $"Unblocked {accountName}.";
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to unblock SPARK account {accountName}.", accountName);
                return "Couldn't update the block list.";
            }
        }

        public void WatchSavedProfiles(Action handler)
        {
            if (handler != null)
                SavedProfilesChanged += handler;
        }

        public void UnwatchSavedProfiles(Action handler)
        {
            if (handler != null)
                SavedProfilesChanged -= handler;
        }

        public void WatchBlockedAccounts(Action handler)
        {
            if (handler != null)
                BlockedAccountsChanged += handler;
        }

        public void UnwatchBlockedAccounts(Action handler)
        {
            if (handler != null)
                BlockedAccountsChanged -= handler;
        }

        private void HandleServerSyncStatusChanged(ServerSyncStatus status)
        {
            if (status?.State == ServerSyncState.Connected)
                SyncBlocks();
        }

        private void StartBlockSyncWorker()
        {
            if (!_isStarted || _isDisposed)
                return;

            lock (_blockSyncLock)
            {
                if (_blockSyncWorker != null && !_blockSyncWorker.IsCompleted)
                    return;

                if (_blockSyncCancellation == null || _blockSyncCancellation.IsCancellationRequested)
                    _blockSyncCancellation = new CancellationTokenSource();

                _blockSyncWorker = RunBlocksAsync(_blockSyncCancellation.Token);
            }
        }

        private async Task RunBlocksAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    lock (_blockSyncLock)
                    {
                        if (!_needsFullBlockSync)
                            return;

                        _needsFullBlockSync = false;
                    }

                    var succeeded = await SyncBlocksAsync(cancellationToken);

                    if (succeeded)
                        continue;

                    lock (_blockSyncLock)
                    {
                        _needsFullBlockSync = true;
                    }

                    return;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                lock (_blockSyncLock)
                {
                    _needsFullBlockSync = true;
                }

                Logger.Warn(ex, "Failed to sync the SPARK block list.");
            }
        }

        private Task<bool> SyncBlocksAsync(CancellationToken cancellationToken)
        {
            var accountNames = LocalBlocks();
            return PushBlocksAsync(accountNames, GetBlocklistKey(accountNames), cancellationToken);
        }

        private async Task<bool> PushBlocksAsync(
            IReadOnlyList<string> accountNames,
            string blocklistKey,
            CancellationToken cancellationToken)
        {
            if (_apiClient == null || !_apiClient.IsConfigured)
                return false;

            await _fullBlockSyncGate.WaitAsync(cancellationToken);

            try
            {
                if (IsBlocklistSynced(blocklistKey))
                    return true;

                var verificationToken = await GetVerificationTokenAsync(cancellationToken);

                if (string.IsNullOrWhiteSpace(verificationToken))
                    return false;

                var result = await _apiClient.ReplaceBlocklistResultAsync(
                    accountNames,
                    verificationToken,
                    cancellationToken);

                if (result.Succeeded)
                {
                    MarkBlocklistSynced(blocklistKey);
                    Logger.Info("Synced {count} SPARK blocked account(s) to the server.", accountNames.Count);
                    return true;
                }

                if (result.StatusCode == HttpStatusCode.NotFound || result.StatusCode == HttpStatusCode.MethodNotAllowed)
                    Logger.Warn("SPARK full block-list sync endpoint is unavailable.");

                Logger.Warn(
                    "Failed to sync the full SPARK block list to the server. {message}",
                    result.ErrorMessage);
                return false;
            }
            finally
            {
                _fullBlockSyncGate.Release();
            }
        }

        private void QueueBlockChange(string accountName, bool isBlocked)
        {
            var cancellationToken = _blockSyncCancellation?.Token ?? CancellationToken.None;

            _ = PushBlockChangeAsync(accountName, isBlocked, cancellationToken);
        }

        private async Task PushBlockChangeAsync(
            string accountName,
            bool isBlocked,
            CancellationToken cancellationToken)
        {
            if (_apiClient == null || string.IsNullOrWhiteSpace(accountName))
                return;

            try
            {
                var verificationToken = await GetVerificationTokenAsync(cancellationToken);

                if (string.IsNullOrWhiteSpace(verificationToken))
                    return;

                var result = isBlocked
                    ? await _apiClient.BlockAccountResultAsync(accountName, verificationToken, cancellationToken)
                    : await _apiClient.UnblockAccountResultAsync(accountName, verificationToken, cancellationToken);

                if (!result.Succeeded)
                {
                    Logger.Warn(
                        "Failed to publish SPARK account {action} for {accountName}. {message}",
                        isBlocked ? "block" : "unblock",
                        accountName,
                        result.ErrorMessage);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to publish SPARK account block change for {accountName}.", accountName);
            }
        }

        private async Task<string> GetVerificationTokenAsync(CancellationToken cancellationToken)
        {
            return _tokens == null
                ? string.Empty
                : await _tokens.GetTokenAsync(cancellationToken);
        }

        private void NotifySavedChanged()
        {
            SavedProfilesChanged?.Invoke();
        }

        private void NotifyBlockedAccountsChanged()
        {
            BlockedAccountsChanged?.Invoke();
        }

        private List<string> LocalBlocks()
        {
            return _settings.GetBlockedAccountNames().ToList();
        }

        private bool IsBlocklistSynced(string blocklistKey)
        {
            lock (_blockSyncLock)
            {
                return _blocklistSynced
                    && string.Equals(_lastSyncedBlocklistKey, blocklistKey ?? string.Empty, StringComparison.Ordinal);
            }
        }

        private void MarkBlocklistSynced(string blocklistKey)
        {
            lock (_blockSyncLock)
            {
                _blocklistSynced = true;
                _lastSyncedBlocklistKey = blocklistKey ?? string.Empty;
            }
        }

        private static string GetBlocklistKey(IEnumerable<string> accountNames)
        {
            return string.Join(
                "\n",
                (accountNames ?? Enumerable.Empty<string>())
                    .Select(account => account?.Trim() ?? string.Empty)
                    .Where(account => account.Length > 0)
                    .OrderBy(account => account, StringComparer.OrdinalIgnoreCase)
                    .Select(account => account.ToLowerInvariant()));
        }

        private static string BlockedMessage(string subject, bool includeRemovalHelp = false)
        {
            var message = $"{TextUtil.FirstNonEmpty(subject, "Account")} blocked.";

            return includeRemovalHelp
                ? $"{message} {BlockRemovalHelp}"
                : message;
        }

        private static string GetProfileBlockSubject(
            CharacterProfile profile,
            PlayerPresence presence,
            string accountName)
        {
            return TextUtil.FirstNonEmpty(
                presence?.DisplayCharacterName,
                profile?.DisplayName,
                presence?.OfficialCharacterName,
                profile?.CharacterName,
                accountName);
        }

        private static bool CanReportProfile(CharacterProfile profile, PlayerPresence presence)
        {
            if (presence == null)
                return false;

            if (presence.Status == RPStatus.Offline || presence.Status == RPStatus.Invisible)
                return false;

            return !string.IsNullOrWhiteSpace(TextUtil.FirstNonEmpty(presence.AccountName, profile?.AccountName))
                && !string.IsNullOrWhiteSpace(TextUtil.FirstNonEmpty(presence.OfficialCharacterName, profile?.CharacterName))
                && !string.IsNullOrWhiteSpace(TextUtil.FirstNonEmpty(presence.ActiveProfileId, profile?.ProfileId));
        }

        private static bool CanSaveToRecent(CharacterProfile profile, PlayerPresence presence)
        {
            if (profile == null)
                return false;

            if (string.Equals(profile.ProfileName?.Trim(), "No Active Profile", StringComparison.OrdinalIgnoreCase))
                return false;

            return presence?.HasActiveProfile == true
                || !string.IsNullOrWhiteSpace(presence?.ActiveProfileId)
                || (!string.IsNullOrWhiteSpace(profile.ProfileId)
                    && !string.IsNullOrWhiteSpace(profile.AccountName)
                    && !string.IsNullOrWhiteSpace(profile.CharacterName));
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;

            if (_serverSync != null)
                _serverSync.StatusChanged -= HandleServerSyncStatusChanged;

            var cancellation = _blockSyncCancellation;
            _blockSyncCancellation = null;
            _blockSyncWorker = null;

            cancellation?.Cancel();
        }
    }
}
