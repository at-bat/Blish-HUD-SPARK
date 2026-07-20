using Blish_HUD.Controls;
using Blish_HUD.Input;
using Microsoft.Xna.Framework;
using rp.spark.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace rp.spark.UI.Controls
{
    internal enum ProfileFilterCategory
    {
        Experience,
        Preference,
        Theme,
        Style
    }

    internal sealed class ProfileFilterMenu : IDisposable
    {
        private readonly Dictionary<ProfileFilterCategory, HashSet<string>> _selected =
            new Dictionary<ProfileFilterCategory, HashSet<string>>
            {
                [ProfileFilterCategory.Experience] = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                [ProfileFilterCategory.Preference] = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                [ProfileFilterCategory.Theme] = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                [ProfileFilterCategory.Style] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            };

        private readonly StandardButton _button;
        private readonly ContextMenuStrip _menu;
        private readonly Action _changed;
        private ContextMenuStripItem _resetItem;
        private bool _isDisposed;

        public ProfileFilterMenu(Container parent, Action changed)
        {
            _changed = changed;

            _button = new StandardButton
            {
                Text = "Filters",
                Location = new Point(650, 40),
                Size = new Point(100, 35),
                Parent = parent
            };

            _menu = new ContextMenuStrip(GetMenuItems)
            {
                Width = 200
            };

            _button.Click += OnButtonClick;
        }

        public int ActiveCount => _selected.Values.Sum(values => values.Count);

        public bool Matches(ProfileExperience experience, ProfileDiscoveryTags tags)
        {
            tags = tags ?? new ProfileDiscoveryTags();

            return MatchesExperience(experience)
                && MatchesCategory(ProfileFilterCategory.Preference, tags.Preferences)
                && MatchesCategory(ProfileFilterCategory.Theme, tags.Themes)
                && MatchesCategory(ProfileFilterCategory.Style, tags.Styles);
        }

        private bool MatchesExperience(ProfileExperience experience)
        {
            var selected = _selected[ProfileFilterCategory.Experience];

            return selected.Count == 0
                || selected.Contains(experience.ToString());
        }

        private bool MatchesCategory(ProfileFilterCategory category, IEnumerable<string> available)
        {
            var selected = _selected[category];
            var availableValues = new HashSet<string>(
                available ?? Enumerable.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);

            return selected.Count == 0
                || selected.IsSubsetOf(availableValues);
        }

        private IEnumerable<ContextMenuStripItem> GetMenuItems()
        {
            yield return CreateSubmenu(
                "Experience",
                ProfileFilterCategory.Experience,
                ProfileLabels.ExperienceOptions
                    .Skip(1)
                    .Select(label => new KeyValuePair<string, string>(
                        ProfileLabels.ParseExperience(label).ToString(),
                        label)));

            yield return CreateSubmenu(
                "Preferences",
                ProfileFilterCategory.Preference,
                ProfileLabels.PreferenceOptions.Select(option =>
                    new KeyValuePair<string, string>(option.Key.ToString(), option.Value)));

            yield return CreateSubmenu(
                "Themes",
                ProfileFilterCategory.Theme,
                ProfileLabels.ThemeOptions.Select(option =>
                    new KeyValuePair<string, string>(option.Key.ToString(), option.Value)));

            yield return CreateSubmenu(
                "Styles",
                ProfileFilterCategory.Style,
                ProfileLabels.StyleOptions.Select(option =>
                    new KeyValuePair<string, string>(option.Key.ToString(), option.Value)));

            _resetItem = new ContextMenuStripItem
            {
                Text = "Clear all filters",
                Enabled = ActiveCount > 0
            };

            _resetItem.Click += (s, e) => Reset();
            yield return _resetItem;
        }

        private ContextMenuStripItem CreateSubmenu(
            string text,
            ProfileFilterCategory category,
            IEnumerable<KeyValuePair<string, string>> options)
        {
            var item = new ContextMenuStripItem
            {
                Text = text,
                Submenu = new ContextMenuStrip(() => GetOptionItems(category, options))
                {
                    Width = 220
                }
            };

            return item;
        }

        private IEnumerable<ContextMenuStripItem> GetOptionItems(
            ProfileFilterCategory category,
            IEnumerable<KeyValuePair<string, string>> options)
        {
            foreach (var option in options)
            {
                var optionId = option.Key;
                var item = new ContextMenuStripItem
                {
                    Text = option.Value,
                    CanCheck = true,
                    Checked = _selected[category].Contains(optionId)
                };

                item.CheckedChanged += (s, e) =>
                {
                    SetSelected(category, optionId, e.Checked);
                };

                yield return item;
            }
        }

        private void SetSelected(ProfileFilterCategory category, string optionId, bool selected)
        {
            if (selected)
                _selected[category].Add(optionId);
            else
                _selected[category].Remove(optionId);

            UpdateButton();
            _changed?.Invoke();
        }

        private void Reset()
        {
            foreach (var selected in _selected.Values)
                selected.Clear();

            UpdateButton();
            _changed?.Invoke();
        }

        private void UpdateButton()
        {
            _button.Text = ActiveCount == 0
                ? "Filters"
                : $"Filters ({ActiveCount})";

            if (_resetItem != null)
                _resetItem.Enabled = ActiveCount > 0;
        }

        private void OnButtonClick(object sender, MouseEventArgs e)
        {
            _menu.Show(_button);
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            _button.Click -= OnButtonClick;
            _menu.Dispose();
        }
    }
}