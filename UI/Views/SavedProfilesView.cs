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

namespace rp.spark.UI.Views
{
    public enum SavedProfilesMode
    {
        Recent,
        Bookmarks
    }

    public class SavedProfilesView : View
    {
        private const int RowHeight = 40;
        private const string SortViewed = "Viewed";
        private const string SortBookmarked = "Bookmarked";
        private static readonly TimeSpan LivePresenceWindow = TimeSpan.FromSeconds(45);

        private readonly Func<IReadOnlyList<SavedProfileSummary>> _loadSavedProfiles;
        private readonly Action<SavedProfileSummary, Action<string>> _openProfile;
        private readonly Action<SavedProfileSummary> _removeBookmark;
        private readonly Func<string, bool> _isBlockedAccount;
        private readonly Func<bool> _showMatureProfiles;
        private readonly Action<Action> _watchSavedProfiles;
        private readonly Action<Action> _unwatchSavedProfiles;
        private readonly SavedProfilesMode _mode;
        private readonly SparkSettings _settings;
        private readonly PageList _page = new PageList();
        private PageListControls _pageControls;

        private bool _isUnloaded;
        private TextBox _searchBox;
        private Dropdown _searchFieldDropdown;
        private Dropdown _sortDropdown;
        private ProfileFilterMenu _discoveryFilters;
        private ProfileScrollList _profileList;
        private Label _status;
        private string _statusOverride = string.Empty;

        public SavedProfilesView(
            Func<IReadOnlyList<SavedProfileSummary>> getSavedProfiles,
            Action<SavedProfileSummary, Action<string>> openProfile,
            SavedProfilesMode mode,
            SparkSettings settings,
            Func<string, bool> isBlockedAccount = null,
            Action<SavedProfileSummary> removeBookmark = null,
            Action<Action> watchSavedProfilesChanged = null,
            Action<Action> unwatchSavedProfilesChanged = null,
            Func<bool> showMatureProfiles = null)
        {
            _loadSavedProfiles = getSavedProfiles;
            _openProfile = openProfile;
            _mode = mode;
            _settings = settings;
            _isBlockedAccount = isBlockedAccount;
            _showMatureProfiles = showMatureProfiles;
            _removeBookmark = removeBookmark;
            _watchSavedProfiles = watchSavedProfilesChanged;
            _unwatchSavedProfiles = unwatchSavedProfilesChanged;
        }

        protected override void Build(Container buildPanel)
        {
            _isUnloaded = false;

            ProfileListViewUI.AddTitle(
                buildPanel,
                _mode == SavedProfilesMode.Bookmarks ? "Bookmarked Profiles" : "Recent Profiles",
                320);

            var refreshButton = ProfileListViewUI.AddRefreshButton(buildPanel);
            refreshButton.Click += (s, e) => RefreshRows(false);

            BuildSearchControls(buildPanel);
            BuildHeader(buildPanel);
            _watchSavedProfiles?.Invoke(HandleSavedProfilesChanged);
            WatchTooltipSettings();

            _profileList = new ProfileScrollList(ProfileListViewUI.BodyWidth, ProfileListViewUI.ListHeight, RowHeight)
            {
                Location = new Point(0, ProfileListViewUI.ListY),
                Parent = buildPanel
            };

            _pageControls = new PageListControls(
                buildPanel,
                _page,
                ProfileListViewUI.BodyWidth,
                () => RefreshRows(false))
            {
                Location = new Point(0, ProfileListViewUI.PageY)
            };

            _status = ProfileListViewUI.AddStatusLabel(buildPanel);

            RefreshRows(true);
        }

        private void BuildSearchControls(Container parent)
        {
            var sortOptions = _mode == SavedProfilesMode.Bookmarks
                ? new[] { SortBookmarked, ProfileListViewUI.SortRecentlySeen, ProfileListViewUI.SortName, ProfileListViewUI.SortRace, ProfileListViewUI.SortAccount, SortViewed }
                : new[] { SortViewed, ProfileListViewUI.SortRecentlySeen, ProfileListViewUI.SortName, ProfileListViewUI.SortRace, ProfileListViewUI.SortAccount, SortBookmarked };

            var controls = ProfileListViewUI.AddSearchControls(
                parent,
                "Search saved profiles",
                new[]
                {
                    ProfileListViewUI.SearchAllFields,
                    ProfileListViewUI.SearchName,
                    ProfileListViewUI.SearchAccount,
                    ProfileListViewUI.SearchRace,
                    ProfileListViewUI.SearchProfession
                },
                sortOptions,
                sortOptions[0],
                () => RefreshRows(true));

            _searchBox = controls.SearchBox;
            _searchFieldDropdown = controls.SearchFieldDropdown;
            _sortDropdown = controls.SortDropdown;

            _discoveryFilters = new ProfileFilterMenu(
                parent,
                () => RefreshRows(true));
        }

