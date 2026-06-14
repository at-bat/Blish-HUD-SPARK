using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Graphics.UI;
using rp.spark.Models;
using rp.spark.Services;
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;

namespace rp.spark.UI.Views
{
    public class SparkSettingsView : View
    {
        private const int ContentWidth = 660;
        private const int ControlHeight = 30;
        private const int RowGap = 8;
        private const int ApiKeyWarningHeight = 44;

        private readonly SparkSettings _settings;
        private readonly Func<ServerSyncStatus> _getServerSyncStatus;
        private readonly Action<Action<ServerSyncStatus>> _watchServerSyncStatus;
        private readonly Action<Action<ServerSyncStatus>> _unwatchServerSyncStatus;
        private readonly Action _requestServerSync;
        private readonly Action _enforceGameplayWindowVisibility;
        private readonly Func<string> _getImportantNotice;
        private readonly SparkSettingsButtons _buttons;
        private readonly SparkBlocklist _blocklist;

        private bool _isUnloaded;
        private Label _serverStatusLabel;
        private Label _apiKeyWarning;

        private static readonly TimeSpan NoticeRefreshInterval = TimeSpan.FromMilliseconds(500);

        private CancellationTokenSource _noticeRefreshCancel;
        private Task _noticeRefreshTask;

        public SparkSettingsView(
            Action openProfileManager,
            Action openProfileViewer,
            Action openOnlineList,
            Action openSavedProfiles,
            Action openAbout,
            Func<System.Threading.Tasks.Task<string>> waitForInitialState,
            Func<string> getCurrentStateMessage,
            Action requestStateRefresh,
            SparkSettings settings,
            Func<ServerSyncStatus> getServerSyncStatus,
            Action<Action<ServerSyncStatus>> watchServerSyncStatus,
            Action<Action<ServerSyncStatus>> unwatchServerSyncStatus,
            Action requestServerSync,
            Func<string> getImportantNotice,
            Func<bool> shouldHideGameplayWindows,
            Action enforceGameplayWindowVisibility,
            Func<string, string> blockAccount,
            Func<string, string> unblockAccount,
            Action<Action> watchBlockedAccountsChanged,
            Action<Action> unwatchBlockedAccountsChanged)
        {
            _settings = settings;
            _getServerSyncStatus = getServerSyncStatus;
            _watchServerSyncStatus = watchServerSyncStatus;
            _unwatchServerSyncStatus = unwatchServerSyncStatus;
            _requestServerSync = requestServerSync;
            _getImportantNotice = getImportantNotice;
            _enforceGameplayWindowVisibility = enforceGameplayWindowVisibility;
            _buttons = new SparkSettingsButtons(
                openProfileManager,
                openProfileViewer,
                openOnlineList,
                openSavedProfiles,
                openAbout,
                waitForInitialState,
                getCurrentStateMessage,
                requestStateRefresh,
                shouldHideGameplayWindows);
            _blocklist = new SparkBlocklist(
                settings,
                blockAccount,
                unblockAccount,
                watchBlockedAccountsChanged,
                unwatchBlockedAccountsChanged);
        }

        protected override void Build(Container buildPanel)
        {
            BuildSettings(buildPanel);
            WatchServer();
            WatchGameState();
            StartNoticeRefresh();
            RefreshServerStatus();
        }

        private void BuildSettings(Container buildPanel)
        {
            var settingsStack = SparkFormLayout.AddVerticalStack(
                buildPanel,
                0,
                0,
                ContentWidth,
                620,
                6);

            BuildServerStatus(settingsStack);
            BuildImportantNotice(settingsStack);

            SparkFormLayout.AddSpacer(settingsStack, ContentWidth, 4);
            _buttons.Build(settingsStack);

            SparkFormLayout.AddSpacer(settingsStack, ContentWidth, 4);
            BuildPresence(settingsStack);

            SparkFormLayout.AddSpacer(settingsStack, ContentWidth, 4);
            BuildGlobalSettings(settingsStack);

            SparkFormLayout.AddSpacer(settingsStack, ContentWidth, 5);
            _blocklist.Build(settingsStack, ContentWidth);
        }

