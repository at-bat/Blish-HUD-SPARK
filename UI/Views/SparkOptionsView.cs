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

        private static readonly string[] ProfileTooltipLineOptions =
        {
            "2",
            "4",
            "6",
            "8",
            "10",
            "12",
            "14",
            "16",
            "18",
            "20"
        };

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
        private Checkbox _showKnownForTooltipsCheckbox;
        private Checkbox _showCurrentlyTooltipsCheckbox;
        private Checkbox _showOocTooltipsCheckbox;
        private Checkbox _trimLongTooltipsCheckbox;
        private Dropdown _profileTooltipLinesDropdown;
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
            _shareCheckbox.BasicTooltipText =
                "When unchecked, your profile will not be uploaded to SPARK. This means anyone viewing a local copy of your profile will not receive updates, even if you're set to Invisible.";
            _shareCheckbox.CheckedChanged += (s, e) =>
            {
                if (_settings.BroadcastProfile.Value == _shareCheckbox.Checked)
                    return;

                _settings.BroadcastProfile.Value = _shareCheckbox.Checked;
                _requestServerSync?.Invoke();
            };

            _hideLocationCheckbox = SparkFormLayout.AddCheckbox(stack, "Hide my location", _settings.HideLocation.Value, 220, ControlHeight);
            _hideLocationCheckbox.BasicTooltipText =
                "When checked, your location will be set to 'Hidden' for all location fields in SPARK.";
            _hideLocationCheckbox.CheckedChanged += (s, e) =>
            {
                if (_settings.HideLocation.Value == _hideLocationCheckbox.Checked)
                    return;

                _settings.HideLocation.Value = _hideLocationCheckbox.Checked;
                _requestServerSync?.Invoke();
            };

            _showNearbyCheckbox = SparkFormLayout.AddCheckbox(stack, "Show me nearby", _settings.ShowNearbyPresence.Value, 220, ControlHeight);
            _showNearbyCheckbox.BasicTooltipText =
                "When checked, others using the Nearby Players window will be able to see you if you're on the same map and how far away you are.";
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

            _regionDropdown.BasicTooltipText =
                "Set which region to broadcast your profile to in order for players in the same region to be able to find and contact you.";

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
            _autoHideCheckbox.BasicTooltipText =
                "Closes all SPARK windows when loading new maps or on character select.";
            _autoHideCheckbox.CheckedChanged += (s, e) =>
            {
                if (_settings.AutoHideGameUi.Value == _autoHideCheckbox.Checked)
                    return;

                _settings.AutoHideGameUi.Value = _autoHideCheckbox.Checked;
            }; ;

            _cornerIconCheckbox = SparkFormLayout.AddCheckbox(
                stack,
                "Show SPARK icon",
                _settings.ShowCornerIcon.Value,
                220,
                ControlHeight);

            _cornerIconCheckbox.BasicTooltipText =
                "Displays an icon with quick access to SPARK windows and settings at the top of the screen.";

            _cornerIconCheckbox.CheckedChanged += (s, e) =>
            {
                if (_settings.ShowCornerIcon.Value == _cornerIconCheckbox.Checked)
                    return;

                _settings.ShowCornerIcon.Value = _cornerIconCheckbox.Checked;
            };

            SparkFormLayout.AddSpacer(stack, ContentWidth, 8);

            SparkFormLayout.AddLabel(
                stack,
                "Profile Tooltips",
                ContentWidth,
                28,
                GameService.Content.DefaultFont18,
                new Color(255, 233, 180),
                true);

            _showKnownForTooltipsCheckbox = SparkFormLayout.AddCheckbox(
                stack,
                "Include Known For",
                _settings.ShowKnownForInProfileTooltips.Value,
                260,
                ControlHeight);

            _showKnownForTooltipsCheckbox.BasicTooltipText =
                "Shows Known For information in tooltips when a profile provides it.";

            _showKnownForTooltipsCheckbox.CheckedChanged += (s, e) =>
            {
                if (_settings.ShowKnownForInProfileTooltips.Value !=
                    _showKnownForTooltipsCheckbox.Checked)
                {
                    _settings.ShowKnownForInProfileTooltips.Value =
                        _showKnownForTooltipsCheckbox.Checked;
                }
            };

            _showCurrentlyTooltipsCheckbox = SparkFormLayout.AddCheckbox(
                stack,
                "Include Currently",
                _settings.ShowCurrentlyInProfileTooltips.Value,
                260,
                ControlHeight);

            _showCurrentlyTooltipsCheckbox.BasicTooltipText =
                "Shows Currently (in character) information in tooltips when a profile provides it.";

            _showCurrentlyTooltipsCheckbox.CheckedChanged += (s, e) =>
            {
                if (_settings.ShowCurrentlyInProfileTooltips.Value !=
                    _showCurrentlyTooltipsCheckbox.Checked)
                {
                    _settings.ShowCurrentlyInProfileTooltips.Value =
                        _showCurrentlyTooltipsCheckbox.Checked;
                }
            };

            _showOocTooltipsCheckbox = SparkFormLayout.AddCheckbox(
                stack,
                "Include OOC info",
                _settings.ShowOocInfoInProfileTooltips.Value,
                260,
                ControlHeight);

            _showOocTooltipsCheckbox.BasicTooltipText =
                "Show Out of Character information in tooltips when a profile provides it.";

            _showOocTooltipsCheckbox.CheckedChanged += (s, e) =>
            {
                if (_settings.ShowOocInfoInProfileTooltips.Value !=
                    _showOocTooltipsCheckbox.Checked)
                {
                    _settings.ShowOocInfoInProfileTooltips.Value =
                        _showOocTooltipsCheckbox.Checked;
                }
            };

            var tooltipLinesRow = SparkFormLayout.AddRow(stack, ContentWidth, ControlHeight, 8);

            _trimLongTooltipsCheckbox = SparkFormLayout.AddCheckbox(
                tooltipLinesRow,
                "Trim long profile tooltips",
                _settings.TrimLongProfileTooltips.Value,
                245,
                ControlHeight);

            _trimLongTooltipsCheckbox.BasicTooltipText =
                "Limits each enabled tooltip section to the configured number of wrapped lines.";

            _trimLongTooltipsCheckbox.CheckedChanged += (s, e) =>
            {
                if (_settings.TrimLongProfileTooltips.Value !=
                    _trimLongTooltipsCheckbox.Checked)
                {
                    _settings.TrimLongProfileTooltips.Value =
                        _trimLongTooltipsCheckbox.Checked;
                }

                SyncTooltipSettings();
            };

            _profileTooltipLinesDropdown = SparkFormLayout.AddDropdown(
                tooltipLinesRow,
                ProfileTooltipLineOptions,
                SparkSettings.ProfileTooltipLimit(
                    _settings.ProfileTooltipLinesPerSection.Value).ToString(),
                70,
                ControlHeight);

            SparkFormLayout.AddLabel(
                tooltipLinesRow,
                "max lines per section",
                145,
                ControlHeight,
                GameService.Content.DefaultFont14);

            _profileTooltipLinesDropdown.BasicTooltipText =
                "Applied separately to Known For, Currently, and Out of Character.";

            _profileTooltipLinesDropdown.ValueChanged += (s, e) =>
            {
                if (!int.TryParse(
                    _profileTooltipLinesDropdown.SelectedItem?.ToString(),
                    out var selectedLimit))
                {
                    return;
                }

                selectedLimit =
                    SparkSettings.ProfileTooltipLimit(selectedLimit);

                if (_settings.ProfileTooltipLinesPerSection.Value != selectedLimit)
                {
                    _settings.ProfileTooltipLinesPerSection.Value =
                        selectedLimit;
                }
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
            _settings.ShowKnownForInProfileTooltips.SettingChanged += OnSettingChanged;
            _settings.ShowCurrentlyInProfileTooltips.SettingChanged += OnSettingChanged;
            _settings.ShowOocInfoInProfileTooltips.SettingChanged += OnSettingChanged;
            _settings.TrimLongProfileTooltips.SettingChanged += OnSettingChanged;
            _settings.ProfileTooltipLinesPerSection.SettingChanged += OnTooltipLineLimitChanged;
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
            _settings.ShowKnownForInProfileTooltips.SettingChanged -= OnSettingChanged;
            _settings.ShowCurrentlyInProfileTooltips.SettingChanged -= OnSettingChanged;
            _settings.ShowOocInfoInProfileTooltips.SettingChanged -= OnSettingChanged;
            _settings.TrimLongProfileTooltips.SettingChanged -= OnSettingChanged;
            _settings.ProfileTooltipLinesPerSection.SettingChanged -= OnTooltipLineLimitChanged;
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
            SetChecked(_showKnownForTooltipsCheckbox, _settings.ShowKnownForInProfileTooltips.Value);
            SetChecked(_showCurrentlyTooltipsCheckbox, _settings.ShowCurrentlyInProfileTooltips.Value);
            SetChecked(_showOocTooltipsCheckbox, _settings.ShowOocInfoInProfileTooltips.Value);
            SetChecked(_trimLongTooltipsCheckbox, _settings.TrimLongProfileTooltips.Value);

            SyncTooltipSettings();
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

        private void OnTooltipLineLimitChanged(object sender, ValueChangedEventArgs<int> e)
        {
            SparkUiThread.Queue(() =>
            {
                if (!_isUnloaded)
                    SyncTooltipSettings();
            });
        }

        private void SyncTooltipSettings()
        {
            if (_profileTooltipLinesDropdown == null)
                return;

            var normalizedLimit = SparkSettings.ProfileTooltipLimit(_settings.ProfileTooltipLinesPerSection.Value);

            if (_settings.ProfileTooltipLinesPerSection.Value != normalizedLimit)
                _settings.ProfileTooltipLinesPerSection.Value = normalizedLimit;

            var limitText = normalizedLimit.ToString();

            if (!string.Equals(
                _profileTooltipLinesDropdown.SelectedItem?.ToString(),
                limitText,
                StringComparison.Ordinal))
            {
                _profileTooltipLinesDropdown.SelectedItem = limitText;
            }

            _profileTooltipLinesDropdown.Enabled =
                _settings.TrimLongProfileTooltips.Value;
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
            _showKnownForTooltipsCheckbox = null;
            _showCurrentlyTooltipsCheckbox = null;
            _showOocTooltipsCheckbox = null;
            _trimLongTooltipsCheckbox = null;
            _profileTooltipLinesDropdown = null;
            _matureProfilesConfirm.Dispose();
            _regionDropdown = null;
            _matureProfilesButton = null;
        }
    }
}