        private void BuildHeader(Container parent)
        {
            if (_mode == SavedProfilesMode.Bookmarks)
            {
                ProfileListViewUI.AddHeader(parent, "Character", 0, 215);
                ProfileListViewUI.AddHeader(parent, "Race", 225, 95);
                ProfileListViewUI.AddHeader(parent, "Account", 330, 160);
                ProfileListViewUI.AddHeader(parent, "Bookmarked", 500, 135);
                ProfileListViewUI.AddHeader(parent, "Action", 650, 100);
                return;
            }

            ProfileListViewUI.AddHeader(parent, "Character", 28, 212);
            ProfileListViewUI.AddHeader(parent, "Race", 250, 110);
            ProfileListViewUI.AddHeader(parent, "Account", 370, 170);
            ProfileListViewUI.AddHeader(parent, "Saved", 550, 205);
        }

        private void HandleSavedProfilesChanged()
        {
            SparkUiThread.Queue(() =>
            {
                if (!_isUnloaded)
                    RefreshRows(false, false);
            });
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

        private void OnTooltipVisibilityChanged(
            object sender,
            ValueChangedEventArgs<bool> e)
        {
            RebuildTooltips();
        }

        private void OnTooltipLineLimitChanged(
            object sender,
            ValueChangedEventArgs<int> e)
        {
            RebuildTooltips();
        }

        private void RebuildTooltips()
        {
            SparkUiThread.Queue(() =>
            {
                if (!_isUnloaded && _profileList != null)
                    RefreshRows(false, false);
            });
        }

        private void RefreshRows(bool resetPage, bool resetScroll = true)
        {
            if (_isUnloaded || _profileList == null)
                return;

            _profileList.ClearRows(resetScroll);

            var filteredRows = (_loadSavedProfiles?.Invoke() ?? new List<SavedProfileSummary>())
                .Where(savedProfile => savedProfile != null)
                .Where(savedProfile => !IsHidden(savedProfile))
                .Where(MatchesSearch)
                .Where(savedProfile =>
                    _discoveryFilters == null
                    || _discoveryFilters.Matches(savedProfile.Experience, savedProfile.DiscoveryTags));

            var rows = SortRows(filteredRows).ToList();
            var statusOverride = _statusOverride;

            if (resetPage)
                _page.Reset();

            _page.Clamp(rows.Count);
            _pageControls?.Update(rows.Count);

            if (rows.Count == 0)
            {
                _profileList.ShowEmptyMessage(_mode == SavedProfilesMode.Bookmarks
                    ? GetEmptyBookmarksMessage()
                    : GetEmptyRecentMessage());

                _status.Text = !string.IsNullOrWhiteSpace(statusOverride)
                    ? statusOverride
                    : _mode == SavedProfilesMode.Bookmarks
                        ? "0 matching bookmarks."
                        : "0 matching recent profiles.";

                return;
            }

            var pageRows = _page.GetPage(rows);

            for (var index = 0; index < pageRows.Count; index++)
                AddRow(pageRows[index], index);

            var searchSuffix =
                ProfileListViewUI.GetSearchSuffix(_searchBox);

            _status.Text = !string.IsNullOrWhiteSpace(statusOverride)
                ? statusOverride
                : _mode == SavedProfilesMode.Bookmarks
                    ? $"{rows.Count} bookmarked profile(s){searchSuffix}."
                    : $"{rows.Count} recent profile(s){searchSuffix}.";
        }

        private void AddRow(SavedProfileSummary savedProfile, int index)
        {
            var row = _profileList.AddRow(index, string.Empty);
            var secondary = new Color(220, 220, 220);

            if (_mode == SavedProfilesMode.Bookmarks)
            {
                _profileList.AddCell(row, ProfileText.SavedCharacterName(savedProfile), 8, 8, 210, Color.White);
                _profileList.AddCell(row, ProfileText.SavedRace(savedProfile), 225, 8, 95, secondary);
                _profileList.AddCell(row, ProfileText.SavedAccountName(savedProfile), 330, 8, 160, secondary);
                _profileList.AddCell(row, GetSavedTime(savedProfile), 500, 8, 135, secondary);

                AddRemoveButton(row, savedProfile);
            }
            else
            {
                AddBookmarkMarker(row, savedProfile);

                _profileList.AddCell(row, ProfileText.SavedCharacterName(savedProfile), 30, 8, 210, Color.White);
                _profileList.AddCell(row, ProfileText.SavedRace(savedProfile), 250, 8, 110, secondary);
                _profileList.AddCell(row, ProfileText.SavedAccountName(savedProfile), 370, 8, 170, secondary);
                _profileList.AddCell(row, GetSavedTime(savedProfile), 550, 8, 205, secondary);
            }

            ProfileScrollList.AddInteractionLayer(row, MakeTooltip(savedProfile), 
                () => _openProfile?.Invoke(savedProfile, ShowStatusOverride), _mode == SavedProfilesMode.Bookmarks ? 110 : 0);
        }

        private static void AddBookmarkMarker(Container row, SavedProfileSummary savedProfile)
        {
            if (savedProfile?.IsBookmarked == true)
                ProfileListViewUI.AddBookmarkMarker(row);
        }

        private void AddRemoveButton(Container row, SavedProfileSummary savedProfile)
        {
            var removeButton = new StandardButton
            {
                Text = "Remove",
                Location = new Point(650, 5),
                Size = new Point(90, 30),
                Parent = row,
                ZIndex = 101
            };

            removeButton.Click += (s, e) =>
            {
                if (_removeBookmark == null)
                {
                    _status.Text = "Bookmark cache unavailable.";
                    return;
                }

                _removeBookmark(savedProfile);
                RefreshRows(false);
                _status.Text = "Bookmark removed.";
            };
        }

        private void MakeClickable(Control control, SavedProfileSummary savedProfile, string tooltipText)
        {
            ProfileScrollList.WireInteraction(control, tooltipText, () =>
            {
                _openProfile?.Invoke(savedProfile, ShowStatusOverride);
            });
        }

        private void ShowStatusOverride(string message)
        {
            _statusOverride = message ?? string.Empty;

            if (_status != null && !_isUnloaded)
                _status.Text = _statusOverride;
        }

        private bool IsHidden(SavedProfileSummary savedProfile)
        {
            var isBlocked = _isBlockedAccount != null
                && _isBlockedAccount(ProfileText.SavedAccountName(savedProfile));

            var matureHidden = savedProfile?.IsMature == true
                && !(_showMatureProfiles?.Invoke() ?? false);

            return isBlocked || matureHidden;
        }

        private bool MatchesSearch(SavedProfileSummary savedProfile)
        {
            var query = _searchBox?.Text?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(query))
                return true;

            return GetSearchText(savedProfile)
                .IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private string GetSearchText(SavedProfileSummary savedProfile)
        {
            var searchField = _searchFieldDropdown?.SelectedItem?.ToString() ?? ProfileListViewUI.SearchAllFields;

            switch (searchField)
            {
                case ProfileListViewUI.SearchName:
                    return ProfileText.JoinSearchText(ProfileText.SavedCharacterName(savedProfile), savedProfile?.ProfileName);
                case ProfileListViewUI.SearchAccount:
                    return ProfileText.SavedAccountName(savedProfile);
                case ProfileListViewUI.SearchRace:
                    return ProfileText.SavedRace(savedProfile);
                case ProfileListViewUI.SearchProfession:
                    return ProfileText.SavedProfession(savedProfile);
                default:
                    return ProfileText.JoinSearchText(
                        ProfileText.SavedCharacterName(savedProfile),
                        savedProfile?.ProfileName,
                        ProfileText.SavedAccountName(savedProfile),
                        ProfileText.SavedRace(savedProfile),
                        ProfileText.SavedProfession(savedProfile));
            }
        }

        private IEnumerable<SavedProfileSummary> SortRows(IEnumerable<SavedProfileSummary> savedProfiles)
        {
            var sortMode = _sortDropdown?.SelectedItem?.ToString()
                           ?? (_mode == SavedProfilesMode.Bookmarks ? SortBookmarked : SortViewed);

            switch (sortMode)
            {
                case ProfileListViewUI.SortName:
                    return savedProfiles
                        .OrderBy(ProfileText.SavedCharacterName, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(savedProfile => ProfileText.SavedAccountName(savedProfile), StringComparer.OrdinalIgnoreCase);
                case ProfileListViewUI.SortRace:
                    return savedProfiles
                        .OrderBy(savedProfile => ProfileText.SavedRace(savedProfile), StringComparer.OrdinalIgnoreCase)
                        .ThenBy(ProfileText.SavedCharacterName, StringComparer.OrdinalIgnoreCase);
                case ProfileListViewUI.SortAccount:
                    return savedProfiles
                        .OrderBy(savedProfile => ProfileText.SavedAccountName(savedProfile), StringComparer.OrdinalIgnoreCase)
                        .ThenBy(ProfileText.SavedCharacterName, StringComparer.OrdinalIgnoreCase);
                case ProfileListViewUI.SortRecentlySeen:
                    return savedProfiles
                        .OrderByDescending(ProfileText.SavedLastSeen)
                        .ThenBy(ProfileText.SavedCharacterName, StringComparer.OrdinalIgnoreCase);
                case SortBookmarked:
                    return savedProfiles
                        .OrderByDescending(savedProfile => savedProfile.BookmarkedAt ?? DateTime.MinValue)
                        .ThenBy(ProfileText.SavedCharacterName, StringComparer.OrdinalIgnoreCase);
                default:
                    return savedProfiles
                        .OrderByDescending(savedProfile => savedProfile.CachedAt)
                        .ThenBy(ProfileText.SavedCharacterName, StringComparer.OrdinalIgnoreCase);
            }
        }

        private string GetEmptyBookmarksMessage()
        {
            if (_discoveryFilters?.ActiveCount > 0)
                return "No bookmarks match these filters.";

            return string.IsNullOrWhiteSpace(_searchBox?.Text)
                ? "No bookmarked profiles yet."
                : "No bookmarks match this search.";
        }

        private string GetEmptyRecentMessage()
        {
            if (_discoveryFilters?.ActiveCount > 0)
                return "No recent profiles match these filters.";

            return string.IsNullOrWhiteSpace(_searchBox?.Text)
                ? "No recently viewed profiles yet."
                : "No recent profiles match this search.";
        }

        private Tooltip MakeTooltip(SavedProfileSummary savedProfile)
        {
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

            var savedTimeLabel = _mode == SavedProfilesMode.Bookmarks
                ? $"Bookmarked: {ProfileText.FormatShortTime(savedProfile.BookmarkedAt ?? savedProfile.CachedAt, "-")}"
                : $"Viewed: {ProfileText.FormatShortTime(savedProfile.CachedAt, "-")}";

            return new Tooltip(new ProfilePresenceTooltipView(
                ProfileText.SavedCharacterName(savedProfile),
                ProfileText.SavedCharacterDetails(savedProfile),
                GetCachedStatusText(savedProfile),
                GetCachedLocationText(savedProfile),
                savedProfile?.KnownFor,
                savedProfile?.Currently,
                savedProfile?.OutOfCharacterInfo,
                showKnownFor,
                showCurrently,
                showOutOfCharacter,
                trimLongTooltips,
                maximumLinesPerSection,
                new[]
                {
            $"Account: {ProfileText.SavedAccountName(savedProfile)}",
            savedTimeLabel
                }));
        }

        private string GetSavedTime(SavedProfileSummary savedProfile)
        {
            return _mode == SavedProfilesMode.Bookmarks
                ? ProfileText.FormatShortTime(savedProfile.BookmarkedAt ?? savedProfile.CachedAt, "-")
                : ProfileText.FormatShortTime(savedProfile.CachedAt, "-");
        }

        private static string GetCachedStatusText(SavedProfileSummary savedProfile)
        {
            if (!IsRecentPresence(savedProfile) || savedProfile.Status == RPStatus.Invisible)
                return ProfileLabels.StatusLabel(RPStatus.Offline);

            return ProfileLabels.StatusLabel(savedProfile.Status);
        }

        private static string GetCachedLocationText(SavedProfileSummary savedProfile)
        {
            return IsRecentPresence(savedProfile) && !string.IsNullOrWhiteSpace(savedProfile.LocationName)
                ? savedProfile.LocationName.Trim()
                : "Unknown";
        }

        // Cached presence is only treated as live briefly (45s) so we can properly show if they are offline as needed when you open a profile
        private static bool IsRecentPresence(SavedProfileSummary savedProfile)
        {
            return savedProfile != null
                && savedProfile.LastSeen != default
                && DateTime.UtcNow - savedProfile.LastSeen.ToUniversalTime() <= LivePresenceWindow;
        }

        protected override void Unload()
        {
            _isUnloaded = true;
            UnwatchTooltipSettings();
            _unwatchSavedProfiles?.Invoke(HandleSavedProfilesChanged);

            _discoveryFilters?.Dispose();
            _discoveryFilters = null;
        }
    }
}
