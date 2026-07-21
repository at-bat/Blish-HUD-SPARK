using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Graphics.UI;
using Microsoft.Xna.Framework;
using rp.spark.Services;
using rp.spark.Models;
using System;

namespace rp.spark.UI.Views
{
    public class SparkOptionsView : View
    {
        private const int ContentWidth = 660;
        private const int ControlHeight = 30;

        private readonly SparkSettings _settings;
        private readonly Action _requestServerSync;
        private readonly Action<bool> _setNearbySharing;
        private Dropdown _regionDropdown;
        private StandardButton _matureProfilesButton;
        private readonly MatureProfilesConfirmation _matureProfilesConfirm;

        private Checkbox _shareCheckbox;
        private Checkbox _hideLocationCheckbox;
        private Checkbox _showNearbyCheckbox;
        private Checkbox _autoHideCheckbox;
        private Checkbox _cornerIconCheckbox;
        private bool _isUnloaded;

        public SparkOptionsView(
            SparkSettings settings,
            Action requestServerSync,
            Action<bool> setNearbySharing,
            Action<bool> maturePreferenceChanged)
        {
            _settings = settings;
            _requestServerSync = requestServerSync;
            _setNearbySharing = setNearbySharing;
            _matureProfilesConfirm = new MatureProfilesConfirmation(settings, maturePreferenceChanged);
        }

        protected override void Build(Container buildPanel)
        {
            var stack = SparkFormLayout.AddVerticalStack(
                buildPanel,
                8,
                8,
                ContentWidth,
                560,
                8,
                true);

            SparkFormLayout.AddLabel(stack, "Privacy", ContentWidth, 28, GameService.Content.DefaultFont18, new Color(255, 233, 180), true);

            _shareCheckbox = SparkFormLayout.AddCheckbox(stack, "Share my profile", _settings.BroadcastProfile.Value, 220, ControlHeight);
            _shareCheckbox.CheckedChanged += (s, e) =>
            {
                if (_settings.BroadcastProfile.Value == _shareCheckbox.Checked)
                    return;

                _settings.BroadcastProfile.Value = _shareCheckbox.Checked;
                _requestServerSync?.Invoke();
            };

            _hideLocationCheckbox = SparkFormLayout.AddCheckbox(stack, "Hide my location", _settings.HideLocation.Value, 220, ControlHeight);
            _hideLocationCheckbox.CheckedChanged += (s, e) =>
            {
                if (_settings.HideLocation.Value == _hideLocationCheckbox.Checked)
                    return;

                _settings.HideLocation.Value = _hideLocationCheckbox.Checked;
                _requestServerSync?.Invoke();
            };

            _showNearbyCheckbox = SparkFormLayout.AddCheckbox(stack, "Show me nearby", _settings.ShowNearbyPresence.Value, 220, ControlHeight);
            _showNearbyCheckbox.CheckedChanged += (s, e) =>
            {
                if (_settings.ShowNearbyPresence.Value == _showNearbyCheckbox.Checked)
                    return;

                if (_setNearbySharing != null)
                    _setNearbySharing(_showNearbyCheckbox.Checked);
                else
                    _settings.ShowNearbyPresence.Value = _showNearbyCheckbox.Checked;
            };

            SparkFormLayout.AddSpacer(stack, ContentWidth, 8);
            SparkFormLayout.AddLabel(stack, "Profile Discovery", ContentWidth, 28, GameService.Content.DefaultFont18, new Color(255, 233, 180), true);

            var discoveryRow = SparkFormLayout.AddRow(stack, ContentWidth, ControlHeight, 8);

            SparkFormLayout.AddLabel(
                discoveryRow,
                "Region:",
                58,
                ControlHeight,
                GameService.Content.DefaultFont14);

            _regionDropdown = SparkFormLayout.AddDropdown(
                discoveryRow,
                new[] { ProfileRegion.NA.ToString(), ProfileRegion.EU.ToString() },
                _settings.RegionFilter.Value.ToString(),
                90,
                ControlHeight);

            _regionDropdown.ValueChanged += (s, e) =>
            {
                if (Enum.TryParse(_regionDropdown.SelectedItem?.ToString(), out ProfileRegion selectedRegion))
                {
                    if (_settings.RegionFilter.Value == selectedRegion)
                        return;

                    _settings.RegionFilter.Value = selectedRegion;
                    _requestServerSync?.Invoke();
                }
            };

            _matureProfilesButton = SparkFormLayout.AddButton(
                discoveryRow,
                _matureProfilesConfirm.ButtonText,
                190,
                ControlHeight);

            _matureProfilesButton.Click += (s, e) =>
            {
                _matureProfilesConfirm.Toggle(buildPanel);
                SyncMatureButtonFromSettings();
            };

            SparkFormLayout.AddSpacer(stack, ContentWidth, 8);
            SparkFormLayout.AddLabel(stack, "Interface", ContentWidth, 28, GameService.Content.DefaultFont18, new Color(255, 233, 180), true);

            _autoHideCheckbox = SparkFormLayout.AddCheckbox(stack, "Auto-hide UI", _settings.AutoHideGameUi.Value, 220, ControlHeight);
            _autoHideCheckbox.CheckedChanged += (s, e) =>
            {
                if (_settings.AutoHideGameUi.Value == _autoHideCheckbox.Checked)
                    return;

                _settings.AutoHideGameUi.Value = _autoHideCheckbox.Checked;
            }; ;

            _cornerIconCheckbox = SparkFormLayout.AddCheckbox(stack, "Show SPARK icon", _settings.ShowCornerIcon.Value, 220, ControlHeight);
            _cornerIconCheckbox.CheckedChanged += (s, e) =>
            {
                if (_settings.ShowCornerIcon.Value == _cornerIconCheckbox.Checked)
                    return;

                _settings.ShowCornerIcon.Value = _cornerIconCheckbox.Checked;
            };

            WatchSettings();
            SyncCheckboxesFromSettings();
        }
        private void WatchSettings()
        {
            _isUnloaded = false;

            _settings.BroadcastProfile.SettingChanged += OnSettingChanged;
            _settings.HideLocation.SettingChanged += OnSettingChanged;
            _settings.ShowNearbyPresence.SettingChanged += OnSettingChanged;
            _settings.AutoHideGameUi.SettingChanged += OnSettingChanged;
            _settings.ShowCornerIcon.SettingChanged += OnSettingChanged;
            _settings.RegionFilter.SettingChanged += OnRegionFilterChanged;
            _settings.ShowMatureProfiles.SettingChanged += OnSettingChanged;
        }

