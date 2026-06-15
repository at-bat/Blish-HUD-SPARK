using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Graphics.UI;
using Microsoft.Xna.Framework;
using rp.spark.Models;
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
        private readonly Action<SavedProfileSummary> _openProfile;
        private readonly Action<SavedProfileSummary> _removeBookmark;
        private readonly Func<string, bool> _isBlockedAccount;
        private readonly Action<Action> _watchSavedProfiles;
        private readonly Action<Action> _unwatchSavedProfiles;
        private readonly SavedProfilesMode _mode;

        private bool _isUnloaded;
        private TextBox _searchBox;
        private Dropdown _searchFieldDropdown;
        private Dropdown _sortDropdown;
        private ProfileScrollList _profileList;
        private Label _status;

        public SavedProfilesView(
            Func<IReadOnlyList<SavedProfileSummary>> getSavedProfiles,
            Action<SavedProfileSummary> openProfile,
            SavedProfilesMode mode,
            Func<string, bool> isBlockedAccount = null,
            Action<SavedProfileSummary> removeBookmark = null,
            Action<Action> watchSavedProfilesChanged = null,
            Action<Action> unwatchSavedProfilesChanged = null)
        {
            _loadSavedProfiles = getSavedProfiles;
            _openProfile = openProfile;
            _mode = mode;
            _isBlockedAccount = isBlockedAccount;
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
            refreshButton.Click += (s, e) => RefreshRows();

            BuildSearchControls(buildPanel);
            BuildHeader(buildPanel);
            _watchSavedProfiles?.Invoke(HandleSavedProfilesChanged);

            _profileList = new ProfileScrollList(ProfileListViewUI.BodyWidth, ProfileListViewUI.ListHeight, RowHeight)
            {
                Location = new Point(0, ProfileListViewUI.ListY),
                Parent = buildPanel
            };

            _status = ProfileListViewUI.AddStatusLabel(buildPanel);

            RefreshRows();
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
                RefreshRows);

            _searchBox = controls.SearchBox;
            _searchFieldDropdown = controls.SearchFieldDropdown;
            _sortDropdown = controls.SortDropdown;
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
            GameService.Overlay.QueueMainThreadUpdate(gameTime =>
            {
                if (!_isUnloaded)
                    RefreshRows();
            });
        }

        private void RefreshRows()
        {
            if (_isUnloaded || _profileList == null)
                return;

            _profileList.ClearRows();

            var filteredRows = (_loadSavedProfiles?.Invoke() ?? new List<SavedProfileSummary>())
                .Where(savedProfile => savedProfile != null)
                .Where(savedProfile => !IsHidden(savedProfile))
                .Where(MatchesSearch);

            var rows = SortRows(filteredRows).ToList();

            if (!rows.Any())
            {
                _profileList.ShowEmptyMessage(_mode == SavedProfilesMode.Bookmarks
                    ? GetEmptyBookmarksMessage()
                    : GetEmptyRecentMessage());
                _status.Text = _mode == SavedProfilesMode.Bookmarks
                    ? "0 matching bookmarks."
                    : "0 matching recent profiles.";
                return;
            }

            for (var i = 0; i < rows.Count; i++)
                AddRow(rows[i], i);

            _profileList.ResetScroll();

            var searchSuffix = ProfileListViewUI.GetSearchSuffix(_searchBox);
            _status.Text = _mode == SavedProfilesMode.Bookmarks
                ? $"{rows.Count} bookmarked profile(s){searchSuffix}."
                : $"{rows.Count} recent profile(s){searchSuffix}.";
        }

        private void AddRow(SavedProfileSummary savedProfile, int index)
        {
            var tooltipText = TooltipText(savedProfile);
            var row = _profileList.AddRow(index, tooltipText);

            // Quick fix for clickthrough on remove bookmark also opening profiles
            // The last segment of the bookmark row is non-clickable for profile opening, not sure if there's a better way to handle this
            if (_mode == SavedProfilesMode.Bookmarks)
            {
                MakeClickable(_profileList.AddCell(row, ProfileText.SavedCharacterName(savedProfile), 8, 8, 210, Color.White), savedProfile, tooltipText);
                MakeClickable(_profileList.AddCell(row, ProfileText.SavedRace(savedProfile), 225, 8, 95, new Color(220, 220, 220)), savedProfile, tooltipText);
                MakeClickable(_profileList.AddCell(row, ProfileText.SavedAccountName(savedProfile), 330, 8, 160, new Color(220, 220, 220)), savedProfile, tooltipText);
                MakeClickable(_profileList.AddCell(row, GetSavedTime(savedProfile), 500, 8, 135, new Color(220, 220, 220)), savedProfile, tooltipText);

                AddRemoveButton(row, savedProfile);
                return;
            }

            MakeClickable(row, savedProfile, tooltipText);

            AddBookmarkMarker(row, savedProfile, tooltipText);
            MakeClickable(_profileList.AddCell(row, ProfileText.SavedCharacterName(savedProfile), 30, 8, 210, Color.White), savedProfile, tooltipText);
            MakeClickable(_profileList.AddCell(row, ProfileText.SavedRace(savedProfile), 250, 8, 110, new Color(220, 220, 220)), savedProfile, tooltipText);
            MakeClickable(_profileList.AddCell(row, ProfileText.SavedAccountName(savedProfile), 370, 8, 170, new Color(220, 220, 220)), savedProfile, tooltipText);
            MakeClickable(_profileList.AddCell(row, GetSavedTime(savedProfile), 550, 8, 205, new Color(220, 220, 220)), savedProfile, tooltipText);
        }

        private void AddBookmarkMarker(Container row, SavedProfileSummary savedProfile, string tooltipText)
        {
            if (savedProfile?.IsBookmarked != true)
                return;

            var marker = ProfileListViewUI.AddBookmarkMarker(row);
            MakeClickable(marker, savedProfile, tooltipText);
        }

        private void AddRemoveButton(Container row, SavedProfileSummary savedProfile)
        {
            var removeButton = new StandardButton
            {
                Text = "Remove",
                Location = new Point(650, 5),
                Size = new Point(90, 30),
                Parent = row
            };

            removeButton.Click += (s, e) =>
            {
                if (_removeBookmark == null)
                {
                    _status.Text = "Bookmark cache unavailable.";
                    return;
                }

                _removeBookmark(savedProfile);
                RefreshRows();
                _status.Text = "Bookmark removed.";
            };
        }

        private void MakeClickable(Control control, SavedProfileSummary savedProfile, string tooltipText)
        {
            ProfileScrollList.WireInteraction(control, tooltipText, () => _openProfile?.Invoke(savedProfile));
        }

        private bool IsHidden(SavedProfileSummary savedProfile)
        {
            return _isBlockedAccount != null
                && _isBlockedAccount(ProfileText.SavedAccountName(savedProfile));
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
            return string.IsNullOrWhiteSpace(_searchBox?.Text)
                ? "No bookmarked profiles yet."
                : "No bookmarks match this search.";
        }

        private string GetEmptyRecentMessage()
        {
            return string.IsNullOrWhiteSpace(_searchBox?.Text)
                ? "No recently viewed profiles yet."
                : "No recent profiles match this search.";
        }

        private string TooltipText(SavedProfileSummary savedProfile)
        {
            var lines = new List<string>
            {
                ProfileText.SavedCharacterName(savedProfile),
                ProfileText.SavedCharacterDetails(savedProfile),
                $"Account: {ProfileText.SavedAccountName(savedProfile)}",
                $"Status: {GetCachedStatusText(savedProfile)}",
                $"Location: {GetCachedLocationText(savedProfile)}",
                _mode == SavedProfilesMode.Bookmarks
                    ? $"Bookmarked: {ProfileText.FormatShortTime(savedProfile.BookmarkedAt ?? savedProfile.CachedAt, "-")}"
                    : $"Viewed: {ProfileText.FormatShortTime(savedProfile.CachedAt, "-")}"
            };

            if (!string.IsNullOrWhiteSpace(savedProfile.Currently))
            {
                lines.Add("----------------");
                lines.Add($"Currently: {savedProfile.Currently.Trim()}");
            }

            if (!string.IsNullOrWhiteSpace(savedProfile.OutOfCharacterInfo))
            {
                lines.Add("----------------");
                lines.Add(savedProfile.OutOfCharacterInfo.Trim());
            }

            return string.Join(Environment.NewLine, lines.Where(line => !string.IsNullOrWhiteSpace(line)));
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
            _unwatchSavedProfiles?.Invoke(HandleSavedProfilesChanged);
        }
    }
}
