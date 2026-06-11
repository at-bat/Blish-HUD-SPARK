using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Graphics.UI;
using rp.spark.Models;
using rp.spark.Services;
using System;

namespace rp.spark.UI.Views
{
    public class SparkSettingsView : View
    {
        private const int ContentWidth = 660;
        private const int ApiKeyWarningHeight = 44;

        private readonly SparkSettings _settings;
        private readonly Func<ServerSyncStatus> _getServerSyncStatus;
        private readonly Action<Action<ServerSyncStatus>> _watchServerSyncStatus;
        private readonly Action<Action<ServerSyncStatus>> _unwatchServerSyncStatus;
        private readonly Action _requestServerSync;
        private readonly Func<bool> _hasValidApiKey;
        private readonly SparkSettingsButtons _buttons;
        private readonly SparkStatusMessage _statusMessage;
        private readonly SparkBlocklist _blocklist;

        private bool _isUnloaded;
        private Label _serverStatusLabel;
        private Label _apiKeyWarning;

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
            Func<bool> hasValidApiKey,
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
            _hasValidApiKey = hasValidApiKey;
            _buttons = new SparkSettingsButtons(
                openProfileManager,
                openProfileViewer,
                openOnlineList,
                openSavedProfiles,
                openAbout,
                waitForInitialState,
                getCurrentStateMessage,
                requestStateRefresh);
            _statusMessage = new SparkStatusMessage(settings, requestServerSync);
            _blocklist = new SparkBlocklist(
                settings,
                blockAccount,
                unblockAccount,
                watchBlockedAccountsChanged,
                unwatchBlockedAccountsChanged);
        }

        protected override void Build(Container buildPanel)
        {
            _buttons.Build(buildPanel);
            BuildSettings(buildPanel);
            WatchServer();
            RefreshServerStatus();
        }

        private void BuildSettings(Container buildPanel)
        {
            var settingsStack = SparkFormLayout.AddVerticalStack(
                buildPanel,
                0,
                130,
                ContentWidth,
                620,
                10);

            BuildApiKeyWarning(settingsStack);
            BuildServerStatus(settingsStack);
            BuildSharing(settingsStack);
            _statusMessage.Build(settingsStack, ContentWidth);
            BuildDiscovery(settingsStack);
            SparkFormLayout.AddSpacer(settingsStack, ContentWidth, 5);
            _blocklist.Build(settingsStack, ContentWidth);
        }

        private void BuildApiKeyWarning(FlowPanel settingsStack)
        {
            _apiKeyWarning = SparkFormLayout.AddLabel(
                settingsStack,
                string.Empty,
                ContentWidth,
                ApiKeyWarningHeight,
                GameService.Content.DefaultFont16,
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

        private void BuildSharing(FlowPanel settingsStack)
        {
            var broadcastCheckbox = SparkFormLayout.AddCheckbox(
                settingsStack,
                "Share my profile online",
                _settings.BroadcastProfile.Value,
                260);

            broadcastCheckbox.CheckedChanged += (s, e) => _settings.BroadcastProfile.Value = broadcastCheckbox.Checked;
            broadcastCheckbox.CheckedChanged += (s, e) => _requestServerSync?.Invoke();

            var hideLocationCheckbox = SparkFormLayout.AddCheckbox(
                settingsStack,
                "Hide my location",
                _settings.HideLocation.Value,
                260);

            hideLocationCheckbox.CheckedChanged += (s, e) => _settings.HideLocation.Value = hideLocationCheckbox.Checked;
            hideLocationCheckbox.CheckedChanged += (s, e) => _requestServerSync?.Invoke();

            var currentStatus = _settings.CurrentStatus.Value == RPStatus.Offline
                ? RPStatus.Online
                : _settings.CurrentStatus.Value;

            var statusRow = SparkFormLayout.AddRow(settingsStack, ContentWidth, 35, 10);
            SparkFormLayout.AddLabel(statusRow, "Status:", 60, 35);
            var statusDropdown = SparkFormLayout.AddDropdown(
                statusRow,
                ProfileLabels.RpStatusOptions,
                ProfileLabels.StatusLabel(currentStatus),
                180);

            statusDropdown.ValueChanged += (s, e) =>
            {
                _settings.CurrentStatus.Value = ProfileLabels.ParseStatus(statusDropdown.SelectedItem?.ToString());
                _requestServerSync?.Invoke();
            };
        }

        private void BuildDiscovery(FlowPanel settingsStack)
        {
            var regionRow = SparkFormLayout.AddRow(settingsStack, ContentWidth, 35, 5);
            SparkFormLayout.AddLabel(regionRow, "Region Filter:", 100, 35);

            var regionDropdown = SparkFormLayout.AddDropdown(
                regionRow,
                new[] { ProfileRegion.NA.ToString(), ProfileRegion.EU.ToString() },
                _settings.RegionFilter.Value.ToString(),
                160);

            regionDropdown.ValueChanged += (s, e) =>
            {
                if (Enum.TryParse(regionDropdown.SelectedItem?.ToString(), out ProfileRegion selectedRegion))
                {
                    _settings.RegionFilter.Value = selectedRegion;
                    _requestServerSync?.Invoke();
                }
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

        private void RefreshApiKeyWarning()
        {
            if (_apiKeyWarning == null)
                return;

            var showWarning = !HasValidApiKey();
            _apiKeyWarning.Text = showWarning ? SparkViewUI.MissingApiKeyWarning : string.Empty;
            _apiKeyWarning.Height = showWarning ? ApiKeyWarningHeight : 0;
            _apiKeyWarning.Visible = showWarning;
        }

        private bool HasValidApiKey()
        {
            try
            {
                return _hasValidApiKey?.Invoke() == true;
            }
            catch
            {
                return false;
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
            _buttons.Dispose();
            _blocklist.Dispose();
            UnwatchServer();
        }
    }
}