        private void BuildImportantNotice(FlowPanel settingsStack)
        {
            _apiKeyWarning = SparkFormLayout.AddLabel(
                settingsStack,
                string.Empty,
                ContentWidth,
                24,
                GameService.Content.DefaultFont14,
                SparkViewUI.WarningTextColor,
                true);

            _apiKeyWarning.WrapText = true;
            RefreshApiKeyWarning();
        }

        private void BuildServerStatus(FlowPanel settingsStack)
        {
            var serverRow = SparkFormLayout.AddRow(settingsStack, ContentWidth, 24, 5);

            SparkFormLayout.AddLabel(
                serverRow,
                "Server status:",
                110,
                24,
                GameService.Content.DefaultFont12);

            _serverStatusLabel = SparkFormLayout.AddLabel(
                serverRow,
                ServerStatusText(_getServerSyncStatus?.Invoke()),
                ContentWidth - 115,
                24,
                GameService.Content.DefaultFont12,
                SparkViewUI.SecondaryTextColor);
        }

        private void BuildPresence(FlowPanel settingsStack)
        {
            var currentStatus = _settings.CurrentStatus.Value == RPStatus.Offline
                ? RPStatus.Online
                : _settings.CurrentStatus.Value;

            var statusRow = SparkFormLayout.AddRow(settingsStack, ContentWidth, ControlHeight, RowGap);

            SparkFormLayout.AddLabel(
                statusRow,
                "Status:",
                55,
                ControlHeight,
                GameService.Content.DefaultFont14);

            var statusDropdown = SparkFormLayout.AddDropdown(
                statusRow,
                ProfileLabels.RpStatusOptions,
                ProfileLabels.StatusLabel(currentStatus),
                155,
                ControlHeight);

            statusDropdown.ValueChanged += (s, e) =>
            {
                _settings.CurrentStatus.Value = ProfileLabels.ParseStatus(statusDropdown.SelectedItem?.ToString());
                _requestServerSync?.Invoke();
            };
        }

        private void WatchServer()
        {
            _watchServerSyncStatus?.Invoke(OnServerStatus);
        }

        private void UnwatchServer()
        {
            _unwatchServerSyncStatus?.Invoke(OnServerStatus);
        }

        private void StartNoticeRefresh()
        {
            StopNoticeRefresh();

            _noticeRefreshCancel = new CancellationTokenSource();
            _noticeRefreshTask = RefreshNoticeLoopAsync(_noticeRefreshCancel.Token);
        }

        private async Task RefreshNoticeLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(NoticeRefreshInterval, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                GameService.Overlay.QueueMainThreadUpdate(gameTime =>
                {
                    if (!_isUnloaded)
                        RefreshApiKeyWarning();
                });
            }
        }

        private void StopNoticeRefresh()
        {
            var cancellation = _noticeRefreshCancel;
            var task = _noticeRefreshTask;

            _noticeRefreshCancel = null;
            _noticeRefreshTask = null;

            if (cancellation == null)
                return;

            cancellation.Cancel();
            TaskCleanup.DisposeWhenComplete(task, cancellation);
        }

        private void OnServerStatus(ServerSyncStatus status)
        {
            GameService.Overlay.QueueMainThreadUpdate(gameTime =>
            {
                if (!_isUnloaded)
                    RefreshServerStatus(status);
            });
        }

        private void RefreshServerStatus(ServerSyncStatus status = null)
        {
            if (_serverStatusLabel == null)
                return;

            _serverStatusLabel.Text = ServerStatusText(_getServerSyncStatus?.Invoke() ?? status);
            RefreshApiKeyWarning();
        }

        private void WatchGameState()
        {
            GameService.Gw2Mumble.IsAvailableChanged += OnGameStateChanged;
            GameService.Gw2Mumble.FinishedLoading += OnGameStateChanged;
            GameService.Gw2Mumble.PlayerCharacter.NameChanged += OnGameStateChanged;
            GameService.Gw2Mumble.UI.IsMapOpenChanged += OnGameStateChanged;
            GameService.GameIntegration.Gw2Instance.IsInGameChanged += OnGameStateChanged;
        }

        private void UnwatchGameState()
        {
            GameService.Gw2Mumble.IsAvailableChanged -= OnGameStateChanged;
            GameService.Gw2Mumble.FinishedLoading -= OnGameStateChanged;
            GameService.Gw2Mumble.PlayerCharacter.NameChanged -= OnGameStateChanged;
            GameService.Gw2Mumble.UI.IsMapOpenChanged -= OnGameStateChanged;
            GameService.GameIntegration.Gw2Instance.IsInGameChanged -= OnGameStateChanged;
        }

