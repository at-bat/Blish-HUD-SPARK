using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Graphics.UI;
using Microsoft.Xna.Framework;
using rp.spark.Models;
using rp.spark.UI.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace rp.spark.UI.Views
{
    public class OnlineProfilesView : View
    {
        private const int RowHeight = 36;
        private const string SearchStatus = "Status";
        private const string SearchLocation = "Location";
        private const string SortStatus = "Status";
        private const string SortLocation = "Location";

        // The open online list refreshes periodically; players can still refresh manually if they want it sooner
        // Server-side throttling may be added depending on performance.
        private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(30);

        private readonly Func<Task<IReadOnlyList<PlayerPresence>>> _loadRows;
        private readonly Func<IReadOnlyList<PlayerPresence>> _loadCachedRows;
        private readonly Action<PlayerPresence> _openProfile;
        private readonly Func<PlayerPresence, bool> _isBookmarked;
        private readonly Action<Action> _watchBookmarks;
        private readonly Action<Action> _unwatchBookmarks;
        private readonly SemaphoreSlim _refreshGate = new SemaphoreSlim(1, 1);

        private bool _isUnloaded;
        private CancellationTokenSource _autoRefreshCancellation;
        private Task _autoRefreshWorker;
        private IReadOnlyList<PlayerPresence> _rows = new List<PlayerPresence>();
        private TextBox _searchBox;
        private Dropdown _searchFieldDropdown;
        private Dropdown _sortDropdown;
        private ProfileScrollList _profileList;
        private Label _status;

        public OnlineProfilesView(
            Func<Task<IReadOnlyList<PlayerPresence>>> getPresenceRows,
            Func<IReadOnlyList<PlayerPresence>> getCachedPresenceRows,
            Action<PlayerPresence> openProfile,
            Func<PlayerPresence, bool> isBookmarked = null,
            Action<Action> watchBookmarksChanged = null,
            Action<Action> unwatchBookmarksChanged = null)
        {
            _loadRows = getPresenceRows;
            _loadCachedRows = getCachedPresenceRows;
            _openProfile = openProfile;
            _isBookmarked = isBookmarked;
            _watchBookmarks = watchBookmarksChanged;
            _unwatchBookmarks = unwatchBookmarksChanged;
        }

        protected override void Build(Container buildPanel)
        {
            ProfileListViewUI.AddTitle(buildPanel, "Online Profiles", 300);
            var refreshButton = ProfileListViewUI.AddRefreshButton(buildPanel);
            SparkUiActions.BindClick(
                refreshButton,
                () => RefreshAsync(true),
                SetStatusText,
                "Couldn't refresh online profiles.");

            BuildSearchControls(buildPanel);
            BuildHeader(buildPanel);

            _profileList = new ProfileScrollList(ProfileListViewUI.BodyWidth, ProfileListViewUI.ListHeight, RowHeight)
            {
                Location = new Point(0, ProfileListViewUI.ListY),
                Parent = buildPanel
            };

            _status = ProfileListViewUI.AddStatusLabel(buildPanel);

            _ = RefreshAsync(true);
            _watchBookmarks?.Invoke(HandleBookmarksChanged);
            StartRefresh();
        }

        private static void BuildHeader(Container parent)
        {
            ProfileListViewUI.AddHeader(parent, "Character", 28, 165);
            ProfileListViewUI.AddHeader(parent, "Race", 200, 95);
            ProfileListViewUI.AddHeader(parent, "Account", 305, 140);
            ProfileListViewUI.AddHeader(parent, "Status", 455, 105);
            ProfileListViewUI.AddHeader(parent, "Location", 570, 180);
        }

        private void BuildSearchControls(Container parent)
        {
            var controls = ProfileListViewUI.AddSearchControls(
                parent,
                "Search online profiles",
                new[]
                {
                    ProfileListViewUI.SearchAllFields,
                    ProfileListViewUI.SearchName,
                    ProfileListViewUI.SearchAccount,
                    ProfileListViewUI.SearchRace,
                    ProfileListViewUI.SearchProfession,
                    SearchStatus,
                    SearchLocation
                },
                new[]
                {
                    ProfileListViewUI.SortName,
                    ProfileListViewUI.SortRecentlySeen,
                    ProfileListViewUI.SortRace,
                    ProfileListViewUI.SortAccount,
                    SortStatus,
                    SortLocation
                },
                ProfileListViewUI.SortName,
                () => RefreshVisibleRows(true));

            _searchBox = controls.SearchBox;
            _searchFieldDropdown = controls.SearchFieldDropdown;
            _sortDropdown = controls.SortDropdown;
        }

        private void HandleBookmarksChanged()
        {
            GameService.Overlay.QueueMainThreadUpdate(gameTime => RefreshVisibleRows(false));
        }

        private void StartRefresh()
        {
            StopRefresh();
            _autoRefreshCancellation = new CancellationTokenSource();
            _autoRefreshWorker = AutoRefreshAsync(_autoRefreshCancellation.Token);
        }

        private void StopRefresh()
        {
            var cancellation = _autoRefreshCancellation;
            var worker = _autoRefreshWorker;
            _autoRefreshCancellation = null;
            _autoRefreshWorker = null;

            if (cancellation == null)
                return;

            cancellation.Cancel();
            TaskCleanup.DisposeWhenComplete(worker, cancellation);
        }

        private async Task AutoRefreshAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(RefreshInterval, cancellationToken);
                    await RefreshAsync(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
            }
        }

        private async Task RefreshAsync(bool resetScroll)
        {
            if (_isUnloaded || _profileList == null)
                return;

            if (!await _refreshGate.WaitAsync(0))
                return;

            var showedCachedRows = false;

            try
            {
                if (_isUnloaded || _profileList == null)
                    return;

                showedCachedRows = ShowCachedRows(resetScroll);
                SetStatusText(showedCachedRows ? "Refreshing profiles..." : "Loading profiles...");

                var rows = _loadRows == null
                    ? new List<PlayerPresence>()
                    : await _loadRows();

                if (_isUnloaded)
                    return;

                GameService.Overlay.QueueMainThreadUpdate(gameTime =>
                {
                    if (_profileList == null || _status == null || _isUnloaded)
                        return;

                    _rows = rows ?? new List<PlayerPresence>();
                    RefreshVisibleRows(resetScroll);
                });
            }
            catch
            {
                if (_isUnloaded)
                    return;

                GameService.Overlay.QueueMainThreadUpdate(gameTime =>
                {
                    if (_profileList == null || _status == null || _isUnloaded)
                        return;

                    if (showedCachedRows)
                    {
                        _status.Text = "Showing local profile. Server list unavailable.";
                        return;
                    }

                    _profileList.ShowEmptyMessage("Could not load online profiles.");
                    _status.Text = "Server list unavailable.";
                });
            }
            finally
            {
                _refreshGate.Release();
            }
        }
        // Adding cache to online list so previous entries will remain when reopening view until refresh replaces them per feedback
        private bool ShowCachedRows(bool resetScroll)
        {
            if (_loadCachedRows == null || _isUnloaded || _profileList == null)
                return false;

            IReadOnlyList<PlayerPresence> cachedRows;

            try
            {
                cachedRows = _loadCachedRows();
            }
            catch
            {
                return false;
            }

            if (cachedRows == null || cachedRows.Count == 0)
                return false;

            GameService.Overlay.QueueMainThreadUpdate(gameTime =>
            {
                if (_profileList == null || _status == null || _isUnloaded)
                    return;

                _rows = cachedRows;
                RefreshVisibleRows(resetScroll);
            });

            return true;
        }

        private void ShowRows(IReadOnlyList<PlayerPresence> presenceRows, bool resetScroll)
        {
            if (_profileList == null || _status == null || _isUnloaded)
                return;

            _profileList.ClearRows();

            var filteredRows = (presenceRows ?? new List<PlayerPresence>())
                .Where(row => row != null && row.Status != RPStatus.Invisible)
                .Where(MatchesSearch);

            var rows = SortRows(filteredRows).ToList();

            if (!rows.Any())
            {
                _profileList.ShowEmptyMessage(GetEmptyMessage());
                _status.Text = "0 matching profiles.";
                return;
            }

            for (var i = 0; i < rows.Count; i++)
                AddRow(rows[i], i);

            if (resetScroll)
                _profileList.ResetScroll();

            var visibleRows = rows.Count == 1 ? "1 visible profile" : $"{rows.Count} visible profiles";
            var searchSuffix = ProfileListViewUI.GetSearchSuffix(_searchBox);
            _status.Text = $"{visibleRows}{searchSuffix}.";
        }

        private void RefreshVisibleRows(bool resetScroll)
        {
            ShowRows(_rows, resetScroll);
        }

        private void AddRow(PlayerPresence presence, int index)
        {
            var tooltipText = TooltipText(presence);
            var row = _profileList.AddRow(index, tooltipText);

            MakeClickable(row, presence, tooltipText);

            AddBookmarkMarker(row, presence, tooltipText);
            MakeClickable(_profileList.AddCell(row, presence.VisibleName(), 30, 7, 163, Color.White), presence, tooltipText);
            MakeClickable(_profileList.AddCell(row, ProfileText.PresenceRace(presence), 200, 7, 95, new Color(220, 220, 220)), presence, tooltipText);
            MakeClickable(_profileList.AddCell(row, presence.AccountName, 305, 7, 140, new Color(220, 220, 220)), presence, tooltipText);
            MakeClickable(_profileList.AddCell(row, ProfileLabels.StatusLabel(presence.Status), 455, 7, 105, GetStatusColor(presence.Status)), presence, tooltipText);
            MakeClickable(_profileList.AddCell(row, ProfileText.PresenceLocation(presence), 570, 7, 180, new Color(220, 220, 220)), presence, tooltipText);
        }

        private void AddBookmarkMarker(Container row, PlayerPresence presence, string tooltipText)
        {
            if (_isBookmarked?.Invoke(presence) != true)
                return;

            var marker = ProfileListViewUI.AddBookmarkMarker(row);
            MakeClickable(marker, presence, tooltipText);
        }

        private void MakeClickable(Control control, PlayerPresence presence, string tooltipText)
        {
            ProfileScrollList.WireInteraction(control, tooltipText, () => _openProfile?.Invoke(presence));
        }

        private bool MatchesSearch(PlayerPresence presence)
        {
            var query = _searchBox?.Text?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(query))
                return true;

            return GetSearchText(presence)
                .IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private string GetSearchText(PlayerPresence presence)
        {
            var searchField = _searchFieldDropdown?.SelectedItem?.ToString() ?? ProfileListViewUI.SearchAllFields;

            switch (searchField)
            {
                case ProfileListViewUI.SearchName:
                    return ProfileText.JoinSearchText(presence?.VisibleName(), presence?.ActiveProfileName);
                case ProfileListViewUI.SearchAccount:
                    return presence?.AccountName ?? string.Empty;
                case ProfileListViewUI.SearchRace:
                    return ProfileText.PresenceRace(presence);
                case ProfileListViewUI.SearchProfession:
                    return presence?.VisibleProfession() ?? string.Empty;
                case SearchStatus:
                    return ProfileLabels.StatusLabel(presence?.Status ?? RPStatus.Offline);
                case SearchLocation:
                    return ProfileText.PresenceLocation(presence);
                default:
                    return ProfileText.JoinSearchText(
                        presence?.VisibleName(),
                        presence?.ActiveProfileName,
                        presence?.AccountName,
                        ProfileText.PresenceRace(presence),
                        presence?.VisibleProfession(),
                        ProfileLabels.StatusLabel(presence?.Status ?? RPStatus.Offline),
                        ProfileText.PresenceLocation(presence));
            }
        }

        private IEnumerable<PlayerPresence> SortRows(IEnumerable<PlayerPresence> rows)
        {
            var sortMode = _sortDropdown?.SelectedItem?.ToString() ?? ProfileListViewUI.SortName;

            switch (sortMode)
            {
                case ProfileListViewUI.SortRecentlySeen:
                    return rows
                        .OrderByDescending(row => row.LastSeen)
                        .ThenBy(row => row.VisibleName(), StringComparer.OrdinalIgnoreCase);
                case ProfileListViewUI.SortRace:
                    return rows
                        .OrderBy(row => ProfileText.PresenceRace(row), StringComparer.OrdinalIgnoreCase)
                        .ThenBy(row => row.VisibleName(), StringComparer.OrdinalIgnoreCase);
                case ProfileListViewUI.SortAccount:
                    return rows
                        .OrderBy(row => row.AccountName, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(row => row.VisibleName(), StringComparer.OrdinalIgnoreCase);
                case SortStatus:
                    return rows
                        .OrderBy(row => ProfileLabels.StatusLabel(row.Status), StringComparer.OrdinalIgnoreCase)
                        .ThenBy(row => row.VisibleName(), StringComparer.OrdinalIgnoreCase);
                case SortLocation:
                    return rows
                        .OrderBy(ProfileText.PresenceLocation, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(row => row.VisibleName(), StringComparer.OrdinalIgnoreCase);
                default:
                    return rows
                        .OrderBy(row => row.VisibleName(), StringComparer.OrdinalIgnoreCase)
                        .ThenBy(row => row.AccountName, StringComparer.OrdinalIgnoreCase);
            }
        }

        private string GetEmptyMessage()
        {
            return string.IsNullOrWhiteSpace(_searchBox?.Text)
                ? "No visible profiles yet."
                : "No online profiles match this search.";
        }

        private static string TooltipText(PlayerPresence presence)
        {
            var lines = new List<string>
            {
                presence.VisibleName(),
                ProfileText.PresenceCharacterDetails(presence),
                $"Status: {ProfileLabels.StatusLabel(presence.Status)}",
                $"Location: {ProfileText.PresenceLocation(presence)}"
            };

            if (!string.IsNullOrWhiteSpace(presence.StatusMessage))
                lines.Add($"Status message: {presence.StatusMessage.Trim()}");

            if (!string.IsNullOrWhiteSpace(presence.Currently))
            {
                lines.Add("----------------");
                lines.Add($"Currently: {presence.Currently.Trim()}");
            }

            if (!string.IsNullOrWhiteSpace(presence.OutOfCharacterInfo))
            {
                lines.Add("----------------");
                lines.Add(presence.OutOfCharacterInfo.Trim());
            }

            return string.Join(Environment.NewLine, lines.Where(line => !string.IsNullOrWhiteSpace(line)));
        }

        private static Color GetStatusColor(RPStatus status)
        {
            switch (status)
            {
                case RPStatus.Looking:
                    return new Color(160, 255, 180);
                case RPStatus.Busy:
                    return new Color(255, 210, 120);
                case RPStatus.Offline:
                    return new Color(160, 160, 160);
                default:
                    return new Color(220, 220, 220);
            }
        }

        protected override void Unload()
        {
            _isUnloaded = true;
            _unwatchBookmarks?.Invoke(HandleBookmarksChanged);
            StopRefresh();
        }

        private void SetStatusText(string text)
        {
            if (_status == null || _isUnloaded)
                return;

            GameService.Overlay.QueueMainThreadUpdate(gameTime =>
            {
                if (_status != null && !_isUnloaded)
                    _status.Text = text ?? string.Empty;
            });
        }

    }
}
