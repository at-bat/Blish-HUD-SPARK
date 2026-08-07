using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Graphics.UI;
using Microsoft.Xna.Framework;
using rp.spark.Services;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace rp.spark.UI.Views
{
    public class SparkSettingsView : View
    {
        private const string SparkUrl = "https://getspark.fyi";
        private const int ContentWidth = 660;
        private const int ControlHeight = 30;
        private const int StatusLineHeight = 24;
        private const int RowGap = 8;
        private const int LeftPadding = 8;

        private static readonly TimeSpan NoticeRefreshInterval = TimeSpan.FromMilliseconds(500);

        private readonly Action<Control> _showMenu;
        private readonly Func<ServerSyncStatus> _getServerSyncStatus;
        private readonly Action<Action<ServerSyncStatus>> _watchServerSyncStatus;
        private readonly Action<Action<ServerSyncStatus>> _unwatchServerSyncStatus;
        private readonly Func<string> _getImportantNotice;

        private bool _isUnloaded;
        private Label _serverStatusLabel;
        private Label _readinessLabel;
        private CancellationTokenSource _noticeRefreshCancel;
        private Task _noticeRefreshTask;

        public SparkSettingsView(
            Action<Control> showMenu,
            Func<ServerSyncStatus> getServerSyncStatus,
            Action<Action<ServerSyncStatus>> watchServerSyncStatus,
            Action<Action<ServerSyncStatus>> unwatchServerSyncStatus,
            Func<string> getImportantNotice)
        {
            _showMenu = showMenu;
            _getServerSyncStatus = getServerSyncStatus;
            _watchServerSyncStatus = watchServerSyncStatus;
            _unwatchServerSyncStatus = unwatchServerSyncStatus;
            _getImportantNotice = getImportantNotice;
        }

        protected override void Build(Container buildPanel)
        {
            _isUnloaded = false;
            BuildSettings(buildPanel);
            WatchServer();
            StartNoticeRefresh();
            RefreshServerStatus();
        }

        private void BuildSettings(Container buildPanel)
        {
            var settingsStack = SparkFormLayout.AddAutoStack(buildPanel, ContentWidth, 6);
            settingsStack.Left = LeftPadding;

            BuildServerStatus(settingsStack);
            BuildReadinessNotice(settingsStack);

            var menuHint = SparkFormLayout.AddLabel(
                settingsStack,
                "Use the SPARK Menu below to access profiles, player lists, RP tools, privacy controls, and settings.",
                ContentWidth,
                30,
                GameService.Content.DefaultFont14,
                SparkViewUI.SecondaryTextColor);

            menuHint.WrapText = true;

            var actions = SparkFormLayout.AddRow(
                settingsStack,
                ContentWidth,
                ControlHeight,
                RowGap);

            var menuButton = SparkFormLayout.AddButton(
                actions,
                "Open SPARK Menu",
                150,
                ControlHeight);

            menuButton.BasicTooltipText = "Open the SPARK menu (also available from the corner icon if enabled).";
            menuButton.Click += (s, e) => _showMenu?.Invoke(menuButton);

            var documentationButton = SparkFormLayout.AddButton(
                actions,
                "Documentation",
                130,
                ControlHeight);

            documentationButton.BasicTooltipText = $"Opens a browser page for {SparkUrl}.";
            documentationButton.Click += (s, e) => OpenDocumentation();
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

        private static void OpenDocumentation()
        {
            try
            {
                Process.Start(new ProcessStartInfo(SparkUrl)
                {
                    UseShellExecute = true
                });
            }
            catch
            {
                ScreenNotification.ShowNotification(
                    "Couldn't open the SPARK documentation.",
                    ScreenNotification.NotificationType.Error);
            }
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

            _serverStatusLabel.Text = ServerStatusText(
                _getServerSyncStatus?.Invoke() ?? status);

            RefreshReadinessNotice();
        }

        private void StartNoticeRefresh()
        {
            StopNoticeRefresh();

            _noticeRefreshCancel = new CancellationTokenSource();
            _noticeRefreshTask = RefreshNoticeLoopAsync(
                _noticeRefreshCancel.Token);
        }

        private async Task RefreshNoticeLoopAsync(
            CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(
                        NoticeRefreshInterval,
                        cancellationToken);
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
            UnwatchServer();
            _serverStatusLabel = null;
            _readinessLabel = null;
        }
    }
}