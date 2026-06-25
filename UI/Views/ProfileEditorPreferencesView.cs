using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Graphics.UI;
using Microsoft.Xna.Framework;
using rp.spark.Models;
using rp.spark.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace rp.spark.UI.Views
{
    public class ProfileEditorPreferencesView : View
    {
        private const int FormWidth = 760;
        private const int FormHeight = 500;
        private const int CheckboxWidth = 230;
        private const int CheckboxHeight = 30;
        private const int CheckboxColumns = 3;
        private const int CheckboxGap = 5;

        private readonly ProfileEditorSession _session;
        private readonly List<KeyValuePair<Checkbox, ProfilePreferenceFlags>> _preferenceCheckboxes = new List<KeyValuePair<Checkbox, ProfilePreferenceFlags>>();
        private readonly List<KeyValuePair<Checkbox, ProfileThemeFlags>> _themeCheckboxes = new List<KeyValuePair<Checkbox, ProfileThemeFlags>>();
        private readonly List<KeyValuePair<Checkbox, ProfileStyleFlags>> _styleCheckboxes = new List<KeyValuePair<Checkbox, ProfileStyleFlags>>();

        private Label _status;
        private Dropdown _experienceDropdown;
        private bool _isRefreshing;

        public ProfileEditorPreferencesView(ProfileEditorSession session)
        {
            _session = session;
        }

        protected override void Build(Container buildPanel)
        {
            if (!_session.InitialState.CanEditProfile)
            {
                ProfileEditorUI.ShowUnavailableMessage(buildPanel);
                return;
            }

            var form = SparkFormLayout.AddVerticalStack(buildPanel, 0, 0, FormWidth, FormHeight, 15);
            var experienceGroup = SparkFormLayout.AddAutoStack(form, FormWidth, 0);

            SparkFormLayout.AddLabel(experienceGroup, "Experience", FormWidth);
            _experienceDropdown = SparkFormLayout.AddDropdown(
                experienceGroup,
                ProfileLabels.ExperienceOptions,
                null,
                220);

            _experienceDropdown.ValueChanged += (s, e) =>
            {
                if (!_isRefreshing)
                    _session.Profile.Experience = ProfileLabels.ParseExperience(_experienceDropdown.SelectedItem?.ToString());
            };

            SparkFormLayout.AddLabel(form, "Preferences", FormWidth);
            BuildFlagCheckboxes(
                form,
                ProfileLabels.PreferenceOptions,
                option => (_session.Profile.Preferences & option) == option,
                option => _session.Profile.Preferences = ToggleFlag(_session.Profile.Preferences, option),
                (checkbox, option) => _preferenceCheckboxes.Add(new KeyValuePair<Checkbox, ProfilePreferenceFlags>(checkbox, option)));

            SparkFormLayout.AddLabel(form, "Themes", FormWidth);
            BuildFlagCheckboxes(
                form,
                ProfileLabels.ThemeOptions,
                option => (_session.Profile.Themes & option) == option,
                option => _session.Profile.Themes = ToggleFlag(_session.Profile.Themes, option),
                (checkbox, option) => _themeCheckboxes.Add(new KeyValuePair<Checkbox, ProfileThemeFlags>(checkbox, option)));

            SparkFormLayout.AddLabel(form, "Styles", FormWidth);
            BuildFlagCheckboxes(
                form,
                ProfileLabels.StyleOptions,
                option => (_session.Profile.Styles & option) == option,
                option => _session.Profile.Styles = ToggleFlag(_session.Profile.Styles, option),
                (checkbox, option) => _styleCheckboxes.Add(new KeyValuePair<Checkbox, ProfileStyleFlags>(checkbox, option)));

            BuildFooter(buildPanel);
            _session.ProfileChanged += HandleProfileChanged;
            RefreshFromSession();
        }

        private void BuildFooter(Container buildPanel)
        {
            _status = ProfileEditorUI.AddSaveFooter(buildPanel, _session);
            _session.StatusChanged += HandleStatusChanged;
        }

        private void BuildFlagCheckboxes<TFlag>(
            Container parent,
            System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<TFlag, string>> options,
            Func<TFlag, bool> isChecked,
            Action<TFlag> onToggle,
            Action<Checkbox, TFlag> trackCheckbox)
        {
            var optionList = options.ToList();
            var rowCount = Math.Max(1, (int)Math.Ceiling(optionList.Count / (double)CheckboxColumns));
            var grid = new FlowPanel
            {
                Width = FormWidth,
                Height = rowCount * (CheckboxHeight + CheckboxGap),
                FlowDirection = ControlFlowDirection.LeftToRight,
                ControlPadding = new Vector2(15, CheckboxGap),
                Parent = parent
            };

            foreach (var option in optionList)
            {
                var checkbox = SparkFormLayout.AddCheckbox(
                    grid,
                    option.Value,
                    isChecked(option.Key),
                    CheckboxWidth,
                    CheckboxHeight);

                checkbox.CheckedChanged += (s, e) =>
                {
                    if (!_isRefreshing)
                        onToggle(option.Key);
                };

                trackCheckbox(checkbox, option.Key);
            }
        }

        protected override void Unload()
        {
            _session.StatusChanged -= HandleStatusChanged;
            _session.ProfileChanged -= HandleProfileChanged;
        }

        private void HandleStatusChanged(string statusText)
        {
            SparkUiThread.Queue(() =>
            {
                if (_status?.Parent != null)
                    _status.Text = statusText ?? string.Empty;
            });
        }

        private void HandleProfileChanged()
        {
            SparkUiThread.Queue(() =>
            {
                if (_experienceDropdown?.Parent != null)
                    RefreshFromSession();
            });
        }

        private void RefreshFromSession()
        {
            _isRefreshing = true;

            try
            {
                _experienceDropdown.SelectedItem = ProfileLabels.GetExperienceLabel(_session.Profile.Experience);

                foreach (var pair in _preferenceCheckboxes)
                    pair.Key.Checked = (_session.Profile.Preferences & pair.Value) == pair.Value;

                foreach (var pair in _themeCheckboxes)
                    pair.Key.Checked = (_session.Profile.Themes & pair.Value) == pair.Value;

                foreach (var pair in _styleCheckboxes)
                    pair.Key.Checked = (_session.Profile.Styles & pair.Value) == pair.Value;
            }
            finally
            {
                _isRefreshing = false;
            }
        }

        private static ProfilePreferenceFlags ToggleFlag(ProfilePreferenceFlags current, ProfilePreferenceFlags value)
        {
            return (current & value) == value
                ? current & ~value
                : current | value;
        }

        private static ProfileThemeFlags ToggleFlag(ProfileThemeFlags current, ProfileThemeFlags value)
        {
            return (current & value) == value
                ? current & ~value
                : current | value;
        }

        private static ProfileStyleFlags ToggleFlag(ProfileStyleFlags current, ProfileStyleFlags value)
        {
            return (current & value) == value
                ? current & ~value
                : current | value;
        }

    }
}
