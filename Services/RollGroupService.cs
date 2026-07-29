using Blish_HUD;
using rp.spark.Models.Api;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace rp.spark.Services
{
    public sealed class RollGroupService : IDisposable
    {
        private static readonly Logger Logger = Logger.GetLogger<RollGroupService>();
        private static readonly TimeSpan FirstRetryDelay = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromMinutes(1);

        private readonly SparkClient _api;
        private readonly PlayerStateService _playerState;
        private readonly ProfileRepository _profiles;
        private readonly GW2TokenVerification _tokens;
        private readonly SemaphoreSlim _actionGate = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim _pollWake = new SemaphoreSlim(0, 1);
        private readonly Random _retryRandom = new Random();
        private readonly object _pollWakeLock = new object();
        private readonly object _stateLock = new object();

        private CancellationTokenSource _loopCancel;
        private Task _loopTask;
        private RollGroup _group;
        private string _accountName = string.Empty;
        private string _lastStatus = string.Empty;
        private long _after;
        private bool _pollingEnabled;
        private bool _disposed;

        public RollGroupService(SparkClient api, PlayerStateService playerState, ProfileRepository profiles, GW2TokenVerification tokens)
        {
            _api = api;
            _playerState = playerState;
            _profiles = profiles;
            _tokens = tokens;
        }

        public event Action StateChanged;

        public RollGroup CurrentGroup { get { lock (_stateLock) return _group; } }
        public string CurrentAccountName { get { lock (_stateLock) return _accountName; } }
        public string LastStatus { get { lock (_stateLock) return _lastStatus; } }
        public bool PollingEnabled { get { lock (_stateLock) return _pollingEnabled; } }

        public void SetPollingEnabled(bool enabled)
        {
            if (_disposed)
                return;

            lock (_stateLock) _pollingEnabled = enabled;

            WakePollingLoop();
        }

        public void Start()
        {
            if (_disposed || (_loopTask != null && !_loopTask.IsCompleted))
                return;

            _loopCancel = new CancellationTokenSource();
            _loopTask = RunAsync(_loopCancel.Token);
        }

        public void Stop()
        {
            var cancellation = _loopCancel;
            var task = _loopTask;

            _loopCancel = null;
            _loopTask = null;

            if (cancellation == null)
                return;

            cancellation.Cancel();
            TaskCleanup.DisposeWhenComplete(task, cancellation);
        }

        public Task<bool> RefreshAsync(CancellationToken cancellationToken = default) =>
            RunActionAsync(
                session => RefreshGroupAsync(
                    session,
                    "Unable to refresh the roll group.",
                    string.Empty,
                    cancellationToken),
                cancellationToken);

        private async Task<bool> RefreshGroupAsync(Session session, string error, string status, CancellationToken cancellationToken)
        {
            var result = await _api.GetCurrentRollGroupResultAsync(session.Token, cancellationToken).ConfigureAwait(false);

            if (!result.Succeeded)
                return Fail(result, error);

            var group = result.Value?.Group;
            var member = group?.Members?.FirstOrDefault(entry => SameText(entry?.AccountName, session.AccountName));

            if (member != null && !SameText(member.CharacterName, session.DisplayName))
            {
                var request = ForSession(new RollMemberUpdateRequest(), session);
                var update = await _api
                    .UpdateRollMemberResultAsync(request, session.Token, cancellationToken)
                    .ConfigureAwait(false);

                if (update.Succeeded)
                    group = update.Value?.Group ?? group;
            }

            ApplyGroup(group, status);
            return true;
        }

        public Task<bool> CreateAsync(string password, CancellationToken cancellationToken = default)
        {
            return RunRequestAsync(
                session => _api.CreateRollGroupResultAsync(
                    ForSession(new CreateRollGroupRequest
                    {
                        Password = password?.Trim() ?? string.Empty
                    }, session),
                    session.Token,
                    cancellationToken),
                "Unable to create the roll group.",
                response => ApplyGroup(response?.Group, "Roll group created."),
                cancellationToken);
        }

        public Task<bool> JoinAsync(string code, string password, CancellationToken cancellationToken = default)
        {
            return RunRequestAsync(
                session => _api.JoinRollGroupResultAsync(
                    ForSession(new JoinRollGroupRequest
                    {
                        Code = NormalizeCode(code),
                        Password = password?.Trim() ?? string.Empty
                    }, session),
                    session.Token,
                    cancellationToken),
                "Unable to join the roll group.",
                response => ApplyGroup(response?.Group, "Roll group joined."),
                cancellationToken);
        }

        public Task<bool> RollAsync(string expression, CancellationToken cancellationToken = default)
        {
            expression = expression?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(expression))
            {
                SetStatus("Enter a dice roll such as 1d20.");
                return Task.FromResult(false);
            }

            return RunRequestAsync(
                session => _api.SubmitRollResultAsync(
                    ForSession(new RollRequest
                    {
                        Expression = expression
                    }, session),
                    session.Token,
                    cancellationToken),
                "Unable to submit the dice roll.",
                response =>
                {
                    MergeEvents(new[] { response?.Event }, null);
                    SetStatus(string.Empty);
                },
                cancellationToken);
        }

        public Task<bool> AddHeaderAsync(
            string text,
            CancellationToken cancellationToken = default)
        {
            text = text?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(text))
            {
                SetStatus("Enter a group header.");
                return Task.FromResult(false);
            }

            if (text.Length > ProfileLimits.MaxRollHeaderLength)
            {
                SetStatus(
                    $"Group headers can be up to " +
                    $"{ProfileLimits.MaxRollHeaderLength} characters.");
                return Task.FromResult(false);
            }

            return RunRequestAsync(
                session => _api.SubmitRollHeaderResultAsync(
                    new RollHeaderRequest
                    {
                        Text = text
                    },
                    session.Token,
                    cancellationToken),
                "Unable to add the group header.",
                response =>
                {
                    MergeEvents(
                        new[] { response?.Event },
                        null);

                    SetStatus(string.Empty);
                },
                cancellationToken);
        }

        public Task<bool> LeaveAsync(CancellationToken cancellationToken = default)
        {
            return RunRequestAsync(
                session => _api.LeaveRollGroupResultAsync(session.Token, cancellationToken),
                "Unable to leave the roll group.",
                _ => ApplyGroup(null, "You left the roll group."),
                cancellationToken);
        }

        public Task<bool> DisbandAsync(CancellationToken cancellationToken = default)
        {
            return RunRequestAsync(
                session => _api.DisbandRollGroupResultAsync(session.Token, cancellationToken),
                "Unable to disband the roll group.",
                _ => ApplyGroup(null, "Roll group disbanded."),
                cancellationToken);
        }

        public Task<bool> KickAsync(string accountName, CancellationToken cancellationToken = default)
        {
            return RunActionAsync(async session =>
            {
                var result = await _api
                    .KickRollGroupMemberResultAsync(accountName, session.Token, cancellationToken)
                    .ConfigureAwait(false);

                if (!result.Succeeded)
                    return Fail(result, "Unable to remove that group member.");

                var error = "Member removed, but the group could not refresh.";
                var status = $"{accountName} was removed.";

                return await RefreshGroupAsync(session, error, status, cancellationToken)
                    .ConfigureAwait(false);
            }, cancellationToken);
        }

        public Task<bool> UpdateSettingsAsync(bool allowNewMembers, string newPassword, bool clearPassword, CancellationToken cancellationToken = default)
        {
            return RunRequestAsync(
                session => _api.UpdateRollGroupResultAsync(
                    new RollGroupSettingsRequest
                    {
                        JoinLocked = !allowNewMembers,
                        NewPassword = newPassword?.Trim() ?? string.Empty,
                        ClearPassword = clearPassword
                    },
                    session.Token,
                    cancellationToken),
                "Unable to update group settings.",
                response => ApplyGroup(response?.Group, "Group settings updated."),
                cancellationToken);
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            var failureCount = 0;

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var group = await WaitForPollableGroupAsync(cancellationToken).ConfigureAwait(false);
                    var succeeded = await ListenOnceAsync(group, cancellationToken).ConfigureAwait(false);

                    if (!PollingEnabled)
                    {
                        failureCount = 0;
                        continue;
                    }

                    if (succeeded)
                    {
                        failureCount = 0;
                        continue;
                    }

                    failureCount++;
                    await Task.Delay(GetRetryDelay(failureCount), cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "SPARK roll group listener failed.");
                    SetStatus("Roll group updates are reconnecting.");

                    if (!PollingEnabled)
                    {
                        failureCount = 0;
                        continue;
                    }

                    failureCount++;

                    try
                    {
                        await Task.Delay(GetRetryDelay(failureCount), cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                }
            }
        }

        private async Task<RollGroup> WaitForPollableGroupAsync(CancellationToken cancellationToken)
        {
            while (true)
            {
                lock (_stateLock)
                {
                    if (_pollingEnabled && _group != null)
                        return _group;
                }

                await _pollWake.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        private void WakePollingLoop()
        {
            lock (_pollWakeLock)
            {
                if (_pollWake.CurrentCount == 0)
                    _pollWake.Release();
            }
        }

        // Adding a randomized delay so all clients won't hammer the server the same time each time
        private TimeSpan GetRetryDelay(int failureCount)
        {
            var exponent = Math.Min(5, Math.Max(0, failureCount - 1));
            var seconds = Math.Min(MaxRetryDelay.TotalSeconds, FirstRetryDelay.TotalSeconds * Math.Pow(2, exponent));
            var jitter = 0.75 + _retryRandom.NextDouble() * 0.25;

            return TimeSpan.FromSeconds(seconds * jitter);
        }

        private async Task<bool> ListenOnceAsync(RollGroup observedGroup, CancellationToken cancellationToken)
        {
            var token = await _tokens.GetTokenAsync(cancellationToken).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(token))
            {
                SetStatus("Add a valid GW2 API key to receive group updates.");
                return false;
            }

            long after;

            lock (_stateLock) after = _after;

            var result = await _api.ListenRollEventsResultAsync(
                observedGroup.GroupId,
                after,
                observedGroup.Revision,
                token,
                cancellationToken).ConfigureAwait(false);

            if (!result.Succeeded)
            {
                if (result.StatusCode == HttpStatusCode.NotFound)
                {
                    ClearGroupIf(observedGroup.GroupId, "The roll group is no longer available.");
                    return true;
                }

                if (result.StatusCode == HttpStatusCode.Unauthorized)
                    _tokens.Clear();

                SetStatus(result.ErrorMessage ?? "Roll group updates are reconnecting.");

                if (result.StatusCode == HttpStatusCode.Forbidden)
                {
                    SetPollingEnabled(false);
                    return true;
                }

                return false;
            }

            if (!IsCurrentGroup(observedGroup.GroupId))
                return true;

            MergeEvents(result.Value?.Events, observedGroup.GroupId);

            if (PollingEnabled && result.Value?.GroupChanged == true && IsCurrentGroup(observedGroup.GroupId))
                return await RefreshAsync(cancellationToken).ConfigureAwait(false);

            return true;
        }

        private async Task<bool> RunActionAsync(Func<Session, Task<bool>> action, CancellationToken cancellationToken)
        {
            if (_disposed)
                return false;

            await _actionGate.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                if (_disposed)
                    return false;

                var session = await GetSessionAsync(cancellationToken).ConfigureAwait(false);

                return session != null && await action(session).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "SPARK roll group action failed.");
                SetStatus("The roll group action failed.");
                return false;
            }
            finally
            {
                _actionGate.Release();
            }
        }

        private Task<bool> RunRequestAsync<T>(Func<Session, Task<ApiResult<T>>> request, string error, Action<T> success, CancellationToken cancellationToken)
        {
            return RunActionAsync(
                async session =>
                {
                    var result = await request(session).ConfigureAwait(false);

                    if (!result.Succeeded)
                        return Fail(result, error);

                    success(result.Value);
                    return true;
                },
                cancellationToken);
        }

        private async Task<Session> GetSessionAsync(CancellationToken cancellationToken)
        {
            var state = await _playerState.GetCurrentAsync(cancellationToken).ConfigureAwait(false);

            if (state == null
                || string.IsNullOrWhiteSpace(state.AccountName)
                || string.IsNullOrWhiteSpace(state.OfficialCharacterName))
            {
                SetStatus("Load into GW2 on a character before using roll groups.");
                return null;
            }

            var token = await _tokens.GetTokenAsync(cancellationToken).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(token))
            {
                SetStatus("Add a GW2 API key with account and characters permissions.");
                return null;
            }

            var account = state.AccountName.Trim();
            var official = state.OfficialCharacterName.Trim();
            var profile = _profiles.LoadActiveForCharacter(account, official);

            var session = new Session
            {
                Token = token,
                AccountName = account,
                OfficialCharacterName = official,
                DisplayName = TextUtil.FirstNonEmpty(profile?.DisplayName, official)
            };

            lock (_stateLock) _accountName = session.AccountName;

            return session;
        }

        private bool Fail<T>(ApiResult<T> result, string fallback)
        {
            SetStatus(string.IsNullOrWhiteSpace(result?.ErrorMessage)
                ? fallback
                : result.ErrorMessage);

            return false;
        }

        private void ApplyGroup(RollGroup group, string status, string expectedGroupId = null)
        {
            if (_disposed)
                return;

            lock (_stateLock)
            {
                if (expectedGroupId != null && !SameGroup(_group, expectedGroupId))
                    return;

                _group = group;
                _lastStatus = status ?? string.Empty;
                _after = group?.History?
                    .Where(entry => entry != null)
                    .Select(entry => entry.Sequence)
                    .DefaultIfEmpty(group?.LastSequence ?? 0)
                    .Max() ?? 0;
            }

            WakePollingLoop();
            Notify();
        }

        private void MergeEvents(IEnumerable<RollEvent> events, string expectedGroupId)
        {
            var additions = (events ?? Enumerable.Empty<RollEvent>())
                .Where(entry => entry != null)
                .ToList();

            if (additions.Count == 0)
                return;

            lock (_stateLock)
            {
                if (_group == null
                    || (!string.IsNullOrWhiteSpace(expectedGroupId)
                        && !SameGroup(_group, expectedGroupId)))
                    return;

                var merged = (_group.History ?? new List<RollEvent>())
                    .Concat(additions)
                    .Where(entry => entry != null)
                    .GroupBy(entry => entry.Sequence)
                    .Select(group => group.Last())
                    .OrderBy(entry => entry.Sequence)
                    .ToList();

                var excess = merged.Count - ProfileLimits.MaxRollHistory;

                if (excess > 0)
                    merged.RemoveRange(0, excess);

                _after = Math.Max(_after, additions.Max(entry => entry.Sequence));

                _group = CopyGroup(_group, merged, _after);
            }

            Notify();
        }

        private static RollGroup CopyGroup(RollGroup source, List<RollEvent> history, long lastSequence)
        {
            return new RollGroup
            {
                GroupId = source.GroupId,
                Code = source.Code,
                OwnerAccountName = source.OwnerAccountName,
                CreatedAt = source.CreatedAt,
                ExpiresAt = source.ExpiresAt,
                JoinLocked = source.JoinLocked,
                HasPassword = source.HasPassword,
                Revision = source.Revision,
                LastSequence = lastSequence,
                Members = source.Members ?? new List<RollMember>(),
                History = history ?? new List<RollEvent>()
            };
        }

        private static bool SameGroup(RollGroup group, string groupId) =>
            group != null && string.Equals(group.GroupId, groupId, StringComparison.Ordinal);

        private bool IsCurrentGroup(string groupId)
        {
            lock (_stateLock)
                return SameGroup(_group, groupId);
        }

        private void ClearGroupIf(string groupId, string status) => ApplyGroup(null, status, groupId);

        private void SetStatus(string status)
        {
            if (_disposed)
                return;

            lock (_stateLock) _lastStatus = status ?? string.Empty;

            Notify();
        }

        private void Notify()
        {
            if (_disposed)
                return;

            try
            {
                StateChanged?.Invoke();
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "SPARK roll group state listener failed.");
            }
        }

        private static T ForSession<T>(T request, Session session) where T : RollCharacterRequest
        {
            request.CharacterName = session.OfficialCharacterName;
            request.DisplayName = session.DisplayName;
            return request;
        }

        private static bool SameText(string left, string right) =>
            string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);

        private static string NormalizeCode(string code)
        {
            return (code ?? string.Empty)
                .Trim()
                .Replace("-", string.Empty)
                .Replace(" ", string.Empty)
                .ToUpperInvariant();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            Stop();
            _disposed = true;
            StateChanged = null;
        }

        private sealed class Session
        {
            public string Token { get; set; } = string.Empty;
            public string AccountName { get; set; } = string.Empty;
            public string OfficialCharacterName { get; set; } = string.Empty;
            public string DisplayName { get; set; } = string.Empty;
        }
    }
}