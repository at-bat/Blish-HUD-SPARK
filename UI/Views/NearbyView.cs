using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Graphics.UI;
using Microsoft.Xna.Framework;
using rp.spark.Models;
using rp.spark.Services;
using rp.spark.UI.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace rp.spark.UI.Views
{
    // Reusing shared list, text, status color, and async UI helpers from elsewhere in SPARK
    // TODO: Extract more common window behavior to make building new UI windows less of a pain
    public class NearbyView : View
    {
        private const int BodyWidth = 590;
        private const int HeaderY = 38;
        private const int ListY = 62;
        private const int ListHeight = 188;
        private const int StatusY = 258;
        private const int RowHeight = 30;

        private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(15);
        private static readonly TimeSpan RefreshTimeout = TimeSpan.FromSeconds(15);

        private readonly NearbyPresenceService _nearby;
        private readonly SparkSettings _settings;
        private readonly Action<PlayerPresence> _openProfile;
        private readonly SemaphoreSlim _refreshGate = new SemaphoreSlim(1, 1);
        private readonly Action<bool> _setWindowLocked;

        private bool _isUnloaded;
        private CancellationTokenSource _refreshCancellation;
        private Task _autoRefreshTask;
        private Checkbox _showNearbyCheckbox;
        private ProfileScrollList _nearbyList;
        private Label _status;

        public NearbyView(
            NearbyPresenceService nearby,
            SparkSettings settings,
            Action<PlayerPresence> openProfile,
            Action<bool> setWindowLocked)
        {
            _nearby = nearby;
            _settings = settings;
            _openProfile = openProfile;
            _setWindowLocked = setWindowLocked;
        }

        protected override void Build(Container buildPanel)
        {
            _isUnloaded = false;

            var refreshButton = SparkViewUI.AddButton(buildPanel, "Refresh", BodyWidth - 100, 0, 100, 28);
            SparkUiActions.BindClick(
                refreshButton,
                () => RefreshAsync(false),
                SetStatusText,
                "Couldn't refresh nearby players.");

            _showNearbyCheckbox = SparkViewUI.AddCheckbox(
                buildPanel,
                "Show me nearby",
                _settings.ShowNearbyPresence.Value,
                0,
                0,
                170,
                28);

            _showNearbyCheckbox.CheckedChanged += async (s, e) =>
            {
                if (_showNearbyCheckbox == null)
                    return;

                if (_settings.ShowNearbyPresence.Value == _showNearbyCheckbox.Checked)
                    return;

                await SetNearbySharingAsync(_showNearbyCheckbox.Checked);
            };

            var autoRefreshCheckbox = SparkViewUI.AddCheckbox(
                buildPanel,
                "Auto-refresh",
                _settings.AutoRefreshNearbyRpers.Value,
                180,
                0,
                150,
                28);

            autoRefreshCheckbox.CheckedChanged += (s, e) =>
            {
                _settings.AutoRefreshNearbyRpers.Value = autoRefreshCheckbox.Checked;
            };

            var lockWindowCheckbox = SparkViewUI.AddCheckbox(
                buildPanel,
                "Lock position",
                _settings.NearbyWindowLock.Value,
                330,
                0,
                140,
                28);

            lockWindowCheckbox.CheckedChanged += (s, e) =>
            {
                _settings.NearbyWindowLock.Value = lockWindowCheckbox.Checked;
                _setWindowLocked?.Invoke(lockWindowCheckbox.Checked);
            };

            _setWindowLocked?.Invoke(lockWindowCheckbox.Checked);

            AddHeader(buildPanel, "Character", 8, 170);
            AddHeader(buildPanel, "Race", 186, 70);
            AddHeader(buildPanel, "Status", 264, 110);
            AddHeader(buildPanel, "Map IP", 382, 70);
            AddHeader(buildPanel, "Distance", 460, 80);

            _nearbyList = new ProfileScrollList(BodyWidth, ListHeight, RowHeight)
            {
                Location = new Point(0, ListY),
                Parent = buildPanel
            };

            _status = new Label
            {
                Text = string.Empty,
                Font = GameService.Content.DefaultFont12,
                TextColor = SparkViewUI.SecondaryTextColor,
                WrapText = true,
                Location = new Point(0, StatusY),
                Size = new Point(BodyWidth, 42),
                Parent = buildPanel
            };

            WatchSettings();
            SyncShowNearbyCheckboxFromSettings();

            StartRefresh();
            _ = RefreshAsync(true);
        }

        private void WatchSettings()
        {
            _settings.ShowNearbyPresence.SettingChanged +=
                OnShowNearbyPresenceChanged;

            _settings.ShowKnownForInProfileTooltips.SettingChanged +=
                OnTooltipVisibilityChanged;

            _settings.ShowCurrentlyInProfileTooltips.SettingChanged +=
                OnTooltipVisibilityChanged;

            _settings.ShowOocInfoInProfileTooltips.SettingChanged +=
                OnTooltipVisibilityChanged;

            _settings.TrimLongProfileTooltips.SettingChanged +=
                OnTooltipVisibilityChanged;

            _settings.ProfileTooltipLinesPerSection.SettingChanged +=
                OnTooltipLineLimitChanged;
        }

        private void UnwatchSettings()
        {
            _settings.ShowNearbyPresence.SettingChanged -=
                OnShowNearbyPresenceChanged;

            _settings.ShowKnownForInProfileTooltips.SettingChanged -=
                OnTooltipVisibilityChanged;

            _settings.ShowCurrentlyInProfileTooltips.SettingChanged -=
                OnTooltipVisibilityChanged;

            _settings.ShowOocInfoInProfileTooltips.SettingChanged -=
                OnTooltipVisibilityChanged;

            _settings.TrimLongProfileTooltips.SettingChanged -=
                OnTooltipVisibilityChanged;

            _settings.ProfileTooltipLinesPerSection.SettingChanged -=
                OnTooltipLineLimitChanged;
        }

        private void OnShowNearbyPresenceChanged(object sender, ValueChangedEventArgs<bool> e)
        {
            SparkUiThread.Queue(() =>
            {
                if (!_isUnloaded)
                    SyncShowNearbyCheckboxFromSettings();
            });
        }

        private void OnTooltipVisibilityChanged(object sender, ValueChangedEventArgs<bool> e)
        {
            RefreshNearbyTooltips();
        }

        private void OnTooltipLineLimitChanged(object sender, ValueChangedEventArgs<int> e)
        {
            RefreshNearbyTooltips();
        }

        private void RefreshNearbyTooltips()
        {
            SparkUiThread.Queue(() =>
            {
                if (!_isUnloaded && _nearbyList != null)
                    _ = RefreshAsync(false);
            });
        }

        private void SyncShowNearbyCheckboxFromSettings()
        {
            SetChecked(_showNearbyCheckbox, _settings.ShowNearbyPresence.Value);
        }

        private static void SetChecked(Checkbox checkbox, bool value)
        {
            if (checkbox != null && checkbox.Checked != value)
                checkbox.Checked = value;
        }

        private static void AddHeader(Container parent, string text, int x, int width)
        {
            new Label
            {
                Text = text,
                Font = GameService.Content.DefaultFont14,
                TextColor = new Color(255, 233, 180),
                StrokeText = true,
                Location = new Point(x, HeaderY),
                Size = new Point(width, 24),
                Parent = parent
            };
        }

        private async Task SetNearbySharingAsync(bool enabled)
        {
            _settings.ShowNearbyPresence.Value = enabled;

            try
            {
                SetStatusText(enabled ? "Sharing nearby presence..." : "Hiding nearby presence...");

                if (enabled)
                    await _nearby.PublishNowAsync(_refreshCancellation?.Token ?? CancellationToken.None);
                else
                    await _nearby.RemoveAsync(_refreshCancellation?.Token ?? CancellationToken.None);

                SetStatusText(_nearby.LastStatus);

                if (enabled)
                    await RefreshAsync(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch
            {
                SetStatusText(enabled
                    ? "Couldn't share nearby presence."
                    : "Couldn't hide nearby presence.");
            }
        }

        private void StartRefresh()
        {
            StopRefresh();

            _refreshCancellation = new CancellationTokenSource();
            _autoRefreshTask = AutoRefreshAsync(_refreshCancellation.Token);
        }

        private void StopRefresh()
        {
            var cancellation = _refreshCancellation;
            var task = _autoRefreshTask;

            _refreshCancellation = null;
            _autoRefreshTask = null;

            if (cancellation == null)
                return;

            cancellation.Cancel();
            TaskCleanup.DisposeWhenComplete(task, cancellation);
        }

        private async Task AutoRefreshAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(RefreshInterval, cancellationToken);

                    if (_settings.AutoRefreshNearbyRpers.Value)
                        await RefreshAsync(false, cancellationToken);
                }
                catch (OperationCanceledException)
                    when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
            }
        }

        private Task RefreshAsync(bool resetScroll)
        {
            return RefreshAsync(
                resetScroll,
                _refreshCancellation?.Token ?? CancellationToken.None);
        }

        private async Task RefreshAsync(bool resetScroll, CancellationToken cancellationToken)
        {
            if (_isUnloaded || _nearbyList == null || cancellationToken.IsCancellationRequested)
                return;

            var hasRefreshLock = false;

            try
            {
                hasRefreshLock = await _refreshGate.WaitAsync(0, cancellationToken);
                if (!hasRefreshLock)
                    return;

                SetStatusText("Refreshing nearby players...");

                IReadOnlyList<NearbyPresence> rows;
                string sharingNotice;

                using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    timeout.CancelAfter(RefreshTimeout);
                    sharingNotice = await _nearby.GetSharingNoticeAsync(timeout.Token);
                    rows = await _nearby.SearchAsync(timeout.Token);
                }

                SparkUiThread.Queue(() =>
                {
                    if (_isUnloaded || _nearbyList == null)
                        return;

                    ShowRows(rows, resetScroll, sharingNotice);
                });
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested || _isUnloaded)
            {
            }
            catch
            {
                SparkUiThread.Queue(() =>
                {
                    if (_isUnloaded || _nearbyList == null)
                        return;

                    _nearbyList.ShowEmptyMessage("Could not load nearby players.");
                    SetStatusText("Nearby list unavailable.");
                });
            }
            finally
            {
                if (hasRefreshLock)
                    _refreshGate.Release();
            }
        }

        private void ShowRows(IReadOnlyList<NearbyPresence> nearbyRows, bool resetScroll, string sharingNotice)
        {
            _nearbyList.ClearRows(resetScroll);

            var rows = (nearbyRows ?? new List<NearbyPresence>())
                .Where(row => row?.Presence != null)
                .Where(row => row.Presence.Status != RPStatus.Invisible)
                .OrderBy(row => _nearby.IsCurrentMapIp(row) ? 0 : 1)
                .ThenBy(row => row.DistanceMeters < 0 ? double.MaxValue : row.DistanceMeters)
                .ThenBy(row => MapIpText(row.ServerAddress), StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.VisibleName(), StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (rows.Count == 0)
            {
                _nearbyList.ShowEmptyMessage("No nearby players found.");
                SetStatusText(WithSharingNotice(
                    _settings.AutoRefreshNearbyRpers.Value
                        ? "0 nearby players."
                        : "0 nearby players. Auto-refresh is off.",
                    sharingNotice));
                return;
            }

            for (var index = 0; index < rows.Count; index++)
                AddRow(rows[index], index);

            if (resetScroll)
                _nearbyList.ResetScroll();

            SetStatusText(WithSharingNotice(
                rows.Count == 1
                    ? "1 nearby player."
                    : $"{rows.Count} nearby players.",
                sharingNotice));
        }

        private void AddRow(NearbyPresence nearby, int index)
        {
            var presence = nearby?.Presence ?? new PlayerPresence();
            var row = _nearbyList.AddRow(index, string.Empty);
            var sameMapIp = _nearby.IsCurrentMapIp(nearby);
            var secondary = SparkViewUI.SecondaryTextColor;
            var mapIpColor = sameMapIp ? new Color(140, 220, 140) : secondary;
            var distanceText = sameMapIp ? DistanceText(nearby?.DistanceMeters ?? -1) : "-";

            _nearbyList.AddCell(row, presence.VisibleName(), 8, 5, 170, Color.White);
            _nearbyList.AddCell(row, ProfileText.PresenceRace(presence), 186, 5, 70, secondary);
            _nearbyList.AddCell(row, ProfileLabels.StatusLabel(presence.Status), 264, 5, 110, ProfileStatusColors.Get(presence.Status));
            _nearbyList.AddCell(row, MapIpText(nearby?.ServerAddress), 382, 5, 70, mapIpColor);
            _nearbyList.AddCell(row, distanceText, 460, 5, 80, secondary);

            ProfileScrollList.AddInteractionLayer(
                row,
                MakeTooltip(nearby),
                () => _openProfile?.Invoke(presence));
        }

        private Tooltip MakeTooltip(NearbyPresence nearby)
        {
            var presence = nearby?.Presence ?? new PlayerPresence();

            var showKnownFor =
                _settings?.ShowKnownForInProfileTooltips.Value ?? true;

            var showCurrently =
                _settings?.ShowCurrentlyInProfileTooltips.Value ?? true;

            var showOutOfCharacter =
                _settings?.ShowOocInfoInProfileTooltips.Value ?? true;

            var trimLongTooltips =
                _settings?.TrimLongProfileTooltips.Value ?? true;

            var maximumLinesPerSection =
                _settings?.ProfileTooltipLinesPerSection.Value ?? 12;

            return new Tooltip(new ProfilePresenceTooltipView(
                presence.VisibleName(),
                ProfileText.PresenceCharacterDetails(presence),
                ProfileLabels.StatusLabel(presence.Status),
                ProfileText.PresenceLocation(presence),
                presence.KnownFor,
                presence.Currently,
                presence.OutOfCharacterInfo,
                showKnownFor,
                showCurrently,
                showOutOfCharacter,
                trimLongTooltips,
                maximumLinesPerSection,
                new[]
                {
            $"Distance: {DistanceText(nearby?.DistanceMeters ?? -1)}"
                }));
        }

        private static string DistanceText(double meters)
        {
            if (meters < 0)
                return "-";

            if (meters >= 1000)
                return $"{meters / 1000d:0.0}km";

            return $"{Math.Round(meters):0}m";
        }

        private static string MapIpText(string serverAddress)
        {
            var text = serverAddress?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text))
                return "-";

            var parts = text.Split('.');
            var lastPart = parts.Length > 0 ? parts[parts.Length - 1].Trim() : text;

            if (string.IsNullOrWhiteSpace(lastPart))
                return "-";

            return lastPart.Length <= 3
                ? lastPart
                : lastPart.Substring(lastPart.Length - 3);
        }

        private void SetStatusText(string text)
        {
            if (_status == null || _isUnloaded)
                return;

            SparkUiThread.Queue(() =>
            {
                if (_status != null && !_isUnloaded)
                    _status.Text = text ?? string.Empty;
            });
        }

        private static string WithSharingNotice(string status, string sharingNotice)
        {
            if (string.IsNullOrWhiteSpace(sharingNotice))
                return status ?? string.Empty;

            if (string.IsNullOrWhiteSpace(status))
                return sharingNotice.Trim();

            return $"{status} {sharingNotice.Trim()}";
        }

        protected override void Unload()
        {
            _isUnloaded = true;
            UnwatchSettings();
            StopRefresh();

            _showNearbyCheckbox = null;
        }
    }
}