        private void UnwatchSettings()
        {
            _settings.BroadcastProfile.SettingChanged -= OnSettingChanged;
            _settings.HideLocation.SettingChanged -= OnSettingChanged;
            _settings.ShowNearbyPresence.SettingChanged -= OnSettingChanged;
            _settings.AutoHideGameUi.SettingChanged -= OnSettingChanged;
            _settings.ShowCornerIcon.SettingChanged -= OnSettingChanged;
            _settings.RegionFilter.SettingChanged -= OnRegionFilterChanged;
            _settings.ShowMatureProfiles.SettingChanged -= OnSettingChanged;
        }

        private void OnSettingChanged(object sender, ValueChangedEventArgs<bool> e)
        {
            SparkUiThread.Queue(() =>
            {
                if (!_isUnloaded)
                    SyncCheckboxesFromSettings();
            });
        }

        private void SyncCheckboxesFromSettings()
        {
            SetChecked(_shareCheckbox, _settings.BroadcastProfile.Value);
            SetChecked(_hideLocationCheckbox, _settings.HideLocation.Value);
            SetChecked(_showNearbyCheckbox, _settings.ShowNearbyPresence.Value);
            SetChecked(_autoHideCheckbox, _settings.AutoHideGameUi.Value);
            SetChecked(_cornerIconCheckbox, _settings.ShowCornerIcon.Value);

            SyncRegionDropdownFromSettings();
            SyncMatureButtonFromSettings();
        }

        private static void SetChecked(Checkbox checkbox, bool value)
        {
            if (checkbox != null && checkbox.Checked != value)
                checkbox.Checked = value;
        }

        private void OnRegionFilterChanged(object sender, ValueChangedEventArgs<ProfileRegion> e)
        {
            SparkUiThread.Queue(() =>
            {
                if (!_isUnloaded)
                    SyncRegionDropdownFromSettings();
            });
        }

        private void SyncRegionDropdownFromSettings()
        {
            if (_regionDropdown == null)
                return;

            var label = _settings.RegionFilter.Value.ToString();

            if (!string.Equals(_regionDropdown.SelectedItem?.ToString(), label, StringComparison.Ordinal))
                _regionDropdown.SelectedItem = label;
        }

        private void SyncMatureButtonFromSettings()
        {
            if (_matureProfilesButton != null)
                _matureProfilesButton.Text = _matureProfilesConfirm.ButtonText;
        }

        protected override void Unload()
        {
            _isUnloaded = true;
            UnwatchSettings();

            _shareCheckbox = null;
            _hideLocationCheckbox = null;
            _showNearbyCheckbox = null;
            _autoHideCheckbox = null;
            _cornerIconCheckbox = null;
            _matureProfilesConfirm.Dispose();
            _regionDropdown = null;
            _matureProfilesButton = null;
        }
    }
}