using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Graphics.UI;
using Microsoft.Xna.Framework;
using rp.spark.Models;
using rp.spark.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace rp.spark.UI.Views
{
    public class ProfileManagementView : View
    {
        private const int FormWidth = 760;
        private const int FormHeight = 360;
        private const int StatusY = 390;

        private readonly ProfileEditorSession _session;

        private Label _status;
        private Label _activeStatus;
        private Label _profileTip;
        private Dropdown _profileDropdown;
        private Dropdown _importChar;
        private Dropdown _importProfile;
        private StandardButton _importButton;
        private TextBox _profileName;
        private Dictionary<string, string> _profileOptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, string> _importOptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private bool _isRefreshing;
        private bool _deleteConfirmArmed;
        private CancellationTokenSource _importRefreshCancel;

        public ProfileManagementView(ProfileEditorSession session)
        {
            _session = session;
        }

        protected override void Build(Container buildPanel)
        {
            if (!_session.State.CanEditProfile)
            {
                ProfileEditorUI.ShowUnavailableMessage(buildPanel);
                return;
            }

            var form = SparkFormLayout.AddVerticalStack(buildPanel, 0, 0, FormWidth, FormHeight, 15);

            var profileGroup = SparkFormLayout.AddAutoStack(form, FormWidth, 0);

            SparkFormLayout.AddLabel(profileGroup, "Profile", FormWidth);
            _profileDropdown = SparkFormLayout.AddDropdown(profileGroup, new string[0], null, 300);

            _profileDropdown.ValueChanged += (s, e) =>
            {
                if (_isRefreshing)
                    return;

                var selectedItem = _profileDropdown.SelectedItem?.ToString() ?? string.Empty;

                if (_profileOptions.TryGetValue(selectedItem, out var profileId))
                    _session.SelectProfile(profileId);
            };

            var nameGroup = SparkFormLayout.AddAutoStack(form, FormWidth, 0);

            SparkFormLayout.AddLabel(nameGroup, "Profile Name", FormWidth);
            var nameRow = SparkFormLayout.AddRow(nameGroup, FormWidth, 35, 25);
            _profileName = SparkFormLayout.AddTextBox(
                nameRow,
                string.Empty,
                string.Empty,
                360,
                maxLength: ProfileLimits.MaxProfileNameLength);

            _profileName.TextChanged += (s, e) =>
            {
                if (!_isRefreshing)
                    _session.Profile.ProfileName = _profileName.Text?.Trim() ?? string.Empty;
            };

            _activeStatus = SparkFormLayout.AddLabel(
                nameRow,
                string.Empty,
                320,
                30,
                GameService.Content.DefaultFont16,
                new Color(220, 220, 220));

            BuildActions(form);
            BuildFooter(buildPanel);

            _session.StatusChanged += HandleStatusChanged;
            _session.ProfileChanged += HandleProfileChanged;
            _session.ImportsChanged += HandleImportsChanged;
            RefreshFromSession();
            StartImportRefresh();
        }

        private void BuildActions(Container buildPanel)
        {
            var buttonRow = SparkFormLayout.AddRow(buildPanel, FormWidth, 35, 15);
            var newButton = SparkFormLayout.AddButton(buttonRow, "New", 95);

            newButton.Click += (s, e) => RunProfileAction(() => _session.CreateProfile());

            var duplicateButton = SparkFormLayout.AddButton(buttonRow, "Duplicate", 115);

            duplicateButton.Click += (s, e) => RunProfileAction(() => _session.DuplicateProfile());

            var saveButton = SparkFormLayout.AddButton(buttonRow, "Save", 95);

            SparkUiActions.BindClick(
                saveButton,
                async () =>
                {
                    _deleteConfirmArmed = false;
                    await _session.SaveAsync();
                },
                _session.SetStatus,
                "Couldn't save profile.");

            var activeButton = SparkFormLayout.AddButton(buttonRow, "Set Active", 115);

            SparkUiActions.BindClick(
                activeButton,
                async () =>
                {
                    _deleteConfirmArmed = false;
                    await _session.SetActiveAsync();
                },
                _session.SetStatus,
                "Couldn't set active profile.");

            var deleteButton = SparkFormLayout.AddButton(buttonRow, "Delete", 95);

            deleteButton.Click += (s, e) =>
            {
                if (!_deleteConfirmArmed)
                {
                    _deleteConfirmArmed = true;
                    _session.SetStatus($"Click Delete again to permanently delete {GetProfileName(_session.Profile)}.");
                    return;
                }

                RunProfileAction(() => _session.DeleteProfile());
            };

            BuildImport(buildPanel);

            _profileTip = SparkFormLayout.AddLabel(
                buildPanel,
                string.Empty,
                FormWidth,
                50,
                GameService.Content.DefaultFont14,
                SparkViewUI.SecondaryTextColor);
            _profileTip.WrapText = true;
        }

        private void BuildImport(Container buildPanel)
        {
            var importGroup = SparkFormLayout.AddAutoStack(buildPanel, FormWidth, 5);

            SparkFormLayout.AddLabel(importGroup, "Import from another character", FormWidth);
            var importRow = SparkFormLayout.AddRow(importGroup, FormWidth, 35, 10);

            _importChar = SparkFormLayout.AddDropdown(importRow, new string[0], null, 250);
            _importProfile = SparkFormLayout.AddDropdown(importRow, new string[0], null, 250);
            _importProfile.Enabled = false;

            _importButton = SparkFormLayout.AddButton(importRow, "Import Profile", 140, enabled: false);

            _importChar.ValueChanged += (s, e) =>
            {
                if (!_isRefreshing)
                    RefreshImportProfiles();
            };

            _importProfile.ValueChanged += (s, e) =>
            {
                if (!_isRefreshing)
                    UpdateImportButton();
            };

            SparkUiActions.BindClick(
                _importButton,
                async () =>
                {
                    _deleteConfirmArmed = false;
                    await _session.ImportAsync(GetSelectedImportId());
                },
                _session.SetStatus,
                "Couldn't import profile.");
        }

        private void BuildFooter(Container buildPanel)
        {
            _status = ProfileEditorUI.AddStatusLabel(
                buildPanel,
                _session.StatusText,
                new Point(0, StatusY),
                new Point(760, 30));

            ProfileEditorUI.AddHeaderLabel(buildPanel, _session);
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
                if (_profileDropdown?.Parent == null)
                    return;

                _deleteConfirmArmed = false;
                RefreshFromSession();
            });
        }

        private void HandleImportsChanged()
        {
            SparkUiThread.Queue(() =>
            {
                if (_importChar?.Parent != null)
                    RefreshFromSession();
            });
        }

        private void StartImportRefresh()
        {
            _importRefreshCancel?.Cancel();
            _importRefreshCancel = new CancellationTokenSource();

            _ = RefreshImportsSoonAsync(_importRefreshCancel.Token);
        }

        private async Task RefreshImportsSoonAsync(CancellationToken cancellationToken)
        {
            try
            {
                for (var attempt = 0; attempt < 24; attempt++)
                {
                    if (cancellationToken.IsCancellationRequested || _session.HasImportState)
                        return;

                    await _session.RefreshImportsAsync(quiet: true);

                    if (_session.HasImportState)
                        return;

                    await Task.Delay(5000, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private void RefreshFromSession()
        {
            _isRefreshing = true;

            try
            {
                RefreshProfileDropdown();
                RefreshImportDropdowns();

                _profileName.Text = GetProfileName(_session.Profile);
                _activeStatus.Text = _session.IsSelectedProfileActive
                    ? "Active for broadcast"
                    : "Not active for broadcast";
                _profileTip.Text = GetProfileTip();
            }
            finally
            {
                _isRefreshing = false;
            }
        }

        private void RefreshProfileDropdown()
        {
            _profileOptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _profileDropdown.Items.Clear();

            var duplicateNames = _session.Profiles
                .GroupBy(profile => GetProfileName(profile), StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToList();

            string selectedLabel = null;

            foreach (var profile in _session.Profiles)
            {
                var label = GetProfileOptionLabel(profile, duplicateNames);
                _profileOptions[label] = profile.ProfileId;
                _profileDropdown.Items.Add(label);

                if (string.Equals(profile.ProfileId, _session.Profile.ProfileId, StringComparison.OrdinalIgnoreCase))
                    selectedLabel = label;
            }

            if (!string.IsNullOrWhiteSpace(selectedLabel))
                _profileDropdown.SelectedItem = selectedLabel;
        }

        private void RefreshImportDropdowns()
        {
            if (_importChar == null || _importProfile == null || _importButton == null)
                return;

            var selectedChar = _importChar.SelectedItem?.ToString();
            _importChar.Items.Clear();

            if (_session.ImportGroups.Count == 0)
            {
                var emptyText = _session.HasImportState
                    ? "No profiles found"
                    : "Waiting for SPARK sync";

                _importChar.Items.Add(emptyText);
                _importChar.SelectedItem = emptyText;
                _importChar.Enabled = false;
                _importProfile.Items.Clear();
                _importProfile.Enabled = false;
                _importButton.Enabled = false;
                _importOptions.Clear();
                return;
            }

            foreach (var group in _session.ImportGroups)
                _importChar.Items.Add(group.CharacterName);

            if (string.IsNullOrWhiteSpace(selectedChar)
                || !_session.ImportGroups.Any(group => string.Equals(group.CharacterName, selectedChar, StringComparison.OrdinalIgnoreCase)))
                selectedChar = _session.ImportGroups[0].CharacterName;

            _importChar.Enabled = true;
            _importChar.SelectedItem = selectedChar;
            RefreshImportProfiles();
        }

        private void RefreshImportProfiles()
        {
            _importOptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _importProfile.Items.Clear();

            var selectedChar = _importChar.SelectedItem?.ToString() ?? string.Empty;
            var group = _session.ImportGroups.FirstOrDefault(importGroup =>
                string.Equals(importGroup.CharacterName, selectedChar, StringComparison.OrdinalIgnoreCase));

            if (group == null || group.Profiles.Count == 0)
            {
                _importProfile.Enabled = false;
                _importButton.Enabled = false;
                return;
            }

            foreach (var profile in group.Profiles)
            {
                var label = GetProfileName(profile);
                _importOptions[label] = profile.ProfileId;
                _importProfile.Items.Add(label);
            }

            _importProfile.Enabled = true;
            _importProfile.SelectedItem = group.Profiles.Count > 0 ? GetProfileName(group.Profiles[0]) : null;
            UpdateImportButton();
        }

        private void UpdateImportButton()
        {
            if (_importButton != null)
                _importButton.Enabled = !string.IsNullOrWhiteSpace(GetSelectedImportId());
        }

        private string GetSelectedImportId()
        {
            var selectedItem = _importProfile?.SelectedItem?.ToString() ?? string.Empty;
            return _importOptions.TryGetValue(selectedItem, out var profileId)
                ? profileId
                : string.Empty;
        }

        private void RunProfileAction(Action action)
        {
            _deleteConfirmArmed = false;

            try
            {
                action();
            }
            catch (Exception ex)
            {
                _session.SetStatus(ex.Message);
            }
        }

        protected override void Unload()
        {
            _importRefreshCancel?.Cancel();
            _importRefreshCancel?.Dispose();
            _importRefreshCancel = null;

            _session.StatusChanged -= HandleStatusChanged;
            _session.ProfileChanged -= HandleProfileChanged;
            _session.ImportsChanged -= HandleImportsChanged;
        }

        private string GetProfileOptionLabel(CharacterProfile profile, IEnumerable<string> duplicateNames)
        {
            var name = GetProfileName(profile);
            var label = duplicateNames.Contains(name, StringComparer.OrdinalIgnoreCase)
                ? $"{name} [{profile.ProfileId.Substring(0, Math.Min(4, profile.ProfileId.Length))}]"
                : name;

            if (!string.IsNullOrWhiteSpace(_session.ActiveProfileId)
                && string.Equals(profile.ProfileId, _session.ActiveProfileId, StringComparison.OrdinalIgnoreCase))
                label += " (active)";

            return label;
        }

        private static string GetProfileName(CharacterProfile profile)
        {
            return string.IsNullOrWhiteSpace(profile?.ProfileName)
                ? "Default"
                : profile.ProfileName.Trim();
        }

        private string GetProfileTip()
        {
            if (_session.Profiles.Count == 0)
                return "You have no profiles! Click the 'New' button to start!";

            if (string.IsNullOrWhiteSpace(_session.ActiveProfileId))
                return "You need to set your profile to active for it to be broadcast. Your active profile is the one shown to others.";

            return "Tip: You can make multiple profiles per character and set whichever one to active depending on who you want to RP as today!";
        }
    }
}
