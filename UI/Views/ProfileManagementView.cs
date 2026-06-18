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
    public class ProfileManagementView : View
    {
        private const int FormWidth = 760;
        private const int FormHeight = 275;
        private const int StatusY = 300;
        private const int ApiKeyWarningHeight = 44;

        private readonly ProfileEditorSession _session;

        private Label _status;
        private Label _apiKeyWarning;
        private Label _activeStatus;
        private Label _profileTip;
        private Dropdown _profileDropdown;
        private TextBox _profileName;
        private Dictionary<string, string> _profileOptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private bool _isRefreshing;
        private bool _deleteConfirmArmed;

        public ProfileManagementView(ProfileEditorSession session)
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
            BuildApiKeyWarning(form);

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
            RefreshFromSession();
        }

        private void BuildApiKeyWarning(FlowPanel form)
        {
            _apiKeyWarning = SparkFormLayout.AddLabel(
                form,
                string.Empty,
                FormWidth,
                ApiKeyWarningHeight,
                GameService.Content.DefaultFont16,
                SparkViewUI.WarningTextColor,
                true);
            _apiKeyWarning.WrapText = true;
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

            _profileTip = SparkFormLayout.AddLabel(
                buildPanel,
                string.Empty,
                FormWidth,
                50,
                GameService.Content.DefaultFont14,
                SparkViewUI.SecondaryTextColor);
            _profileTip.WrapText = true;
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

        private void RefreshFromSession()
        {
            _isRefreshing = true;

            try
            {
                RefreshProfileDropdown();

                _profileName.Text = GetProfileName(_session.Profile);
                _activeStatus.Text = _session.IsSelectedProfileActive
                    ? "Active for broadcast"
                    : "Not active for broadcast";
                _profileTip.Text = GetProfileTip();
                RefreshApiKeyWarning();
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
            _session.StatusChanged -= HandleStatusChanged;
            _session.ProfileChanged -= HandleProfileChanged;
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

        private void RefreshApiKeyWarning()
        {
            if (_apiKeyWarning == null)
                return;

            var showWarning = !_session.InitialState.HasCharactersPermission;
            _apiKeyWarning.Text = showWarning ? SparkViewUI.MissingApiKeyWarning : string.Empty;
            _apiKeyWarning.Height = showWarning ? ApiKeyWarningHeight : 0;
            _apiKeyWarning.Visible = showWarning;
        }

    }
}
