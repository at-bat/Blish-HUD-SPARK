using Blish_HUD;
using Blish_HUD.Controls;
using Microsoft.Xna.Framework;
using rp.spark.UI.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace rp.spark.UI.Views
{
    internal static class ProfileListViewUI
    {
        public const int BodyWidth = 760;
        public const int HeaderY = 92;
        public const int ListY = 128;
        public const int ListHeight = 400;
        public const int StatusY = 565;
        public const int PageY = 532;
        public const int BookmarkMarkerY = 11;
        public const int BookmarkIconAssetId = 102439;

        public const string SearchAllFields = "All fields";
        public const string SearchName = "Name";
        public const string SearchAccount = "Account";
        public const string SearchRace = "Race";
        public const string SearchProfession = "Profession";

        public const string SortName = "Name";
        public const string SortRecentlySeen = "Recently Seen";
        public const string SortRace = "Race";
        public const string SortAccount = "Account";

        private static readonly Color SecondaryTextColor = new Color(220, 220, 220);
        private static readonly Color HeaderTextColor = new Color(255, 233, 180);

        // Time before we submit the search for filtering
        // 250ms felt too quick, 450ms might be okay for now
        private const int SearchDebounceMilliseconds = 450;

        public static void AddTitle(Container parent, string text, int width)
        {
            new Label
            {
                Text = text,
                Font = GameService.Content.DefaultFont18,
                TextColor = Color.White,
                StrokeText = true,
                Location = new Point(0, 0),
                Size = new Point(width, 34),
                Parent = parent
            };
        }

        public static StandardButton AddRefreshButton(Container parent)
        {
            return new StandardButton
            {
                Text = "Refresh",
                Location = new Point(650, 0),
                Size = new Point(100, 30),
                Parent = parent
            };
        }

        public static ProfileListSearchControls AddSearchControls(
            Container parent,
            string placeholderText,
            IEnumerable<string> searchOptions,
            IEnumerable<string> sortOptions,
            string selectedSortOption,
            Action changed)
        {
            var searchBox = new TextBox
            {
                PlaceholderText = placeholderText,
                Location = new Point(0, 40),
                Size = new Point(250, 35),
                Parent = parent
            };

            var searchChangeVersion = 0;

            searchBox.TextChanged += (s, e) =>
            {
                var scheduledVersion = ++searchChangeVersion;

                _ = QueueDebouncedAsync(() =>
                {
                    if (scheduledVersion == searchChangeVersion)
                        changed?.Invoke();
                });
            };

            var searchFieldDropdown = new Dropdown
            {
                Location = new Point(260, 40),
                Size = new Point(140, 35),
                Parent = parent
            };

            AddOptions(searchFieldDropdown, searchOptions);
            searchFieldDropdown.SelectedItem = SearchAllFields;
            searchFieldDropdown.ValueChanged += (s, e) =>
            {
                searchChangeVersion++;
                changed?.Invoke();
            };

            new Label
            {
                Text = "Sort by:",
                Font = GameService.Content.DefaultFont14,
                TextColor = SecondaryTextColor,
                Location = new Point(410, 48),
                Size = new Point(60, 24),
                Parent = parent
            };

            var sortDropdown = new Dropdown
            {
                Location = new Point(470, 40),
                Size = new Point(170, 35),
                Parent = parent
            };

            var sortOptionList = (sortOptions ?? Enumerable.Empty<string>()).ToList();
            AddOptions(sortDropdown, sortOptionList);
            sortDropdown.SelectedItem = selectedSortOption ?? sortOptionList.FirstOrDefault();
            sortDropdown.ValueChanged += (s, e) =>
            {
                searchChangeVersion++;
                changed?.Invoke();
            };

            return new ProfileListSearchControls(searchBox, searchFieldDropdown, sortDropdown);
        }

        public static Label AddStatusLabel(Container parent)
        {
            return new Label
            {
                Text = string.Empty,
                Font = GameService.Content.DefaultFont12,
                TextColor = SecondaryTextColor,
                Location = new Point(0, StatusY),
                Size = new Point(BodyWidth, 24),
                Parent = parent
            };
        }

        public static void AddHeader(Container parent, string text, int x, int width)
        {
            new Label
            {
                Text = text,
                Font = GameService.Content.DefaultFont16,
                TextColor = HeaderTextColor,
                StrokeText = true,
                Location = new Point(x, HeaderY),
                Size = new Point(width, 26),
                Parent = parent
            };
        }

        public static AssetIcon AddBookmarkMarker(Container row)
        {
            var marker = new AssetIcon
            {
                Location = new Point(7, BookmarkMarkerY),
                Size = new Point(18, 18),
                Parent = row
            };

            marker.SetAssetId(BookmarkIconAssetId);
            return marker;
        }

        public static string GetSearchSuffix(TextBox searchBox)
        {
            return string.IsNullOrWhiteSpace(searchBox?.Text)
                ? string.Empty
                : " match";
        }

        private static void AddOptions(Dropdown dropdown, IEnumerable<string> options)
        {
            foreach (var option in options ?? Enumerable.Empty<string>())
                dropdown.Items.Add(option);
        }

        // Fix search/sorting from rebuilding every keypress with a debounce
        private static async Task QueueDebouncedAsync(Action action)
        {
            await Task.Delay(SearchDebounceMilliseconds);
            SparkUiThread.Queue(action);
        }
    }

    internal sealed class ProfileListSearchControls
    {
        public ProfileListSearchControls(TextBox searchBox, Dropdown searchFieldDropdown, Dropdown sortDropdown)
        {
            SearchBox = searchBox;
            SearchFieldDropdown = searchFieldDropdown;
            SortDropdown = sortDropdown;
        }

        public TextBox SearchBox { get; }

        public Dropdown SearchFieldDropdown { get; }

        public Dropdown SortDropdown { get; }
    }
}