        private void OnGameStateChanged(object sender, EventArgs e)
        {
            GameService.Overlay.QueueMainThreadUpdate(gameTime =>
            {
                if (!_isUnloaded)
                    RefreshApiKeyWarning();
            });
        }

        // Reserving space for the API key message since dynamic height isn't working on this bit
        private void RefreshApiKeyWarning()
        {
            if (_apiKeyWarning == null)
                return;

            var notice = GetImportantNotice();

            _apiKeyWarning.Text = string.IsNullOrWhiteSpace(notice)
                ? "SPARK ready."
                : notice;

            _apiKeyWarning.TextColor = string.IsNullOrWhiteSpace(notice)
                ? new Color(140, 220, 140)
                : SparkViewUI.WarningTextColor;

            _apiKeyWarning.Height = string.IsNullOrWhiteSpace(notice)
                ? 24
                : ApiKeyWarningHeight;

            _apiKeyWarning.Visible = true;
        }

        private void BuildGlobalSettings(FlowPanel settingsStack)
        {
            var sharingRow = SparkFormLayout.AddRow(settingsStack, ContentWidth, ControlHeight, 12);

            var broadcastCheckbox = SparkFormLayout.AddCheckbox(
                sharingRow,
                "Share my profile online",
                _settings.BroadcastProfile.Value,
                230,
                ControlHeight);

            broadcastCheckbox.CheckedChanged += (s, e) => _settings.BroadcastProfile.Value = broadcastCheckbox.Checked;
            broadcastCheckbox.CheckedChanged += (s, e) => _requestServerSync?.Invoke();

            var hideLocationCheckbox = SparkFormLayout.AddCheckbox(
                sharingRow,
                "Hide my location",
                _settings.HideLocation.Value,
                180,
                ControlHeight);

            hideLocationCheckbox.CheckedChanged += (s, e) => _settings.HideLocation.Value = hideLocationCheckbox.Checked;
            hideLocationCheckbox.CheckedChanged += (s, e) => _requestServerSync?.Invoke();

            var optionsRow = SparkFormLayout.AddRow(settingsStack, ContentWidth, ControlHeight, 12);

            SparkFormLayout.AddLabel(
                optionsRow,
                "Region:",
                55,
                ControlHeight,
                GameService.Content.DefaultFont14);

            var regionDropdown = SparkFormLayout.AddDropdown(
                optionsRow,
                new[] { ProfileRegion.NA.ToString(), ProfileRegion.EU.ToString() },
                _settings.RegionFilter.Value.ToString(),
                90,
                ControlHeight);

            regionDropdown.ValueChanged += (s, e) =>
            {
                if (Enum.TryParse(regionDropdown.SelectedItem?.ToString(), out ProfileRegion selectedRegion))
                {
                    _settings.RegionFilter.Value = selectedRegion;
                    _requestServerSync?.Invoke();
                }
            };

            var autoHideCheckbox = SparkFormLayout.AddCheckbox(
                optionsRow,
                "Auto-hide during map/UI",
                _settings.AutoHideGameUi.Value,
                230,
                ControlHeight);

            autoHideCheckbox.CheckedChanged += (s, e) =>
            {
                _settings.AutoHideGameUi.Value = autoHideCheckbox.Checked;
                RefreshApiKeyWarning();
                _buttons.Refresh();
                _enforceGameplayWindowVisibility?.Invoke();
            };
        }

        private string GetImportantNotice()
        {
            try
            {
                return _getImportantNotice?.Invoke() ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string ServerStatusText(ServerSyncStatus status)
        {
            if (status == null)
                return "Disconnected";

            if (string.IsNullOrWhiteSpace(status.DisplayName))
                return status.Message;

            return string.IsNullOrWhiteSpace(status.Message)
                ? status.DisplayName
                : $"{status.DisplayName}: {status.Message}";
        }

        protected override void Unload()
        {
            _isUnloaded = true;
            StopNoticeRefresh();
            _buttons.Dispose();
            _blocklist.Dispose();
            UnwatchServer();
            UnwatchGameState();
        }
    }
}
