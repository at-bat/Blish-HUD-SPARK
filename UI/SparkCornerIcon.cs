using Blish_HUD;
using Blish_HUD.Content;
using Blish_HUD.Controls;
using Blish_HUD.Input;
using Blish_HUD.Modules.Managers;
using rp.spark.Services;
using rp.spark.Models;
using rp.spark.UI.Controls;
using rp.spark.UI.Views;
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace rp.spark.UI
{
    internal sealed class SparkCornerIcon : IDisposable
    {
        private const int MinimumMenuWidth = 190;
        private const int MenuItemTextPadding = 72;

        private readonly SparkSettings _settings;
        private readonly AsyncTexture2D _icon;
        private readonly AsyncTexture2D _hoverIcon;
        private readonly bool _disposeHoverIcon;
        private readonly Action _openMyProfile;
        private readonly Action _openProfileManager;
        private readonly Action _openOnlineList;
        private readonly Action _openNearby;
        private readonly Action _openSavedProfiles;
        private readonly Action _openBlocklist;
        private readonly Action _openSettings;
        private readonly Action _requestServerSync;
        private readonly Action<bool> _setNearbySharing;
        private readonly Func<ServerSyncStatus> _getServerSyncStatus;
        private readonly Action<Action<ServerSyncStatus>> _watchServerSyncStatus;
        private readonly Action<Action<ServerSyncStatus>> _unwatchServerSyncStatus;
        private readonly Func<string> _getImportantNotice;

        private CornerIcon _cornerIcon;
        private ContextMenuStrip _menu;
        private ContextMenuStripItem _myProfileItem;
        private ContextMenuStripItem _profileEditorItem;
        private ContextMenuStripItem _onlineListItem;
        private ContextMenuStripItem _nearbyPlayersItem;
        private ContextMenuStripItem _savedProfilesItem;
        private ContextMenuStripItem _statusMenuItem;
        private ContextMenuColours _readinessMenuItem;
        private ContextMenuColours _serverStatusMenuItem;
        private readonly Dictionary<RPStatus, ContextMenuStripItem> _statusMenuItems = new Dictionary<RPStatus, ContextMenuStripItem>();
        private bool _isSyncingStatusMenu;
        private bool _isDisposed;

        public SparkCornerIcon(
            SparkSettings settings,
            ContentsManager contentsManager,
            Action openMyProfile,
            Action openProfileManager,
            Action openOnlineList,
            Action openNearby,
            Action openSavedProfiles,
            Action openBlocklist,
            Action openSettings,
            Action requestServerSync,
            Action<bool> setNearbySharing,
            Func<ServerSyncStatus> getServerSyncStatus,
            Func<string> getImportantNotice,
            Action<Action<ServerSyncStatus>> watchServerSyncStatus,
            Action<Action<ServerSyncStatus>> unwatchServerSyncStatus)
        {
            _settings = settings;
            _openMyProfile = openMyProfile;
            _openProfileManager = openProfileManager;
            _openOnlineList = openOnlineList;
            _openNearby = openNearby;
            _openSavedProfiles = openSavedProfiles;
            _openBlocklist = openBlocklist;
            _openSettings = openSettings;
            _requestServerSync = requestServerSync;
            _setNearbySharing = setNearbySharing;
            _getServerSyncStatus = getServerSyncStatus;
            _getImportantNotice = getImportantNotice;
            _watchServerSyncStatus = watchServerSyncStatus;
            _unwatchServerSyncStatus = unwatchServerSyncStatus;

            _watchServerSyncStatus?.Invoke(OnServerStatusChanged);

            var iconTexture = contentsManager.GetTexture(SparkServiceConfig.CornerIconFilename);
            var hoverTexture = contentsManager.GetTexture(SparkServiceConfig.CornerIconHoverFilename, iconTexture);

            _icon = new AsyncTexture2D(iconTexture);

            if (ReferenceEquals(iconTexture, hoverTexture))
            {
                _hoverIcon = _icon;
            }
            else
            {
                _hoverIcon = new AsyncTexture2D(hoverTexture);
                _disposeHoverIcon = true;
            }

            _settings.ShowCornerIcon.SettingChanged += OnShowCornerIconChanged;
            _settings.CurrentStatus.SettingChanged += OnCurrentStatusChanged;
        }

        public void Refresh()
        {
            if (_isDisposed)
                return;

            if (_settings.ShowCornerIcon.Value)
                EnsureCreated();
            else
                Clear();
        }

        private void EnsureCreated()
        {
            if (_cornerIcon != null)
                return;

            _menu = BuildMenu();

            _cornerIcon = new CornerIcon(_icon, _hoverIcon, "SPARK")
            {
                Priority = 0
            };

            _cornerIcon.Click += OnCornerIconClick;
        }

        private ContextMenuStrip BuildMenu()
        {
            var menu = new ContextMenuStrip
            {
                Width = CalculateMenuWidth()
            };

            AddStatusSubmenu(menu);
            _myProfileItem = AddMenuItem(menu, "My Profile", _openMyProfile);
            _profileEditorItem = AddMenuItem(menu, "Profile Editor", _openProfileManager);

            AddSectionHeader(menu, "Players");

            _onlineListItem = AddMenuItem(menu, "Online List", _openOnlineList);
            _nearbyPlayersItem = AddMenuItem(menu, "Nearby Players", _openNearby);
            _savedProfilesItem = AddMenuItem(menu, "Saved Profiles", _openSavedProfiles);

            AddSectionHeader(menu, "SPARK Status");
            AddSparkStatusItems(menu);

            AddSectionHeader(menu, "Config");

            AddMenuItem(menu, "Options", _openSettings);
            AddMenuItem(menu, "Manage Blocks", _openBlocklist);
            AddPrivacySubmenu(menu);

            RefreshMenuState();

            return menu;
        }

        private int CalculateMenuWidth()
        {
            var width = MinimumMenuWidth;

            width = Math.Max(width, MenuItemWidth("My Profile"));
            width = Math.Max(width, MenuItemWidth("Profile Editor"));
            width = Math.Max(width, MenuItemWidth("Nearby Players"));
            width = Math.Max(width, MenuItemWidth("Saved Profiles"));
            width = Math.Max(width, MenuItemWidth("Manage Blocks"));

            foreach (var label in ProfileLabels.RpStatusOptions)
                width = Math.Max(width, MenuItemWidth($"Status: {label}"));

            width = Math.Max(width, MenuItemWidth($"Status: {CurrentStatusLabel()}"));
            width = Math.Max(width, MenuItemWidth("SPARK needs attention"));
            width = Math.Max(width, MenuItemWidth("Server: SPARK webserver unavailable"));

            return width;
        }

        private static int MenuItemWidth(string text)
        {
            return (int)Math.Ceiling(GameService.Content.DefaultFont14.MeasureString(text).Width) + MenuItemTextPadding;
        }

        private static ContextMenuStripItem AddMenuItem(ContextMenuStrip menu, string text, Action action)
        {
            var item = menu.AddMenuItem(text);

            item.Click += (s, e) =>
            {
                if (item.Enabled)
                    action?.Invoke();
            };

            return item;
        }

        private static void AddSectionHeader(ContextMenuStrip menu, string text)
        {
            menu.AddMenuItem(new ContextMenuHeader(text));
        }

        private void AddSparkStatusItems(ContextMenuStrip menu)
        {
            _readinessMenuItem = new ContextMenuColours(
                "SPARK ready",
                new Color(140, 220, 140));

            _serverStatusMenuItem = new ContextMenuColours(
                "Server: Disconnected",
                SparkViewUI.SecondaryTextColor);

            _readinessMenuItem.Click += (s, e) => _openSettings?.Invoke();
            _serverStatusMenuItem.Click += (s, e) => _openSettings?.Invoke();

            menu.AddMenuItem(_readinessMenuItem);
            menu.AddMenuItem(_serverStatusMenuItem);

            RefreshSparkStatusItems();
        }

        private void OnServerStatusChanged(ServerSyncStatus status)
        {
            SparkUiThread.Queue(() =>
            {
                if (_isDisposed || _menu == null)
                    return;

                RefreshSparkStatusItems();
            });
        }

        private void RefreshSparkStatusItems()
        {
            if (_readinessMenuItem == null || _serverStatusMenuItem == null)
                return;

            var display = SparkStatusDisplay.Create(
                _getImportantNotice,
                _getServerSyncStatus);

            _readinessMenuItem.Text = display.ReadinessText;
            _readinessMenuItem.TextColor = display.ReadinessColor;
            _readinessMenuItem.BasicTooltipText =
                display.ReadinessTooltip;

            _serverStatusMenuItem.Text = display.ServerText;
            _serverStatusMenuItem.TextColor = display.ServerColor;
            _serverStatusMenuItem.BasicTooltipText =
                display.ServerTooltip;
        }

        private void AddPrivacySubmenu(ContextMenuStrip menu)
        {
            var privacyItem = menu.AddMenuItem("Privacy");
            privacyItem.Submenu = new ContextMenuStrip(GetPrivacyMenuItems);
        }

        private IEnumerable<ContextMenuStripItem> GetPrivacyMenuItems()
        {
            yield return CreateCheckMenuItem(
                "Share My Profile",
                _settings.BroadcastProfile.Value,
                SetShareProfile);

            yield return CreateCheckMenuItem(
                "Hide My Location",
                _settings.HideLocation.Value,
                SetHideLocation);

            yield return CreateCheckMenuItem(
                "Show Me Nearby",
                _settings.ShowNearbyPresence.Value,
                SetNearbyPresence);
        }

        private static ContextMenuStripItem CreateCheckMenuItem(
            string text,
            bool isChecked,
            Action<bool> onChanged)
        {
            var item = new ContextMenuStripItem
            {
                Text = text,
                CanCheck = true,
                Checked = isChecked
            };

            item.CheckedChanged += (s, e) => onChanged?.Invoke(e.Checked);
            return item;
        }

        private void SetShareProfile(bool enabled)
        {
            if (_settings.BroadcastProfile.Value == enabled)
                return;

            _settings.BroadcastProfile.Value = enabled;
            _requestServerSync?.Invoke();
        }

        private void SetHideLocation(bool enabled)
        {
            if (_settings.HideLocation.Value == enabled)
                return;

            _settings.HideLocation.Value = enabled;
            _requestServerSync?.Invoke();
        }

        private void SetNearbyPresence(bool enabled)
        {
            if (_settings.ShowNearbyPresence.Value == enabled)
                return;

            if (_setNearbySharing != null)
                _setNearbySharing(enabled);
            else
                _settings.ShowNearbyPresence.Value = enabled;
        }

        private void AddStatusSubmenu(ContextMenuStrip menu)
        {
            var currentStatus = CurrentStatus();

            _statusMenuItem = menu.AddMenuItem(new ContextMenuColours(
                $"Status: {ProfileLabels.StatusLabel(currentStatus)}",
                ProfileStatusColors.Get(currentStatus)));

            _statusMenuItem.Submenu = new ContextMenuStrip(GetStatusMenuItems);
        }

        private static ContextMenuColours CreateColoredCheckMenuItem(
            string text,
            RPStatus status,
            bool isChecked,
            Action<bool> onChanged)
        {
            var item = new ContextMenuColours(text, ProfileStatusColors.Get(status))
            {
                CanCheck = true,
                Checked = isChecked
            };

            item.CheckedChanged += (s, e) => onChanged?.Invoke(e.Checked);
            return item;
        }

        private IEnumerable<ContextMenuStripItem> GetStatusMenuItems()
        {
            _statusMenuItems.Clear();

            var currentStatus = CurrentStatus();

            foreach (var label in ProfileLabels.RpStatusOptions)
            {
                var optionStatus = ProfileLabels.ParseStatus(label);

                var item = CreateColoredCheckMenuItem(
                    label,
                    optionStatus,
                    optionStatus == currentStatus,
                    enabled =>
                    {
                        if (_isSyncingStatusMenu || !enabled)
                            return;

                        SetStatus(optionStatus);
                        SyncStatusMenuFromSettings();
                    });

                _statusMenuItems[optionStatus] = item;

                yield return item;
            }
        }

        private RPStatus CurrentStatus()
        {
            var status = _settings.CurrentStatus.Value;
            return status == RPStatus.Offline ? RPStatus.Online : status;
        }

        private string CurrentStatusLabel()
        {
            return ProfileLabels.StatusLabel(CurrentStatus());
        }

        private void SetStatus(RPStatus status)
        {
            if (status == RPStatus.Offline)
                status = RPStatus.Online;

            if (_settings.CurrentStatus.Value == status)
                return;

            _settings.CurrentStatus.Value = status;
            _requestServerSync?.Invoke();
        }

        private void OnCurrentStatusChanged(object sender, ValueChangedEventArgs<RPStatus> e)
        {
            SparkUiThread.Queue(() =>
            {
                if (!_isDisposed)
                    SyncStatusMenuFromSettings();
            });
        }

        private void SyncStatusMenuFromSettings()
        {
            var currentStatus = CurrentStatus();

            if (_statusMenuItem != null)
            {
                _statusMenuItem.Text = $"Status: {ProfileLabels.StatusLabel(currentStatus)}";

                if (_statusMenuItem is ContextMenuColours coloredStatusMenuItem)
                    coloredStatusMenuItem.TextColor = ProfileStatusColors.Get(currentStatus);
            }

            _isSyncingStatusMenu = true;

            try
            {
                foreach (var pair in _statusMenuItems)
                {
                    var shouldBeChecked = pair.Key == currentStatus;

                    if (pair.Value.Checked != shouldBeChecked)
                        pair.Value.Checked = shouldBeChecked;
                }
            }
            finally
            {
                _isSyncingStatusMenu = false;
            }
        }

        private void RefreshMenuState()
        {
            var enabled = !ShouldDisableGameplayMenuItems();
            var tooltip = enabled ? string.Empty : DisabledMenuTooltip();

            SetMenuItemState(_myProfileItem, enabled, tooltip);
            SetMenuItemState(_profileEditorItem, enabled, tooltip);
            SetMenuItemState(_onlineListItem, enabled, tooltip);
            SetMenuItemState(_nearbyPlayersItem, enabled, tooltip);
            SetMenuItemState(_savedProfilesItem, enabled, tooltip);
        }

        internal void RefreshForGameState()
        {
            if (_isDisposed)
                return;

            SparkUiThread.Queue(() =>
            {
                if (_isDisposed || _menu == null)
                    return;

                RefreshMenuState();
                RefreshSparkStatusItems();
            });
        }

        private static void SetMenuItemState(ContextMenuStripItem item, bool enabled, string tooltip)
        {
            if (item == null)
                return;

            item.Enabled = enabled;
            item.BasicTooltipText = enabled ? string.Empty : tooltip;
        }

        private bool ShouldDisableGameplayMenuItems()
        {
            return !GameService.GameIntegration.Gw2Instance.IsInGame
                || SparkWindows.IsLoadingScreen()
                || ShouldHideForGameUi();
        }

        private bool ShouldHideForGameUi()
        {
            return (_settings?.AutoHideGameUi.Value ?? true)
                && GameService.Gw2Mumble.UI.IsMapOpen;
        }

        private string DisabledMenuTooltip()
        {
            if (ShouldHideForGameUi())
                return "SPARK profile tools are unavailable while the map or game UI is open.";

            return "SPARK profile tools are unavailable during loading screens or character select.";
        }

        private void OnCornerIconClick(object sender, MouseEventArgs e)
        {
            RefreshMenuState();
            RefreshSparkStatusItems();
            _menu?.Show(_cornerIcon);
        }

        private void OnShowCornerIconChanged(object sender, ValueChangedEventArgs<bool> e)
        {
            SparkUiThread.Queue(Refresh);
        }

        private void Clear()
        {
            if (_cornerIcon != null)
            {
                _cornerIcon.Click -= OnCornerIconClick;
                _cornerIcon.Dispose();
                _cornerIcon = null;
            }

            _menu?.Dispose();
            _menu = null;
            _myProfileItem = null;
            _profileEditorItem = null;
            _onlineListItem = null;
            _nearbyPlayersItem = null;
            _savedProfilesItem = null;
            _statusMenuItem = null;
            _readinessMenuItem = null;
            _serverStatusMenuItem = null;
            _statusMenuItems.Clear();
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            _settings.ShowCornerIcon.SettingChanged -= OnShowCornerIconChanged;
            _settings.CurrentStatus.SettingChanged -= OnCurrentStatusChanged;
            _unwatchServerSyncStatus?.Invoke(OnServerStatusChanged);

            Clear();

            _icon?.Dispose();

            if (_disposeHoverIcon)
                _hoverIcon?.Dispose();
        }
    }
}