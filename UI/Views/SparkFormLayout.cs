using Blish_HUD;
using Blish_HUD.Controls;
using Microsoft.Xna.Framework;
using MonoGame.Extended.BitmapFonts;
using System.Collections.Generic;

namespace rp.spark.UI.Views
{
    internal static class SparkFormLayout
    {
        public static FlowPanel AddVerticalStack(
            Container parent,
            int x,
            int y,
            int width,
            int height,
            int gap = 10,
            bool canScroll = false)
        {
            return new FlowPanel
            {
                Parent = parent,
                Location = new Point(x, y),
                Size = new Point(width, height),
                CanScroll = canScroll,
                FlowDirection = ControlFlowDirection.SingleTopToBottom,
                ControlPadding = new Vector2(0, gap)
            };
        }

        public static FlowPanel AddAutoStack(Container parent, int width, int gap = 5)
        {
            return new FlowPanel
            {
                Parent = parent,
                Width = width,
                HeightSizingMode = SizingMode.AutoSize,
                FlowDirection = ControlFlowDirection.SingleTopToBottom,
                ControlPadding = new Vector2(0, gap)
            };
        }

        public static FlowPanel AddRow(Container parent, int width, int height, int gap = 10)
        {
            return new FlowPanel
            {
                Parent = parent,
                Width = width,
                Height = height,
                FlowDirection = ControlFlowDirection.SingleLeftToRight,
                ControlPadding = new Vector2(gap, 0)
            };
        }

        public static Label AddLabel(
            Container parent,
            string text,
            int width,
            int height = 25,
            BitmapFont font = null,
            Color? textColor = null,
            bool strokeText = false)
        {
            var label = new Label
            {
                Text = text ?? string.Empty,
                Width = width,
                Height = height,
                Font = font ?? GameService.Content.DefaultFont14,
                TextColor = textColor ?? Color.White,
                StrokeText = strokeText,
                Parent = parent
            };

            return label;
        }

        public static TextBox AddTextBox(
            Container parent,
            string text,
            string placeholderText,
            int width,
            int height = 35,
            int? maxLength = null)
        {
            var textBox = new TextBox
            {
                Text = text ?? string.Empty,
                PlaceholderText = placeholderText ?? string.Empty,
                Width = width,
                Height = height,
                Parent = parent
            };

            if (maxLength.HasValue)
                textBox.MaxLength = maxLength.Value;

            return textBox;
        }

        public static MultilineTextBox AddMultilineTextBox(
            Container parent,
            string text,
            string placeholderText,
            int width,
            int height,
            int? maxLength = null)
        {
            var textBox = new MultilineTextBox
            {
                Text = text ?? string.Empty,
                PlaceholderText = placeholderText ?? string.Empty,
                Width = width,
                Height = height,
                Parent = parent
            };

            if (maxLength.HasValue)
                textBox.MaxLength = maxLength.Value;

            return textBox;
        }

        public static MultilineTextBox AddLabeledMultilineTextBox(
            FlowPanel parent,
            string labelText,
            string text,
            string placeholderText,
            int width,
            int height,
            int? maxLength = null)
        {
            var group = AddAutoStack(parent, width, 0);

            AddLabel(group, labelText, width);
            return AddMultilineTextBox(group, text, placeholderText, width, height, maxLength);
        }

        public static TextBox AddLabeledTextBox(
            FlowPanel parent,
            string labelText,
            string text,
            string placeholderText,
            int width,
            int height = 35,
            int? maxLength = null)
        {
            var group = AddAutoStack(parent, width, 0);

            AddLabel(group, labelText, width);
            return AddTextBox(group, text, placeholderText, width, height, maxLength);
        }

        public static Dropdown AddDropdown(
            Container parent,
            IEnumerable<string> options,
            string selectedItem,
            int width,
            int height = 35)
        {
            var dropdown = new Dropdown
            {
                Width = width,
                Height = height,
                Parent = parent
            };

            foreach (var option in options ?? new string[0])
                dropdown.Items.Add(option);

            dropdown.SelectedItem = selectedItem;
            return dropdown;
        }

        public static Checkbox AddCheckbox(
            Container parent,
            string text,
            bool isChecked,
            int width,
            int height = 30)
        {
            return new Checkbox
            {
                Text = text ?? string.Empty,
                Checked = isChecked,
                Width = width,
                Height = height,
                Parent = parent
            };
        }

        public static StandardButton AddButton(
            Container parent,
            string text,
            int width,
            int height = 35,
            bool enabled = true)
        {
            return new StandardButton
            {
                Text = text ?? string.Empty,
                Width = width,
                Height = height,
                Enabled = enabled,
                Parent = parent
            };
        }

        public static Panel AddSpacer(Container parent, int width, int height)
        {
            return new Panel
            {
                Width = width,
                Height = height,
                Parent = parent
            };
        }
    }
}
