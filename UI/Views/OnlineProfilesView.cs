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
        private static readonly TimeSpan RefreshTimeout = TimeSpan.FromSeconds(15);
        private readonly Func<bool> _isAutoRefreshEnabled;
        private readonly Action<bool> _setAutoRefreshEnabled;
        private readonly SparkSettings _settings;

        private readonly PageList _page = new PageList();
        private PageListControls _pageControls;

        private readonly Func<CancellationToken, Task<IReadOnlyList<PlayerPresence>>> _loadRows;
        private readonly Func<IReadOnlyList<PlayerPresence>> _loadCachedRows;
        private readonly Action<PlayerPresence> _openProfile;
        private readonly Func<PlayerPresence, bool> _isBookmarked;
        private readonly Action<Action> _watchBookmarks;
        private readonly Action<Action> _unwatchBookmarks;
        private readonly SemaphoreSlim _refreshGate = new SemaphoreSlim(1, 1);

        private bool _isUnloaded;
        private CancellationTokenSource _refreshCancellation;
        private IReadOnlyList<PlayerPresence> _rows = new List<PlayerPresence>();
        private TextBox _searchBox;
        private Dropdown _searchFieldDropdown;
        private Dropdown _sortDropdown;
        private ProfileFilterMenu _discoveryFilters;
        private ProfileScrollList _profileList;
        private Label _status;

        public OnlineProfilesView(
            Func<CancellationToken, Task<IReadOnlyList<PlayerPresence>>> getPresenceRows,
            Func<IReadOnlyList<PlayerPresence>> getCachedPresenceRows,
            Action<PlayerPresence> openProfile,
            Func<PlayerPresence, bool> isBookmarked = null,
            Action<Action> watchBookmarksChanged = null,
            Action<Action> unwatchBookmarksChanged = null,
            Func<bool> isAutoRefreshEnabled = null,
            Action<bool> setAutoRefreshEnabled = null,
            SparkSettings settings = null)
        {
            _loadRows = getPresenceRows;
            _loadCachedRows = getCachedPresenceRows;
            _openProfile = openProfile;
            _isBookmarked = isBookmarked;
            _watchBookmarks = watchBookmarksChanged;
            _unwatchBookmarks = unwatchBookmarksChanged;
            _isAutoRefreshEnabled = isAutoRefreshEnabled;
            _setAutoRefreshEnabled = setAutoRefreshEnabled;
            _settings = settings;
        }

        protected override void Build(Container buildPanel)
        {
            _isUnloaded = false;
            WatchTooltipSettings();

            ProfileListViewUI.AddTitle(buildPanel, "Online Profiles", 300);
            var refreshButton = ProfileListViewUI.AddRefreshButton(buildPanel);
            SparkUiActions.BindClick(
                refreshButton,
                () => RefreshAsync(false),
                SetStatusText,
                "Couldn't refresh online profiles.");

            BuildSearchControls(buildPanel);
            BuildHeader(buildPanel);

            _profileList = new ProfileScrollList(ProfileListViewUI.BodyWidth, ProfileListViewUI.ListHeight, RowHeight)
            {
                Location = new Point(0, ProfileListViewUI.ListY),
                Parent = buildPanel
            };

            var autoRefreshCheckbox = new Checkbox
            {
                Text = "Auto-refresh",
                Checked = IsAutoRefreshEnabled(),
                Location = new Point(510, 1),
                Size = new Point(130, 28),
                Parent = buildPanel
            };

            autoRefreshCheckbox.CheckedChanged += (s, e) =>
            {
                _setAutoRefreshEnabled?.Invoke(autoRefreshCheckbox.Checked);
                RefreshVisibleRows(false);
            };

            _pageControls = new PageListControls(
                buildPanel,
                _page,
                ProfileListViewUI.BodyWidth,
                () => RefreshVisibleRows(false))
            {
                Location = new Point(0, ProfileListViewUI.PageY)
            };

            _status = ProfileListViewUI.AddStatusLabel(buildPanel);

            _watchBookmarks?.Invoke(HandleBookmarksChanged);
            StartRefresh();
            _ = RefreshAsync(true);
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

            _discoveryFilters = new ProfileFilterMenu(
                parent,
                () => RefreshVisibleRows(true));
        }

        private void HandleBookmarksChanged()
        {
            SparkUiThread.Queue(() => RefreshVisibleRows(false));
        }

        private void StartRefresh()
        {
            StopRefresh();
            _refreshCancellation = new CancellationTokenSource();
            _ = AutoRefreshAsync(_refreshCancellation.Token);
        }

        private void StopRefresh()
        {
            var cancellation = _refreshCancellation;
            _refreshCancellation = null;

            cancellation?.Cancel();
        }

        private async Task AutoRefreshAsync(
            CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(
                        RefreshInterval,
                        cancellationToken);

                    if (IsAutoRefreshEnabled())
                        await RefreshAsync(false, cancellationToken);
                }
                catch (OperationCanceledException)
                    when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
            }
        }

        private bool IsAutoRefreshEnabled()
        {
            return _isAutoRefreshEnabled?.Invoke() ?? true;
        }

        // Renamed resetScroll -> resetPage since we have pagination now
        private Task RefreshAsync(bool resetPage)
        {
            return RefreshAsync(
                resetPage,
                _refreshCancellation?.Token ?? CancellationToken.None);
        }

        private async Task RefreshAsync(
            bool resetPage,
            CancellationToken cancellationToken)
        {
            if (_isUnloaded
                || _profileList == null
                || cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var hasRefreshLock = false;
            var showedCachedRows = false;
            var hadExistingRows = _rows != null && _rows.Count > 0;

            try
            {
                hasRefreshLock = await _refreshGate.WaitAsync(
                    0,
                    cancellationToken);

                if (!hasRefreshLock)
                    return;

                if (_isUnloaded
                    || _profileList == null
                    || cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                // Fixing previous bug due to UX when first loading.
                // Only show the fallback when opening an empty list, if it contains anything but yourself, don't fallback.
                if (!hadExistingRows)
                    showedCachedRows = ShowCachedRows(resetPage);

                SetStatusText(
                    hadExistingRows || showedCachedRows
                        ? "Refreshing profiles..."
                        : "Loading profiles...");

                IReadOnlyList<PlayerPresence> rows;

                using (var refreshTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    refreshTimeout.CancelAfter(RefreshTimeout);

                    rows = _loadRows == null
                        ? new List<PlayerPresence>()
                        : await _loadRows(refreshTimeout.Token);
                }

                cancellationToken.ThrowIfCancellationRequested();

                if (_isUnloaded)
                    return;

                SparkUiThread.Queue(() =>
                {
                    if (_profileList == null
                        || _status == null
                        || _isUnloaded)
                    {
                        return;
                    }

                    _rows = rows ?? new List<PlayerPresence>();
                    RefreshVisibleRows(resetPage);
                });
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested || _isUnloaded)
            {
                return;
            }
            catch
            {
                if (_isUnloaded)
                    return;

                SparkUiThread.Queue(() =>
                {
                    if (_profileList == null
                        || _status == null
                        || _isUnloaded)
                    {
                        return;
                    }

                    if (hadExistingRows)
                    {
                        _status.Text =
                            "Showing previous results. Server list unavailable.";
                        return;
                    }

                    if (showedCachedRows)
                    {
                        _status.Text =
                            "Showing local profile. Server list unavailable.";
                        return;
                    }

                    _profileList.ShowEmptyMessage(
                        "Could not load online profiles.");

                    _status.Text = "Server list unavailable.";
                });
            }
            finally
            {
                if (hasRefreshLock)
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

            SparkUiThread.Queue(() =>
            {
                if (_profileList == null || _status == null || _isUnloaded)
                    return;

                _rows = cachedRows;
                RefreshVisibleRows(resetScroll);
            });

            return true;
        }

        private void ShowRows(
            IReadOnlyList<PlayerPresence> presenceRows,
            bool resetScroll)
        {
            if (_profileList == null
                || _status == null
                || _isUnloaded)
            {
                return;
            }

            _profileList.ClearRows();

            var filteredRows = (presenceRows
                                ?? new List<PlayerPresence>())
                .Where(row =>
                    row != null
                    && row.Status != RPStatus.Invisible)
                .Where(MatchesSearch)
                .Where(row =>
                    _discoveryFilters == null
                    || _discoveryFilters.Matches(row.Experience, row.DiscoveryTags));

            var rows = SortRows(filteredRows).ToList();

            if (resetScroll)
                _page.Reset();

            _page.Clamp(rows.Count);
            _pageControls?.Update(rows.Count);

            if (rows.Count == 0)
            {
                _profileList.ShowEmptyMessage(GetEmptyMessage());

                _status.Text = IsAutoRefreshEnabled()
                    ? "0 matching profiles."
                    : "0 matching profiles. Auto-refresh is off. Manually refresh instead.";

                return;
            }

            var pageRows = _page.GetPage(rows);

            for (var index = 0; index < pageRows.Count; index++)
                AddRow(pageRows[index], index);

            if (resetScroll)
                _profileList.ResetScroll();

            var visibleRows = rows.Count == 1
                ? "1 visible profile"
                : $"{rows.Count} visible profiles";

            var searchSuffix =
                ProfileListViewUI.GetSearchSuffix(_searchBox);

            var refreshSuffix = IsAutoRefreshEnabled()
                ? string.Empty
                : " Auto-refresh is off. Manually refresh instead.";

            _status.Text =
                $"{visibleRows}{searchSuffix}.{refreshSuffix}";
        }

        private void RefreshVisibleRows(bool resetScroll)
        {
            ShowRows(_rows, resetScroll);
        }

        private void AddRow(PlayerPresence presence, int index)
        {
            var row = _profileList.AddRow(
                index,
                string.Empty);

            AddBookmarkMarker(row, presence);

            _profileList.AddCell(
                row,
                presence.VisibleName(),
                30,
                7,
                163,
                Color.White);

            _profileList.AddCell(
                row,
                ProfileText.PresenceRace(presence),
                200,
                7,
                95,
                new Color(220, 220, 220));

            _profileList.AddCell(
                row,
                presence.AccountName,
                305,
                7,
                140,
                new Color(220, 220, 220));

            _profileList.AddCell(
                row,
                ProfileLabels.StatusLabel(presence.Status),
                455,
                7,
                105,
                ProfileStatusColors.Get(presence.Status));

            _profileList.AddCell(
                row,
                ProfileText.PresenceLocation(presence),
                570,
                7,
                180,
                new Color(220, 220, 220));

            ProfileScrollList.AddInteractionLayer(
                row,
                MakeTooltip(presence),
                () => _openProfile?.Invoke(presence));
        }

        private void AddBookmarkMarker(
            Container row,
            PlayerPresence presence)
        {
            if (_isBookmarked?.Invoke(presence) != true)
                return;

            ProfileListViewUI.AddBookmarkMarker(row);
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
            if (_discoveryFilters?.ActiveCount > 0)
                return "No online profiles match these filters.";

            return string.IsNullOrWhiteSpace(_searchBox?.Text)
                ? "No visible profiles yet."
                : "No online profiles match this search.";
        }

        private Tooltip MakeTooltip(PlayerPresence presence)
        {
            var showKnownFor =  _settings?.ShowKnownForInProfileTooltips.Value ?? true;

            var showCurrently = _settings?.ShowCurrentlyInProfileTooltips.Value ?? true;

            var showOutOfCharacter = _settings?.ShowOocInfoInProfileTooltips.Value ?? true;

            var trimLongTooltips = _settings?.TrimLongProfileTooltips.Value ?? true;

            var maximumLinesPerSection = _settings?.ProfileTooltipLinesPerSection.Value ?? 12;

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
                maximumLinesPerSection));
        }

        private void WatchTooltipSettings()
        {
            if (_settings == null)
                return;

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

        private void UnwatchTooltipSettings()
        {
            if (_settings == null)
                return;

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

        private void OnTooltipVisibilityChanged(object sender, ValueChangedEventArgs<bool> e)
        {
            QueueTooltipRefresh();
        }

        private void OnTooltipLineLimitChanged(object sender, ValueChangedEventArgs<int> e)
        {
            QueueTooltipRefresh();
        }

        private void QueueTooltipRefresh()
        {
            SparkUiThread.Queue(() =>
            {
                if (_isUnloaded || _profileList == null)
                    return;

                RefreshVisibleRows(false);
            });
        }

        protected override void Unload()
        {
            _isUnloaded = true;
            UnwatchTooltipSettings();
            _unwatchBookmarks?.Invoke(HandleBookmarksChanged);
            StopRefresh();

            _discoveryFilters?.Dispose();
            _discoveryFilters = null;
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

    }
}
