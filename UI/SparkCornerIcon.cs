using Blish_HUD;
using Blish_HUD.Content;
using Blish_HUD.Controls;
using Blish_HUD.Input;
using Blish_HUD.Modules.Managers;
using rp.spark.Services;
using System;

namespace rp.spark.UI
{
    internal sealed class SparkCornerIcon : IDisposable
    {
        private const int MenuWidth = 170;

        private readonly SparkSettings _settings;
        private readonly AsyncTexture2D _icon;
        private readonly AsyncTexture2D _hoverIcon;
        private readonly bool _disposeHoverIcon;
        private readonly Action _openProfileManager;
        private readonly Action _openOnlineList;
        private readonly Action _openNearby;
        private readonly Action _openSavedProfiles;
        private readonly Action _openBlocklist;

        private CornerIcon _cornerIcon;
        private ContextMenuStrip _menu;
        private bool _isDisposed;

        public SparkCornerIcon(
            SparkSettings settings,
            ContentsManager contentsManager,
            Action openProfileManager,
            Action openOnlineList,
            Action openNearby,
            Action openSavedProfiles,
            Action openBlocklist)
        {
            _settings = settings;
            _openProfileManager = openProfileManager;
            _openOnlineList = openOnlineList;
            _openNearby = openNearby;
            _openSavedProfiles = openSavedProfiles;
            _openBlocklist = openBlocklist;

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
                Width = MenuWidth
            };

            AddMenuItem(menu, "Profile Editor", _openProfileManager);
            AddMenuItem(menu, "Online List", _openOnlineList);
            AddMenuItem(menu, "Nearby Players", _openNearby);
            AddMenuItem(menu, "Saved Profiles", _openSavedProfiles);
            AddMenuItem(menu, "Manage Blocks", _openBlocklist);

            return menu;
        }

        private static void AddMenuItem(ContextMenuStrip menu, string text, Action action)
        {
            var item = menu.AddMenuItem(text);
            item.Click += (s, e) => action?.Invoke();
        }

        private void OnCornerIconClick(object sender, MouseEventArgs e)
        {
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
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            _settings.ShowCornerIcon.SettingChanged -= OnShowCornerIconChanged;

            Clear();

            _icon?.Dispose();

            if (_disposeHoverIcon)
                _hoverIcon?.Dispose();
        }
    }
}