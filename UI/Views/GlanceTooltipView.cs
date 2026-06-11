using Blish_HUD;
using Blish_HUD.Common.UI.Views;
using Blish_HUD.Controls;
using Blish_HUD.Graphics.UI;
using Microsoft.Xna.Framework;
using MonoGame.Extended.BitmapFonts;
using System;
using System.Linq;

namespace rp.spark.UI.Views
{
    internal class GlanceTooltipView : View, ITooltipView
    {
        private const int MinWidth = 220;
        private const int MaxWidth = 520;
        private const int Padding = 2;
        private const int TitleHeight = 28;
        private const int TitleGap = 2;
        private const int DescriptionLineHeight = 22;
        private const int DescriptionMaxLines = 6;

        private static readonly Color TooltipBackground = new Color(8, 12, 14, 245);
        private static readonly Color TitleColor = new Color(235, 186, 108);
        private static readonly Color DescriptionColor = new Color(235, 235, 235);

        private readonly string _title;
        private readonly string _description;

        public GlanceTooltipView(string title, string description)
        {
            _title = string.IsNullOrWhiteSpace(title) ? "At a Glance" : title.Trim();
            _description = string.IsNullOrWhiteSpace(description) ? "No description set." : description.Trim();
        }

        protected override void Build(Container buildPanel)
        {
            var tooltipWidth = GetTooltipWidth(_title);
            var contentWidth = tooltipWidth - (Padding * 2);
            var descriptionHeight = GetWrappedTextHeight(
                _description,
                contentWidth,
                GameService.Content.DefaultFont16,
                DescriptionLineHeight,
                DescriptionMaxLines);

            buildPanel.Size = new Point(
                tooltipWidth,
                (Padding * 2) + TitleHeight + TitleGap + descriptionHeight);
            buildPanel.BackgroundColor = TooltipBackground;

            new Label
            {
                Text = _title,
                Font = GameService.Content.DefaultFont16,
                TextColor = TitleColor,
                StrokeText = true,
                WrapText = false,
                ShowShadow = true,
                Location = new Point(Padding, Padding),
                Size = new Point(contentWidth, TitleHeight),
                Parent = buildPanel
            };

            new Label
            {
                Text = _description,
                Font = GameService.Content.DefaultFont14,
                TextColor = DescriptionColor,
                WrapText = true,
                Location = new Point(Padding, Padding + TitleHeight + TitleGap),
                Size = new Point(contentWidth, descriptionHeight),
                Parent = buildPanel
            };
        }

        private static int GetTooltipWidth(string title)
        {
            var screenWidth = GameService.Graphics.SpriteScreen.Size.X;
            var maxWidth = Math.Min(MaxWidth, screenWidth - 16);
            var titleWidth = (int)Math.Ceiling(GameService.Content.DefaultFont18.MeasureString(title).Width);

            return Math.Max(MinWidth, Math.Min(maxWidth, titleWidth + (Padding * 2)));
        }

        private static int GetWrappedTextHeight(
            string text,
            float maxWidth,
            BitmapFont font,
            int lineHeight,
            int maxLines)
        {
            var normalized = (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
            var lineCount = normalized.Split('\n').Sum(line => CountWrappedLines(line, maxWidth, font));

            return Math.Max(1, Math.Min(maxLines, lineCount)) * lineHeight;
        }

        private static int CountWrappedLines(string line, float maxWidth, BitmapFont font)
        {
            if (string.IsNullOrWhiteSpace(line))
                return 1;

            var lineCount = 1;
            var currentLine = string.Empty;

            foreach (var word in line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var candidate = string.IsNullOrEmpty(currentLine) ? word : $"{currentLine} {word}";

                if (font.MeasureString(candidate).Width <= maxWidth || string.IsNullOrEmpty(currentLine))
                {
                    currentLine = candidate;
                    continue;
                }

                lineCount++;
                currentLine = word;
            }

            return lineCount;
        }
    }
}
