using Blish_HUD;
using Blish_HUD.Common.UI.Views;
using Blish_HUD.Controls;
using Blish_HUD.Graphics.UI;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace rp.spark.UI.Views
{
    internal class ProfileTooltipView : View, ITooltipView
    {
        private const int MinWidth = 270;
        private const int MaxWidth = 390;
        private const int Padding = 7;
        private const int TitleHeight = 23;
        private const int DescriptionGap = 2;
        private const int DescriptionLineHeight = 22;
        private const int DescriptionMaxLines = 12;
        private const int WrapMeasurePadding = 14;

        private static readonly Color TooltipBackground = new Color(7, 10, 12, 190);
        private static readonly Color TitleColor = new Color(255, 194, 55);
        private static readonly Color DescriptionColor = new Color(238, 238, 238);

        private readonly string _title;
        private readonly string _description;

        public ProfileTooltipView(string title, string description, string fallbackTitle = "Profile")
        {
            _title = string.IsNullOrWhiteSpace(title)
                ? fallbackTitle?.Trim() ?? string.Empty
                : title.Trim();

            _description = description?.Trim() ?? string.Empty;
        }

        protected override void Build(Container buildPanel)
        {
            var hasTitle = !string.IsNullOrWhiteSpace(_title);
            var hasDescription = !string.IsNullOrWhiteSpace(_description);
            var tooltipWidth = GetTooltipWidth(_title);
            var contentWidth = tooltipWidth - (Padding * 2);
            var descriptionLines = hasDescription
                ? TooltipTextLayout.WrapLines(
                    _description,
                    contentWidth - WrapMeasurePadding,
                    GameService.Content.DefaultFont16)
                    .Take(DescriptionMaxLines)
                    .ToList()
                : new List<string>();

            var descriptionHeight = descriptionLines.Count * DescriptionLineHeight;

            var height = Padding;

            if (hasTitle)
                height += TitleHeight;

            if (hasDescription)
            {
                if (hasTitle)
                    height += DescriptionGap;

                height += descriptionHeight;
            }

            height += Padding;

            buildPanel.Size = new Point(tooltipWidth, height);
            buildPanel.BackgroundColor = TooltipBackground;

            if (buildPanel is Panel tooltipPanel)
                tooltipPanel.ShowBorder = true;

            var y = Padding;

            if (hasTitle)
            {
                new Label
                {
                    Text = _title,
                    Font = GameService.Content.DefaultFont16,
                    TextColor = TitleColor,
                    StrokeText = false,
                    WrapText = false,
                    ShowShadow = true,
                    Location = new Point(Padding, y),
                    Size = new Point(contentWidth, TitleHeight),
                    Parent = buildPanel,
                    ZIndex = 1
                };

                y += TitleHeight;
            }

            if (hasDescription)
            {
                if (hasTitle)
                    y += DescriptionGap;

                foreach (var line in descriptionLines)
                {
                    new Label
                    {
                        Text = line,
                        Font = GameService.Content.DefaultFont14,
                        TextColor = DescriptionColor,
                        WrapText = false,
                        ShowShadow = true,
                        Location = new Point(Padding, y),
                        Size = new Point(contentWidth, DescriptionLineHeight),
                        Parent = buildPanel,
                        ZIndex = 1
                    };

                    y += DescriptionLineHeight;
                }
            }
        }

        private static int GetTooltipWidth(string title)
        {
            var screenWidth = GameService.Graphics.SpriteScreen.Size.X;
            var maxWidth = Math.Max(MinWidth, Math.Min(MaxWidth, screenWidth - 16));
            var titleWidth = (int)Math.Ceiling(GameService.Content.DefaultFont16.MeasureString(title ?? string.Empty).Width)
                           + (Padding * 2);

            return Math.Max(MinWidth, Math.Min(maxWidth, titleWidth));
        }
    }
}
