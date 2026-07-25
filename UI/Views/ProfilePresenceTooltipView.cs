using Blish_HUD;
using Blish_HUD.Common.UI.Views;
using Blish_HUD.Controls;
using Blish_HUD.Graphics.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using rp.spark.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using MonoGame.Extended.BitmapFonts;

namespace rp.spark.UI.Views
{
    internal sealed class ProfilePresenceTooltipView : View, ITooltipView
    {
        private const int MinTooltipWidth = 300;
        private const int PreferredBodyWidth = 430;
        private const int MaxTooltipWidth = 570;
        private const int ScreenEdgePadding = 32;
        private const int WidthMeasurePadding = 20;

        private const int Padding = 8;
        private const int WrapSafetyPadding = 8;
        private const int TitleHeight = 25;
        private const int LineHeight = 21;
        private const int SectionHeaderHeight = 22;
        private const int SectionHeaderSideInset = 8;
        private const int SectionGap = 5;

        private static readonly Color TooltipBackground = new Color(7, 10, 12, 210);
        private static readonly Color TitleColor = new Color(255, 194, 55);
        private static readonly Color BodyColor = new Color(238, 238, 238);
        private static readonly Color SectionColor = new Color(255, 233, 180);
        private static readonly Color SectionLineColor = new Color(255, 233, 180) * 0.35f;
        private static readonly Color MoreInformationColor = new Color(255, 213, 120);

        private readonly string _characterName;
        private readonly string _characterDetails;
        private readonly string _status;
        private readonly string _location;
        private readonly string _knownFor;
        private readonly string _currently;
        private readonly string _outOfCharacter;
        private readonly bool _showKnownFor;
        private readonly bool _showCurrently;
        private readonly bool _showOutOfCharacter;
        private readonly bool _trimLongSections;
        private readonly int _maximumLinesPerSection;
        private readonly IReadOnlyList<string> _additionalDetailLines;

        public ProfilePresenceTooltipView(
            string characterName,
            string characterDetails,
            string status,
            string location,
            string knownFor,
            string currently,
            string outOfCharacter,
            bool showKnownFor,
            bool showCurrently,
            bool showOutOfCharacter,
            bool trimLongSections,
            int maximumLinesPerSection,
            IEnumerable<string> additionalDetailLines = null)
        {
            _characterName = Clean(characterName);
            _characterDetails = Clean(characterDetails);
            _status = Clean(status);
            _location = Clean(location);
            _knownFor = Clean(knownFor);
            _currently = Clean(currently);
            _outOfCharacter = Clean(outOfCharacter);
            _showKnownFor = showKnownFor;
            _showCurrently = showCurrently;
            _showOutOfCharacter = showOutOfCharacter;
            _trimLongSections = trimLongSections;
            _maximumLinesPerSection =
                SparkSettings.ProfileTooltipLimit(
                    maximumLinesPerSection);

            _additionalDetailLines = (additionalDetailLines ?? Enumerable.Empty<string>())
                .Select(Clean)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToList();
        }

