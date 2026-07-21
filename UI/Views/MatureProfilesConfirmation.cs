using Blish_HUD;
using Blish_HUD.Controls;
using Microsoft.Xna.Framework;
using rp.spark.Services;
using System;

namespace rp.spark.UI.Views
{
    internal sealed class MatureProfilesConfirmation : IDisposable
    {
        private readonly SparkSettings _settings;
        private readonly Action<bool> _maturePreferenceChanged;

        private Panel _confirmationPanel;

        public MatureProfilesConfirmation(SparkSettings settings, Action<bool> maturePreferenceChanged)
        {
            _settings = settings;
            _maturePreferenceChanged = maturePreferenceChanged;
        }

        public string ButtonText => _settings.ShowMatureProfiles.Value
            ? "Mature Profiles Visible"
            : "Mature Profiles Hidden";

        public void Toggle(Container popupParent)
        {
            if (_settings.ShowMatureProfiles.Value)
            {
                SetEnabled(false);
                return;
            }

            OpenConfirmation(popupParent);
        }

        public void CloseConfirmation()
        {
            _confirmationPanel?.Dispose();
            _confirmationPanel = null;
        }

        private void SetEnabled(bool enabled)
        {
            if (_settings.ShowMatureProfiles.Value == enabled)
                return;

            _settings.ShowMatureProfiles.Value = enabled;
            _maturePreferenceChanged?.Invoke(enabled);
        }

        private void OpenConfirmation(Container popupParent)
        {
            CloseConfirmation();

            var parent = popupParent ?? GameService.Graphics.SpriteScreen;
            const int popupWidth = 500;
            const int popupHeight = 190;

            _confirmationPanel = new Panel
            {
                ShowBorder = true,
                Title = "Enable Mature Profiles?",
                Size = new Point(popupWidth, popupHeight),
                Location = GetCenteredPopupLocation(parent, popupWidth, popupHeight),
                Parent = parent,
                BackgroundColor = new Color(38, 35, 32),
                ClipsBounds = false,
                ZIndex = 100
            };

            var closeButton = new StandardButton
            {
                Text = "X",
                Location = new Point(popupWidth - 32, -28),
                Size = new Point(24, 24),
                Parent = _confirmationPanel,
                ClipsBounds = false,
                ZIndex = 10011
            };

            closeButton.Click += (s, e) => CloseConfirmation();

            new Label
            {
                Text =
                    "Enabling this will allow you to view profiles marked as mature/18+. "
                    + "These profiles may contain explicit details not suitable for minors."
                    + Environment.NewLine
                    + Environment.NewLine
                    + "Are you sure you want to continue?",
                Font = GameService.Content.DefaultFont14,
                TextColor = Color.White,
                WrapText = true,
                Location = new Point(16, 6),
                Size = new Point(468, 92),
                Parent = _confirmationPanel
            };

            var yesButton = new StandardButton
            {
                Text = "Show Mature Profiles",
                Location = new Point(200, 108),
                Size = new Point(165, 32),
                Parent = _confirmationPanel
            };

            yesButton.Click += (s, e) =>
            {
                SetEnabled(true);
                CloseConfirmation();
            };

            var noButton = new StandardButton
            {
                Text = "No",
                Location = new Point(379, 108),
                Size = new Point(105, 32),
                Parent = _confirmationPanel
            };

            noButton.Click += (s, e) => CloseConfirmation();
        }

        private static Point GetCenteredPopupLocation(Container parent, int width, int height)
        {
            var parentSize = parent?.ContentRegion.Size ?? GameService.Graphics.SpriteScreen.Size;
            const int padding = 8;

            return new Point(
                Math.Max(padding, (parentSize.X - width) / 2),
                Math.Max(padding, (parentSize.Y - height) / 2));
        }

        public void Dispose()
        {
            CloseConfirmation();
        }
    }
}