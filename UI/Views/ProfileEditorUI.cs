using Blish_HUD;
using Blish_HUD.Controls;
using Microsoft.Xna.Framework;
using rp.spark.Services;

namespace rp.spark.UI.Views
{
    internal static class ProfileEditorUI
    {
        public const int SaveY = 515;
        public const int StatusY = 520;
        public const int HeaderY = 560;

        public static Label AddSaveFooter(
            Container parent,
            ProfileEditorSession session,
            int saveY = SaveY,
            int statusY = StatusY,
            int headerY = HeaderY,
            int statusX = 170,
            int statusWidth = 560)
        {
            var saveButton = new StandardButton
            {
                Text = "Save Profile",
                Location = new Point(0, saveY),
                Size = new Point(150, 35),
                Parent = parent
            };

            SparkUiActions.BindClick(
                saveButton,
                () => session.SaveAsync(),
                session.SetStatus,
                "Couldn't save profile.");

            var statusLabel = AddStatusLabel(
                parent,
                session.StatusText,
                new Point(statusX, statusY),
                new Point(statusWidth, 30));

            AddHeaderLabel(parent, session, headerY);

            return statusLabel;
        }

        public static Label AddStatusLabel(Container parent, string text, Point location, Point size)
        {
            return new Label
            {
                Text = text ?? string.Empty,
                Location = location,
                Size = size,
                Parent = parent
            };
        }

        public static Label AddHeaderLabel(Container parent, ProfileEditorSession session, int y = HeaderY)
        {
            return new Label
            {
                Text = session.GetHeaderText(),
                Font = GameService.Content.DefaultFont12,
                TextColor = new Color(220, 220, 220),
                Location = new Point(0, y),
                Size = new Point(760, 25),
                Parent = parent
            };
        }

        public static void ShowUnavailableMessage(Container parent)
        {
            new Label
            {
                Text = "SPARK can't connect to GW2 to retrieve your info.",
                Font = GameService.Content.DefaultFont18,
                TextColor = new Color(255, 233, 180),
                StrokeText = true,
                Location = new Point(0, 0),
                Size = new Point(760, 30),
                Parent = parent
            };

            new Label
            {
                Text = "Please log in to a character to use SPARK.",
                Font = GameService.Content.DefaultFont16,
                TextColor = Color.White,
                WrapText = true,
                Location = new Point(0, 40),
                Size = new Point(760, 80),
                Parent = parent
            };
        }

        public static void AddLabel(Container parent, string text, int y, int x = 0, int width = 250)
        {
            new Label
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Point(width, 25),
                Parent = parent
            };
        }
    }
}
