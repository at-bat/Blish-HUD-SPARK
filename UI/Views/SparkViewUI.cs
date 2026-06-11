using Blish_HUD.Controls;
using Microsoft.Xna.Framework;
using MonoGame.Extended.BitmapFonts;
using System.Collections.Generic;

namespace rp.spark.UI.Views
{
    internal static class SparkViewUI
    {
        public static readonly Color SecondaryTextColor = new Color(220, 220, 220);
        public static readonly Color WarningTextColor = new Color(255, 170, 40);
        public const string MissingApiKeyWarning = "No API key! Click 'Manage API Keys' on Blish HUD and add a key to use SPARK!";

        public static Label AddLabel(
            Container parent,
            string text,
            int x,
            int y,
            int width,
            int height = 25,
            BitmapFont font = null,
            Color? textColor = null,
            bool strokeText = false)
        {
            var label = new Label
            {
                Text = text ?? string.Empty,
                TextColor = textColor ?? Color.White,
                StrokeText = strokeText,
                Location = new Point(x, y),
                Size = new Point(width, height),
                Parent = parent
            };

            if (font != null)
                label.Font = font;

            return label;
        }

        public static StandardButton AddButton(
            Container parent,
            string text,
            int x,
            int y,
            int width,
            int height = 35,
            bool enabled = true)
        {
            return new StandardButton
            {
                Text = text ?? string.Empty,
                Location = new Point(x, y),
                Size = new Point(width, height),
                Parent = parent,
                Enabled = enabled
            };
        }

        public static Checkbox AddCheckbox(
            Container parent,
            string text,
            bool isChecked,
            int x,
            int y,
            int width,
            int height = 30)
        {
            return new Checkbox
            {
                Text = text ?? string.Empty,
                Checked = isChecked,
                Location = new Point(x, y),
                Size = new Point(width, height),
                Parent = parent
            };
        }

        public static Dropdown AddDropdown(
            Container parent,
            IEnumerable<string> options,
            string selectedItem,
            int x,
            int y,
            int width,
            int height = 35)
        {
            var dropdown = new Dropdown
            {
                Location = new Point(x, y),
                Size = new Point(width, height),
                Parent = parent
            };

            foreach (var option in options ?? new string[0])
                dropdown.Items.Add(option);

            dropdown.SelectedItem = selectedItem;

            return dropdown;
        }

        public static TextBox AddTextBox(
            Container parent,
            string text,
            string placeholderText,
            int x,
            int y,
            int width,
            int height = 35,
            int? maxLength = null)
        {
            var textBox = new TextBox
            {
                Text = text ?? string.Empty,
                PlaceholderText = placeholderText ?? string.Empty,
                Location = new Point(x, y),
                Size = new Point(width, height),
                Parent = parent
            };

            if (maxLength.HasValue)
                textBox.MaxLength = maxLength.Value;

            return textBox;
        }
    }
}
