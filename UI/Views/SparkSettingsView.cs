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
        private const int StatusLineHeight = 24;
        private const int RowGap = 8;
        private const int LeftPadding = 8;

        private readonly SparkSettings _settings;
        private readonly Func<ServerSyncStatus> _getServerSyncStatus;
        private readonly Action<Action<ServerSyncStatus>> _watchServerSyncStatus;
        private readonly Action<Action<ServerSyncStatus>> _unwatchServerSyncStatus;
        private readonly Action _requestServerSync;
        private readonly Action _enforceGameplayWindowVisibility;
        private readonly Func<string> _getImportantNotice;
        private readonly SparkSettingsButtons _buttons;
        private readonly Action _openBlocklist;
        private readonly Action<Action> _watchBlockedAccountsChanged;
        private readonly Action<Action> _unwatchBlockedAccountsChanged;
        private readonly Action<bool> _maturePreferenceChanged;

        private Label _blockedAccountsLabel;

        // Mature content window
        private Container _buildPanel;
        private StandardButton _matureButton;
        private Panel _matureConfirmationPanel;

        private bool _isUnloaded;
        private Label _serverStatusLabel;
        // Originally this was for knowing if there were API key issues but it's been renamed to readiness for clarity
        private Label _readinessLabel;

        private static readonly TimeSpan NoticeRefreshInterval = TimeSpan.FromMilliseconds(500);

        private CancellationTokenSource _noticeRefreshCancel;
        private Task _noticeRefreshTask;

        public SparkSettingsView(
            Action openProfileManager,
            Action openProfileViewer,
            Action openOnlineList,
            Action openSavedProfiles,
            Action openAbout,
            Action openBlocklist,
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
            Action<Action> watchBlockedAccountsChanged,
            Action<Action> unwatchBlockedAccountsChanged,
            Action<bool> maturePreferenceChanged)
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
            _openBlocklist = openBlocklist;
            _watchBlockedAccountsChanged = watchBlockedAccountsChanged;
            _unwatchBlockedAccountsChanged = unwatchBlockedAccountsChanged;
            _maturePreferenceChanged = maturePreferenceChanged;
        }

        protected override void Build(Container buildPanel)
        {
            _buildPanel = buildPanel;
            BuildSettings(buildPanel);
            WatchServer();
            WatchGameState();
            StartNoticeRefresh();
            RefreshServerStatus();
        }

        private void BuildSettings(Container buildPanel)
        {
            var settingsStack = SparkFormLayout.AddAutoStack(
                buildPanel,
                ContentWidth,
                6);
            settingsStack.Left = LeftPadding;

            BuildServerStatus(settingsStack);
            BuildReadinessNotice(settingsStack);
            _buttons.Build(settingsStack);

            SparkFormLayout.AddSpacer(settingsStack, ContentWidth, 4);
            BuildGlobalSettings(settingsStack);

            SparkFormLayout.AddSpacer(settingsStack, ContentWidth, 4);
            BuildMatureProfileSetting(settingsStack);

            SparkFormLayout.AddSpacer(settingsStack, ContentWidth, 4);
            BuildBlockSummary(settingsStack);
        }

        private void BuildReadinessNotice(FlowPanel settingsStack)
        {
            _readinessLabel = SparkFormLayout.AddLabel(
                settingsStack,
                string.Empty,
                ContentWidth,
                StatusLineHeight,
                GameService.Content.DefaultFont14,
                SparkViewUI.WarningTextColor,
                true);

            _readinessLabel.WrapText = true;
            RefreshReadinessNotice();
        }

        private void BuildServerStatus(FlowPanel settingsStack)
        {
            const int labelWidth = 95;
            const int columnGap = 5;

            var serverRow = SparkFormLayout.AddRow(
                settingsStack,
                ContentWidth,
                StatusLineHeight,
                columnGap);

            SparkFormLayout.AddLabel(
                serverRow,
                "Server status:",
                labelWidth,
                StatusLineHeight,
                GameService.Content.DefaultFont14);

            _serverStatusLabel = SparkFormLayout.AddLabel(
                serverRow,
                ServerStatusText(_getServerSyncStatus?.Invoke()),
                ContentWidth - labelWidth - columnGap,
                StatusLineHeight,
                GameService.Content.DefaultFont14,
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

        private void BuildBlockSummary(FlowPanel settingsStack)
        {
            var blockRow = SparkFormLayout.AddRow(settingsStack, ContentWidth, ControlHeight, 12);

            SparkFormLayout.AddLabel(
                blockRow,
                "Blocked accounts:",
                125,
                ControlHeight,
                GameService.Content.DefaultFont14);

            _blockedAccountsLabel = SparkFormLayout.AddLabel(
                blockRow,
                string.Empty,
                90,
                ControlHeight,
                GameService.Content.DefaultFont14,
                SparkViewUI.SecondaryTextColor);

            var manageButton = SparkFormLayout.AddButton(
                blockRow,
                "Manage Blocks",
                130,
                ControlHeight);

            manageButton.Click += (s, e) => _openBlocklist?.Invoke();

            _watchBlockedAccountsChanged?.Invoke(OnBlockedAccountsChanged);
            RefreshBlockedAccountCount();
        }

        private void OnBlockedAccountsChanged()
        {
            SparkUiThread.Queue(() =>
            {
                if (!_isUnloaded)
                    RefreshBlockedAccountCount();
            });
        }

        private void RefreshBlockedAccountCount()
        {
            if (_blockedAccountsLabel == null)
                return;

            var count = _settings?.GetBlockedAccountNames().Count ?? 0;
            _blockedAccountsLabel.Text = count == 1
                ? "1 blocked"
                : $"{count} blocked";
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

                SparkUiThread.Queue(() =>
                {
                    if (!_isUnloaded)
                        RefreshReadinessNotice();
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
            SparkUiThread.Queue(() =>
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
            RefreshReadinessNotice();
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
            SparkUiThread.Queue(() =>
            {
                if (!_isUnloaded)
                    RefreshReadinessNotice();
            });
        }

        // Reserving space for the API key message since dynamic height isn't working on this bit
        private void RefreshReadinessNotice()
        {
            if (_readinessLabel == null)
                return;

            var notice = GetImportantNotice();

            _readinessLabel.Text = string.IsNullOrWhiteSpace(notice)
                ? "SPARK tools ready."
                : notice;

            _readinessLabel.TextColor = string.IsNullOrWhiteSpace(notice)
                ? new Color(140, 220, 140)
                : SparkViewUI.WarningTextColor;

            _readinessLabel.Height = StatusLineHeight;
            _readinessLabel.Visible = true;
        }

        private void BuildGlobalSettings(FlowPanel settingsStack)
        {
            var sharingRow = SparkFormLayout.AddRow(settingsStack, ContentWidth, ControlHeight, 12);

            var broadcastCheckbox = SparkFormLayout.AddCheckbox(
                sharingRow,
                "Share my profile",
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

            var autoRefreshCheckbox = SparkFormLayout.AddCheckbox(
                optionsRow,
                "Auto-refresh Online List",
                _settings.AutoRefreshOnlineProfiles.Value,
                220,
                ControlHeight);

            autoRefreshCheckbox.CheckedChanged += (s, e) =>
            {
                _settings.AutoRefreshOnlineProfiles.Value =
                    autoRefreshCheckbox.Checked;
            };

            var autoHideCheckbox = SparkFormLayout.AddCheckbox(
                optionsRow,
                "Auto-hide UI",
                _settings.AutoHideGameUi.Value,
                230,
                ControlHeight);

            autoHideCheckbox.CheckedChanged += (s, e) =>
            {
                _settings.AutoHideGameUi.Value = autoHideCheckbox.Checked;
                RefreshReadinessNotice();
                _buttons.Refresh();
                _enforceGameplayWindowVisibility?.Invoke();
            };
        }

        private void BuildMatureProfileSetting(FlowPanel settingsStack)
        {
            var row = SparkFormLayout.AddRow(
                settingsStack,
                ContentWidth,
                ControlHeight,
                12);

            _matureButton = SparkFormLayout.AddButton(
                row,
                string.Empty,
                210,
                ControlHeight);

            _matureButton.Click += (s, e) =>
            {
                if (_settings.ShowMatureProfiles.Value)
                {
                    SetMatureProfilesEnabled(false);
                    return;
                }

                OpenMatureConfirmation();
            };

            RefreshMatureSettingUi();
        }

        private void SetMatureProfilesEnabled(bool enabled)
        {
            _settings.ShowMatureProfiles.Value = enabled;
            RefreshMatureSettingUi();
            _maturePreferenceChanged?.Invoke(enabled);
        }

        private void RefreshMatureSettingUi()
        {
            if (_matureButton == null)
                return;

            _matureButton.Text = _settings.ShowMatureProfiles.Value
                ? "Mature Profiles Visible"
                : "Mature Profiles Hidden";
        }

        private void OpenMatureConfirmation()
        {
            CloseMatureConfirmation();

            var popupParent = _buildPanel ?? GameService.Graphics.SpriteScreen;
            const int popupWidth = 500;
            const int popupHeight = 190;

            _matureConfirmationPanel = new Panel
            {
                ShowBorder = true,
                Title = "Enable Mature Profiles?",
                Size = new Point(popupWidth, popupHeight),
                Location = GetCenteredPopupLocation(
                    popupParent,
                    popupWidth,
                    popupHeight),
                Parent = popupParent,
                BackgroundColor = new Color(38, 35, 32),
                ClipsBounds = false,
                ZIndex = 100
            };

            var closeButton = new StandardButton
            {
                Text = "X",
                Location = new Point(popupWidth - 32, -28),
                Size = new Point(24, 24),
                Parent = _matureConfirmationPanel,
                ClipsBounds = false,
                ZIndex = 10011
            };

            closeButton.Click += (s, e) => CloseMatureConfirmation();

            new Label
            {
                Text =
                    "Enabling this will allow you to view profiles marked as mature/18+. "
                    + "These profiles may contain explicit details not suitable for minors."
                    + Environment.NewLine
                    + Environment.NewLine
                    + "Are you sure you want to continue?",
                Font = GameService.Content.DefaultFont14,
                TextColor = Color.White,
                WrapText = true,
                Location = new Point(16, 6),
                Size = new Point(468, 92),
                Parent = _matureConfirmationPanel
            };

            var yesButton = new StandardButton
            {
                Text = "Show Mature Profiles",
                Location = new Point(200, 108),
                Size = new Point(165, 32),
                Parent = _matureConfirmationPanel
            };

            yesButton.Click += (s, e) =>
            {
                SetMatureProfilesEnabled(true);
                CloseMatureConfirmation();
            };

            var noButton = new StandardButton
            {
                Text = "No",
                Location = new Point(379, 108),
                Size = new Point(105, 32),
                Parent = _matureConfirmationPanel
            };

            noButton.Click += (s, e) => CloseMatureConfirmation();
        }

        private void CloseMatureConfirmation()
        {
            _matureConfirmationPanel?.Dispose();
            _matureConfirmationPanel = null;
        }

        private static Point GetCenteredPopupLocation(
            Container parent,
            int width,
            int height)
        {
            var parentSize = parent?.ContentRegion.Size ?? GameService.Graphics.SpriteScreen.Size;

            const int padding = 8;

            var x = (parentSize.X - width) / 2;
            var y = (parentSize.Y - height) / 2;

            return new Point(Math.Max(padding, x), Math.Max(padding, y));
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
            CloseMatureConfirmation();
            _buildPanel = null;
            StopNoticeRefresh();
            _buttons.Dispose();
            _unwatchBlockedAccountsChanged?.Invoke(OnBlockedAccountsChanged);
            UnwatchServer();
            UnwatchGameState();
        }
    }
}
