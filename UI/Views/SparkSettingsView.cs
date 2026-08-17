using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Graphics.UI;
using rp.spark.Services;
using System;
using System.Diagnostics;

namespace rp.spark.UI.Views
{
    public class SparkSettingsView : View
    {
        private const string SparkUrl = "https://getspark.fyi";
        private const int ContentWidth = 660;
        private const int ControlHeight = 30;
        private const int RowGap = 8;
        private const int LeftPadding = 8;

        private readonly Action<Control> _showMenu;

        public SparkSettingsView(Action<Control> showMenu)
        {
            _showMenu = showMenu;
        }

        protected override void Build(Container buildPanel)
        {
            BuildSettings(buildPanel);
        }

        private void BuildSettings(Container buildPanel)
        {
            var settingsStack = SparkFormLayout.AddAutoStack(buildPanel, ContentWidth, 6);
            settingsStack.Left = LeftPadding;

            var menuHint = SparkFormLayout.AddLabel(
                settingsStack,
                "Use the SPARK Menu below to access profiles, player lists, RP tools, privacy controls, and settings.",
                ContentWidth,
                30,
                GameService.Content.DefaultFont14,
                SparkViewUI.SecondaryTextColor);

            menuHint.WrapText = true;

            var actions = SparkFormLayout.AddRow(settingsStack, ContentWidth, ControlHeight, RowGap);
            var menuButton = SparkFormLayout.AddButton(actions, "Open SPARK Menu", 150, ControlHeight);

            menuButton.BasicTooltipText = "Open profiles, player lists, RP tools, privacy controls, and settings.";
            menuButton.Click += (s, e) => _showMenu?.Invoke(menuButton);

            var documentationButton = SparkFormLayout.AddButton(actions, "Documentation", 130, ControlHeight);

            documentationButton.BasicTooltipText = $"Opens a browser page for {SparkUrl}.";
            documentationButton.Click += (s, e) => OpenDocumentation();
        }

        private static void OpenDocumentation()
        {
            try
            {
                Process.Start(new ProcessStartInfo(SparkUrl)
                {
                    UseShellExecute = true
                });
            }
            catch
            {
                ScreenNotification.ShowNotification(
                    "Couldn't open the SPARK documentation.",
                    ScreenNotification.NotificationType.Error);
            }
        }
    }
}