        protected override void Build(Container buildPanel)
        {
            var tooltipWidth = GetTooltipWidth();
            var contentWidth = tooltipWidth - (Padding * 2);
            var wrapWidth = contentWidth - WrapSafetyPadding;
            var bodyFont = GameService.Content.DefaultFont14;

            var detailLines = BuildDetailLines().SelectMany(line =>TooltipTextLayout.WrapLines(line, wrapWidth, bodyFont)).ToList();

            var knownForSection = BuildSection("Known For", _showKnownFor ? _knownFor : string.Empty, wrapWidth, bodyFont);

            var currentlySection = BuildSection("Currently", _showCurrently ? _currently : string.Empty, wrapWidth, bodyFont);

            var outOfCharacterSection = BuildSection("Out of Character", _showOutOfCharacter ? _outOfCharacter : string.Empty, wrapWidth, bodyFont);

            var sections = new[]
            {
                knownForSection,
                currentlySection,
                outOfCharacterSection
            };

            var height = Padding;

            if (!string.IsNullOrWhiteSpace(_characterName))
                height += TitleHeight;

            height += detailLines.Count * LineHeight;

            foreach (var section in sections)
                height += GetSectionHeight(section);

            height += Padding;

            buildPanel.Size = new Point(tooltipWidth, height);
            buildPanel.BackgroundColor = TooltipBackground;

            if (buildPanel is Panel tooltipPanel)
                tooltipPanel.ShowBorder = true;

            var y = Padding;

            if (!string.IsNullOrWhiteSpace(_characterName))
            {
                new Label
                {
                    Text = _characterName,
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

            y = AddLines(buildPanel, detailLines, y, contentWidth);

            foreach (var section in sections)
            {
                y = AddSection(buildPanel, section, y, contentWidth);
            }
        }

        private int GetTooltipWidth()
        {
            var screenWidth = GameService.Graphics.SpriteScreen.Size.X;
            var availableWidth = Math.Max(220, screenWidth - ScreenEdgePadding);
            var maximumWidth = Math.Min(MaxTooltipWidth, availableWidth);
            var minimumWidth = Math.Min(MinTooltipWidth, maximumWidth);

            var titleFont = GameService.Content.DefaultFont16;
            var bodyFont = GameService.Content.DefaultFont14;

            var measuredWidth = 0f;

            if (!string.IsNullOrWhiteSpace(_characterName))
            {
                measuredWidth = Math.Max(
                    measuredWidth,
                    titleFont.MeasureString(_characterName).Width);
            }

            foreach (var detailLine in BuildDetailLines())
            {
                measuredWidth = Math.Max(measuredWidth, bodyFont.MeasureString(detailLine).Width);
            }

            var hasBodyContent =
                (_showKnownFor &&
                    !string.IsNullOrWhiteSpace(_knownFor)) 
                    || (_showCurrently && !string.IsNullOrWhiteSpace(_currently)) 
                    || (_showOutOfCharacter && !string.IsNullOrWhiteSpace(_outOfCharacter));

            var desiredWidth =
                (int)Math.Ceiling(measuredWidth) +
                (Padding * 2) +
                WidthMeasurePadding;

            if (hasBodyContent)
                desiredWidth = Math.Max(desiredWidth, PreferredBodyWidth);

            return Math.Max(
                minimumWidth,
                Math.Min(maximumWidth, desiredWidth));
        }

        private IEnumerable<string> BuildDetailLines()
        {
            if (!string.IsNullOrWhiteSpace(_characterDetails))
                yield return _characterDetails;

            if (!string.IsNullOrWhiteSpace(_status))
                yield return $"Status: {_status}";

            if (!string.IsNullOrWhiteSpace(_location))
                yield return $"Location: {_location}";

            foreach (var line in _additionalDetailLines)
                yield return line;
        }

        private TooltipSection BuildSection(string title, string text, float wrapWidth, BitmapFont font)
        {
            var allLines = TooltipTextLayout.WrapLines(text, wrapWidth, font).ToList();

            var visibleLines = TrimSection(allLines, out var wasTrimmed);

            return new TooltipSection(title, visibleLines, wasTrimmed);
        }

        private List<string> TrimSection(List<string> lines, out bool wasTrimmed)
        {
            wasTrimmed =
                _trimLongSections &&
                lines.Count > _maximumLinesPerSection;

            return wasTrimmed
                ? lines.Take(_maximumLinesPerSection).ToList()
                : lines;
        }

        private static int GetSectionHeight(TooltipSection section)
        {
            if (section == null || section.Lines.Count == 0)
                return 0;

            var height =
                SectionGap +
                SectionHeaderHeight +
                section.Lines.Count * LineHeight;

            if (section.WasTrimmed)
                height += LineHeight;

            return height;
        }

        private static int AddSection(Container parent, TooltipSection section, int y, int width)
        {
            if (section == null || section.Lines.Count == 0)
                return y;

            y += SectionGap;

            AddSectionHeader(parent, section.Title, y, width);

            y += SectionHeaderHeight;

            y = AddLines(parent, section.Lines, y, width);

            if (section.WasTrimmed)
            {
                y = AddMoreInformationLine(parent, y, width);
            }

            return y;
        }

        private static int AddMoreInformationLine(Container parent, int y, int width)
        {
            new Label
            {
                Text = "(Exceeded max tooltip lines - open profile to read all)",
                Font = GameService.Content.DefaultFont14,
                TextColor = MoreInformationColor,
                HorizontalAlignment = HorizontalAlignment.Center,
                WrapText = false,
                ShowShadow = true,
                Location = new Point(Padding, y),
                Size = new Point(width, LineHeight),
                Parent = parent,
                ZIndex = 1
            };

            return y + LineHeight;
        }

        private static int AddLines(Container parent, IEnumerable<string> lines, int y, int width)
        {
            foreach (var line in lines)
            {
                new Label
                {
                    Text = line,
                    Font = GameService.Content.DefaultFont14,
                    TextColor = BodyColor,
                    WrapText = false,
                    ShowShadow = true,
                    Location = new Point(Padding, y),
                    Size = new Point(width, LineHeight),
                    Parent = parent,
                    ZIndex = 1
                };

                y += LineHeight;
            }

            return y;
        }

        private static void AddSectionHeader(Container parent, string text, int y, int width)
        {
            new TooltipSectionHeader(text)
            {
                Location = new Point(Padding + SectionHeaderSideInset, y),
                Size = new Point(Math.Max(0, width - (SectionHeaderSideInset * 2)), SectionHeaderHeight),
                Parent = parent,
                ZIndex = 1
            };
        }

        private static string Clean(string text)
        {
            return text?.Trim() ?? string.Empty;
        }

        private sealed class TooltipSection
        {
            public string Title { get; }
            public List<string> Lines { get; }
            public bool WasTrimmed { get; }

            public TooltipSection(string title, List<string> lines, bool wasTrimmed)
            {
                Title = title ?? string.Empty;
                Lines = lines ?? new List<string>();
                WasTrimmed = wasTrimmed;
            }
        }

        private sealed class TooltipSectionHeader : Control
        {
            private readonly string _text;

            public TooltipSectionHeader(string text)
            {
                _text = text ?? string.Empty;
            }

            protected override void Paint(SpriteBatch spriteBatch, Rectangle bounds)
            {
                var font = GameService.Content.DefaultFont14;
                var textWidth = (int)Math.Ceiling(font.MeasureString(_text).Width);
                var textX = Math.Max(8, (Width - textWidth) / 2);
                var lineY = Height / 2 + 1;

                spriteBatch.DrawOnCtrl(this, ContentService.Textures.Pixel, new Rectangle(0, lineY, Math.Max(0, textX - 8), 1), SectionLineColor);

                var rightLineX = textX + textWidth + 8;

                spriteBatch.DrawOnCtrl(
                    this,
                    ContentService.Textures.Pixel,
                    new Rectangle(rightLineX, lineY, Math.Max(0, Width - rightLineX), 1), SectionLineColor);

                var textBounds = new Rectangle(textX, 0, textWidth + 4, Height);

                spriteBatch.DrawStringOnCtrl(
                    this,
                    _text,
                    font,
                    new Rectangle(
                        textBounds.X + 1,
                        textBounds.Y + 1,
                        textBounds.Width,
                        textBounds.Height),
                    StandardColors.Shadow);

                spriteBatch.DrawStringOnCtrl(
                    this,
                    _text,
                    font,
                    textBounds,
                    SectionColor * 0.9f);
            }
        }
    }
}