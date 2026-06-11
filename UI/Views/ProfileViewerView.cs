using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Graphics.UI;
using Microsoft.Xna.Framework;
using rp.spark.Models;
using rp.spark.Services;
using rp.spark.UI.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace rp.spark.UI.Views
{
    public class ProfileViewerView : View
    {
        private readonly Func<CharacterProfile, PlayerPresence, string> _toggleBookmark;
        private readonly Func<CharacterProfile, PlayerPresence, bool> _isBookmarked;
        private readonly Func<CharacterProfile, PlayerPresence, string> _toggleBlock;
        private readonly Func<CharacterProfile, PlayerPresence, bool> _isBlocked;
        private readonly Func<CharacterProfile, PlayerPresence, string, Task<string>> _reportProfile;

        private CharacterProfile _profile;
        private PlayerPresence _presence;
        private Container _buildPanel;
        private Panel _contentPanel;
        private Panel _scrollViewport;
        private Panel _reportPanel;
        private Layout _layout;
        private Label _status;
        private int _headerOffset;

        public ProfileViewerView(
            CharacterProfile profile,
            PlayerPresence presence,
            Func<CharacterProfile, PlayerPresence, string> toggleBookmark = null,
            Func<CharacterProfile, PlayerPresence, bool> isBookmarked = null,
            Func<CharacterProfile, PlayerPresence, string> toggleBlock = null,
            Func<CharacterProfile, PlayerPresence, bool> isBlocked = null,
            Func<CharacterProfile, PlayerPresence, string, Task<string>> reportProfile = null)
        {
            _toggleBookmark = toggleBookmark;
            _isBookmarked = isBookmarked;
            _toggleBlock = toggleBlock;
            _isBlocked = isBlocked;
            _reportProfile = reportProfile;
            RememberProfile(profile, presence);
        }

        protected override void Build(Container buildPanel)
        {
            _buildPanel = buildPanel;
            CreateContentRoot();
            RefreshProfile();
        }

        public void SetProfile(CharacterProfile profile, PlayerPresence presence)
        {
            RememberProfile(profile, presence);

            if (_contentPanel != null)
                RefreshProfile();
        }

        private void RememberProfile(CharacterProfile profile, PlayerPresence presence)
        {
            _profile = profile ?? new CharacterProfile();
            _presence = presence ?? new PlayerPresence();
        }

        private void CreateContentRoot()
        {
            ClearChildren(_buildPanel);

            _contentPanel = new Panel
            {
                ShowBorder = false,
                Location = Point.Zero,
                Size = _buildPanel.ContentRegion.Size,
                Parent = _buildPanel
            };
        }

        private void RefreshProfile()
        {
            if (_contentPanel == null)
                return;

            ClearChildren(_contentPanel);

            _scrollViewport = null;
            _layout = null;
            _status = null;

            _headerOffset = BuildHeader(_contentPanel);
            _layout = new Layout(_headerOffset, _contentPanel.Size);
            BuildGlance(_contentPanel);
            BuildStatus(_contentPanel);
            BuildBody(_contentPanel);
            BuildScrollBar(_contentPanel);
        }

        private static void ClearChildren(Container container)
        {
            if (container == null)
                return;

            foreach (var child in container.Children.ToArray())
                child.Dispose();
        }

        private int BuildHeader(Container buildPanel)
        {
            var displayName = ProfileText.DisplayName(_profile);
            var pronouns = GetPronounsText();
            var officialName = _profile.CharacterName?.Trim() ?? string.Empty;
            var showOfficialName = !string.IsNullOrWhiteSpace(officialName)
                                && !string.Equals(displayName, officialName, StringComparison.OrdinalIgnoreCase);
            var wrappedHeader = ShouldWrapHeader(displayName, pronouns, showOfficialName ? officialName : string.Empty);
            var secondaryHeaderY = Layout.SecondaryHeaderY(wrappedHeader);
            var characterDetailsY = Layout.CharacterDetailsY(wrappedHeader);
            var metadataY = Layout.MetadataY(wrappedHeader);

            var nameWidth = Math.Min(
                500,
                (int)Math.Ceiling(GameService.Content.DefaultFont32.MeasureString(displayName).Width) + 4);

            new Label
            {
                Text = displayName,
                Font = GameService.Content.DefaultFont32,
                TextColor = Color.White,
                StrokeText = true,
                WrapText = false,
                Location = new Point(0, 0),
                Size = new Point(nameWidth, 45),
                Parent = buildPanel
            };

            var nextHeaderX = wrappedHeader ? 0 : nameWidth + 8;

            if (!string.IsNullOrWhiteSpace(pronouns))
            {
                var pronounsText = $"({pronouns})";
                var pronounsWidth = GetSecondaryHeaderWidth(pronounsText);

                new Label
                {
                    Text = pronounsText,
                    Font = GameService.Content.DefaultFont16,
                    TextColor = new Color(220, 220, 220),
                    WrapText = false,
                    Location = new Point(nextHeaderX, secondaryHeaderY),
                    Size = new Point(pronounsWidth, 25),
                    Parent = buildPanel
                };

                nextHeaderX += pronounsWidth + 8;
            }

            if (showOfficialName)
            {
                var officialNameText = $"({officialName})";

                new Label
                {
                    Text = officialNameText,
                    Font = GameService.Content.DefaultFont16,
                    TextColor = new Color(220, 220, 220),
                    WrapText = false,
                    Location = new Point(nextHeaderX, secondaryHeaderY),
                    Size = new Point(GetSecondaryHeaderWidth(officialNameText), 25),
                    Parent = buildPanel
                };
            }

            var characterDetails = ProfileText.ProfileCharacterDetails(_profile);

            if (!string.IsNullOrWhiteSpace(characterDetails))
            {
                new Label
                {
                    Text = characterDetails,
                    Font = GameService.Content.DefaultFont16,
                    TextColor = new Color(220, 220, 220),
                    WrapText = false,
                    Location = new Point(0, characterDetailsY),
                    Size = new Point(480, 25),
                    Parent = buildPanel
                };
            }

            string metadata = $"Current location: {ProfileText.PresenceLocation(_presence)} | Account: {ProfileText.AccountName(_profile, _presence, string.Empty)}";

            if (!string.IsNullOrWhiteSpace(metadata))
            {
                new Label
                {
                    Text = metadata,
                    Font = GameService.Content.DefaultFont16,
                    TextColor = new Color(220, 220, 220),
                    WrapText = false,
                    Location = new Point(0, metadataY),
                    Size = new Point(480, 25),
                    Parent = buildPanel
                };
            }

            // Copy the account name for them to whisper
            // Removed virtual keypress system to attempt to set up /w <name> since it was buggy
            var whisperButton = new StandardButton
            {
                Text = "Copy Name",
                Location = new Point(405, 8),
                Size = new Point(90, 30),
                Parent = buildPanel
            };

            SparkUiActions.BindClick(
                whisperButton,
                CopyAccountNameAsync,
                SetStatusText,
                "Couldn't copy the account name right now.");

            var bookmarkButton = new StandardButton
            {
                Text = GetBookmarkButtonText(),
                Location = new Point(503, 8),
                Size = new Point(125, 30),
                Parent = buildPanel
            };

            bookmarkButton.Click += (s, e) =>
            {
                _status.Text = _toggleBookmark == null
                    ? "Bookmark cache unavailable."
                    : _toggleBookmark(_profile, _presence);
                bookmarkButton.Text = GetBookmarkButtonText();
            };

            var blockButton = new StandardButton
            {
                Text = GetBlockButtonText(),
                Location = new Point(636, 8),
                Size = new Point(64, 30),
                Parent = buildPanel
            };

            blockButton.Click += (s, e) =>
            {
                _status.Text = _toggleBlock == null
                    ? "Block list unavailable."
                    : _toggleBlock(_profile, _presence);
                blockButton.Text = GetBlockButtonText();
            };

            var reportButton = new StandardButton
            {
                Text = "Report",
                Location = new Point(708, 8),
                Size = new Point(70, 30),
                Parent = buildPanel
            };

            reportButton.Click += (s, e) =>
            {
                if (!CanReportViewedProfile())
                {
                    SetStatusText(ReportUnavailableMessage);
                    return;
                }

                OpenReportPanel();
            };

            _status = new Label
            {
                Text = string.Empty,
                Font = GameService.Content.DefaultFont12,
                TextColor = new Color(220, 220, 220),
                Location = new Point(500, 42),
                Size = new Point(278, 48),
                WrapText = true,
                Parent = buildPanel
            };

            return wrappedHeader ? Layout.WrappedHeaderOffset : 0;
        }

        private void BuildGlance(Container buildPanel)
        {
            var entries = GetGlanceEntries().ToList();

            if (!entries.Any())
                return;

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var bounds = _layout.GlanceIconBounds(i);
                var icon = new AssetIcon
                {
                    Location = bounds.Location,
                    Size = bounds.Size,
                    Parent = buildPanel,
                    BackgroundColor = new Color(20, 20, 20, 180),
                    Tooltip = MakeGlanceTooltip(entry)
                };

                icon.SetAssetId(entry.AssetId);
            }
        }

        private void BuildStatus(Container buildPanel)
        {
            if (_presence.Status == RPStatus.Invisible)
                return;

            var bounds = _layout.ProfileStatusBounds;

            new Label
            {
                Text = $"Status: {ProfileLabels.StatusLabel(_presence.Status)}",
                Font = GameService.Content.DefaultFont18,
                TextColor = new Color(255, 233, 180),
                StrokeText = true,
                WrapText = false,
                Location = bounds.Location,
                Size = bounds.Size,
                BasicTooltipText = _presence.StatusMessage ?? string.Empty,
                Parent = buildPanel
            };
        }

        private void BuildBody(Container buildPanel)
        {
            var viewportBounds = _layout.ViewportBounds;

            _scrollViewport = new MouseWheelPanel
            {
                ShowBorder = false,
                Location = viewportBounds.Location,
                Size = viewportBounds.Size,
                Parent = buildPanel,
                ClipsBounds = true,
                BackgroundColor = new Color(0, 0, 0, 60)
            };

            var y = _layout.TextStartY;
            y = AddHighlights(_scrollViewport, y);

            if (y > _layout.TextStartY)
                y += _layout.SectionGap;

            var currently = GetCurrentlyText();
            if (!string.IsNullOrWhiteSpace(currently))
            {
                y = AddSection(_scrollViewport, "Currently:", currently, y);
                y += _layout.SectionGap;
            }

            y = AddSection(_scrollViewport, "Known for:", GetKnownForText(), y);
            y += _layout.SectionGap;
            y = AddSection(_scrollViewport, "Description:", GetDescriptionText(), y);

            var outOfCharacterInfo = GetOtherInfoText();
            if (!string.IsNullOrWhiteSpace(outOfCharacterInfo))
            {
                y += _layout.SectionGap;
                AddSection(_scrollViewport, "Other information:", outOfCharacterInfo, y);
            }

            _scrollViewport.VerticalScrollOffset = 0;
        }

        private void BuildScrollBar(Container buildPanel)
        {
            var bounds = _layout.ScrollbarBounds;

            new Scrollbar(_scrollViewport)
            {
                Location = bounds.Location,
                Size = bounds.Size,
                Parent = buildPanel
            };
        }

        private int AddSection(Container parent, string title, string text, int y)
        {
            new Label
            {
                Text = title,
                Font = GameService.Content.DefaultFont18,
                TextColor = new Color(255, 233, 180),
                StrokeText = true,
                Location = new Point(_layout.TextX, y),
                Size = new Point(_layout.TextLabelWidth, 28),
                Parent = parent
            };

            y += 32;
            return AddWrappedLabel(parent, text, y, GameService.Content.DefaultFont16, _layout.TextLineHeight);
        }

        private int AddHighlights(Container parent, int y)
        {
            var lines = HighlightLines().ToList();

            if (!lines.Any())
                return y;

            foreach (var line in lines)
                y = AddSingleLine(parent, line, y);

            return y;
        }

        private int AddSingleLine(Container parent, string text, int y)
        {
            new Label
            {
                Text = text,
                Font = GameService.Content.DefaultFont14,
                TextColor = new Color(210, 210, 210),
                Location = new Point(_layout.TextX, y),
                Size = new Point(_layout.TextLabelWidth, 26),
                Parent = parent
            };

            return y + 28;
        }

        private IEnumerable<string> HighlightLines()
        {
            if (_profile.Experience != ProfileExperience.Hidden)
                yield return $"Experience: {ProfileLabels.GetExperienceLabel(_profile.Experience)}";

            var preferences = SelectedPreferences(_profile.Preferences);
            if (!string.IsNullOrWhiteSpace(preferences))
                yield return $"Preferences: {preferences}";

            var themes = SelectedThemes(_profile.Themes);
            if (!string.IsNullOrWhiteSpace(themes))
                yield return $"Themes: {themes}";

            var styles = SelectedStyles(_profile.Styles);
            if (!string.IsNullOrWhiteSpace(styles))
                yield return $"Styles: {styles}";
        }

        private static string SelectedPreferences(ProfilePreferenceFlags flags)
        {
            return string.Join(
                ", ",
                ProfileLabels.PreferenceOptions
                    .Where(option => (flags & option.Key) == option.Key)
                    .Select(option => option.Value));
        }

        private static string SelectedThemes(ProfileThemeFlags flags)
        {
            return string.Join(
                ", ",
                ProfileLabels.ThemeOptions
                    .Where(option => (flags & option.Key) == option.Key)
                    .Select(option => option.Value));
        }

        private static string SelectedStyles(ProfileStyleFlags flags)
        {
            return string.Join(
                ", ",
                ProfileLabels.StyleOptions
                    .Where(option => (flags & option.Key) == option.Key)
                    .Select(option => option.Value));
        }

        private int AddWrappedLabel(Container parent, string text, int y, MonoGame.Extended.BitmapFonts.BitmapFont font, int lineHeight)
        {
            foreach (var line in WrapTextLines(text, _layout.TextWidth, font))
            {
                if (line.Length == 0)
                {
                    y += _layout.ParagraphGap;
                    continue;
                }

                new Label
                {
                    Text = line,
                    Font = font,
                    TextColor = Color.White,
                    WrapText = false,
                    Location = new Point(_layout.TextX, y),
                    Size = new Point(_layout.TextLabelWidth, lineHeight),
                    Parent = parent
                };

                y += lineHeight;
            }

            return y + _layout.ParagraphGap;
        }

        private IEnumerable<AtAGlanceEntry> GetGlanceEntries()
        {
            return (_profile.AtAGlance ?? new List<AtAGlanceEntry>())
                .Where(entry => entry != null && entry.AssetId > 0)
                .Take(ProfileLimits.MaxAtAGlanceEntries);
        }

        private static Tooltip MakeGlanceTooltip(AtAGlanceEntry entry)
        {
            return new Tooltip(new GlanceTooltipView(entry?.Title, entry?.Description));
        }

        private string GetBookmarkButtonText()
        {
            return IsBookmarked()
                ? "Remove Bookmark"
                : "Bookmark";
        }

        private bool IsBookmarked()
        {
            try
            {
                return _isBookmarked?.Invoke(_profile, _presence) == true;
            }
            catch
            {
                return false;
            }
        }

        private string GetBlockButtonText()
        {
            return IsBlocked()
                ? "Unblock"
                : "Block";
        }

        private bool IsBlocked()
        {
            try
            {
                return _isBlocked?.Invoke(_profile, _presence) == true;
            }
            catch
            {
                return false;
            }
        }

        private void OpenReportPanel()
        {
            CloseReportPanel();

            var popupParent = _contentPanel ?? _buildPanel ?? GameService.Graphics.SpriteScreen;

            _reportPanel = new Panel
            {
                ShowBorder = true,
                Title = "Report Profile",
                Size = new Point(460, 170),
                Location = GetCenteredPopupLocation(popupParent, 460, 170),
                Parent = popupParent,
                BackgroundColor = new Color(38, 35, 32),
                ClipsBounds = false,
                ZIndex = 100
            };

            var closeButton = new StandardButton
            {
                Text = "X",
                Location = new Point(428, -28),
                Size = new Point(24, 24),
                Parent = _reportPanel,
                ClipsBounds = false,
                ZIndex = 10011
            };

            closeButton.Click += (s, e) => CloseReportPanel();

            new Label
            {
                Text = $"Report reason ({ProfileLimits.MaxReportReasonLength} characters)",
                Font = GameService.Content.DefaultFont14,
                TextColor = Color.White,
                Location = new Point(12, 14),
                Size = new Point(390, 24),
                Parent = _reportPanel
            };

            var reasonBox = new TextBox
            {
                Text = string.Empty,
                PlaceholderText = "Please provide a reason for reporting this profile.",
                MaxLength = ProfileLimits.MaxReportReasonLength,
                Location = new Point(12, 42),
                Size = new Point(432, 32),
                Parent = _reportPanel
            };

            var popupStatus = new Label
            {
                Text = string.Empty,
                Font = GameService.Content.DefaultFont12,
                TextColor = new Color(220, 220, 220),
                WrapText = true,
                Location = new Point(12, 122),
                Size = new Point(432, 38),
                Parent = _reportPanel
            };

            var submitButton = new StandardButton
            {
                Text = "Submit",
                Location = new Point(354, 84),
                Size = new Point(90, 30),
                Parent = _reportPanel
            };

            SparkUiActions.BindClick(
                submitButton,
                async () => await SubmitReportAsync(reasonBox.Text, popupStatus),
                text => popupStatus.Text = text ?? string.Empty,
                "Report failed.");

            reasonBox.EnterPressed += async (s, e) => await SubmitReportAsync(reasonBox.Text, popupStatus);
        }

        private async Task SubmitReportAsync(string reason, Label popupStatus)
        {
            reason = reason?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(reason))
            {
                popupStatus.Text = "Please add a reason for reporting this character.";
                return;
            }

            var message = _reportProfile == null
                ? "Report failed."
                : await _reportProfile(_profile, _presence, reason);

            SetStatusText(message);
            CloseReportPanel();
        }

        private void CloseReportPanel()
        {
            _reportPanel?.Dispose();
            _reportPanel = null;
        }

        private static Point GetCenteredPopupLocation(Container parent, int width, int height)
        {
            var parentSize = parent?.ContentRegion.Size ?? GameService.Graphics.SpriteScreen.Size;
            const int padding = 8;

            var x = (parentSize.X - width) / 2;
            var y = (parentSize.Y - height) / 2;

            return new Point(Math.Max(padding, x), Math.Max(padding, y));
        }

        private bool CanReportViewedProfile()
        {
            return _presence != null
                && _presence.Status != RPStatus.Offline
                && _presence.Status != RPStatus.Invisible
                && !string.IsNullOrWhiteSpace(TextUtil.FirstNonEmpty(_presence.AccountName, _profile?.AccountName))
                && !string.IsNullOrWhiteSpace(TextUtil.FirstNonEmpty(_presence.OfficialCharacterName, _profile?.CharacterName))
                && !string.IsNullOrWhiteSpace(TextUtil.FirstNonEmpty(_presence.ActiveProfileId, _profile?.ProfileId));
        }

        private string GetPronounsText()
        {
            return _profile.Pronouns?.Trim() ?? string.Empty;
        }

        // Long names might push things into the buttons, so wrapping it beneath is the easiest fix
        private static bool ShouldWrapHeader(string displayName, string pronouns, string officialName)
        {
            var combinedHeader = displayName ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(pronouns))
                combinedHeader += $" ({pronouns.Trim()})";

            if (!string.IsNullOrWhiteSpace(officialName))
                combinedHeader += $" ({officialName.Trim()})";

            return combinedHeader.Length > Layout.HeaderCharacterLimit
                && (!string.IsNullOrWhiteSpace(pronouns) || !string.IsNullOrWhiteSpace(officialName));
        }

        private static int GetSecondaryHeaderWidth(string text)
        {
            return Math.Min(
                480,
                (int)Math.Ceiling(GameService.Content.DefaultFont16.MeasureString(text ?? string.Empty).Width) + 4);
        }

        private string GetKnownForText()
        {
            return string.IsNullOrWhiteSpace(_profile.KnownFor)
                ? "Not set."
                : _profile.KnownFor.Trim();
        }

        private string GetDescriptionText()
        {
            return string.IsNullOrWhiteSpace(_profile.Description)
                ? "Missing description."
                : _profile.Description.Trim();
        }

        private string GetCurrentlyText()
        {
            return string.IsNullOrWhiteSpace(_profile.Currently)
                ? string.Empty
                : _profile.Currently.Trim();
        }

        private string GetOtherInfoText()
        {
            return string.IsNullOrWhiteSpace(_profile.OutOfCharacterInfo)
                ? string.Empty
                : _profile.OutOfCharacterInfo.Trim();
        }

        private static IEnumerable<string> WrapTextLines(
            string text,
            float maxWidth,
            MonoGame.Extended.BitmapFonts.BitmapFont font)
        {
            var normalized = (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');

            foreach (var paragraph in normalized.Split('\n'))
            {
                if (string.IsNullOrWhiteSpace(paragraph))
                {
                    yield return string.Empty;
                    continue;
                }

                var currentLine = string.Empty;

                foreach (var word in paragraph.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var candidate = string.IsNullOrEmpty(currentLine) ? word : $"{currentLine} {word}";

                    if (font.MeasureString(candidate).Width <= maxWidth)
                    {
                        currentLine = candidate;
                        continue;
                    }

                    if (!string.IsNullOrEmpty(currentLine))
                    {
                        yield return currentLine;
                        currentLine = string.Empty;
                    }

                    if (font.MeasureString(word).Width <= maxWidth)
                    {
                        currentLine = word;
                        continue;
                    }

                    foreach (var wordSegment in BreakLongWord(word, maxWidth, font))
                        yield return wordSegment;
                }

                if (!string.IsNullOrEmpty(currentLine))
                    yield return currentLine;
            }
        }

        private static IEnumerable<string> BreakLongWord(
            string word,
            float maxWidth,
            MonoGame.Extended.BitmapFonts.BitmapFont font)
        {
            var currentSegment = string.Empty;

            foreach (var character in word)
            {
                var candidate = currentSegment + character;

                if (font.MeasureString(candidate).Width <= maxWidth || string.IsNullOrEmpty(currentSegment))
                {
                    currentSegment = candidate;
                    continue;
                }

                yield return currentSegment;
                currentSegment = character.ToString();
            }

            if (!string.IsNullOrEmpty(currentSegment))
                yield return currentSegment;
        }

        private async Task CopyAccountNameAsync()
        {
            var accountName = ProfileText.AccountName(_profile, _presence, string.Empty);

            if (string.IsNullOrWhiteSpace(accountName))
            {
                _status.Text = "No account name available for this profile.";
                return;
            }

            try
            {
                await CopyTextAsync(accountName.Trim());
                _status.Text = $"Copied {accountName.Trim()}.";
            }
            catch
            {
                _status.Text = "Couldn't copy the account name right now.";
            }
        }

        private void SetStatusText(string text)
        {
            if (_status != null)
                _status.Text = text ?? string.Empty;
        }

        private static async Task CopyTextAsync(string text)
        {
            var clipboardSet = await ClipboardUtil.WindowsClipboardService.SetTextAsync(text ?? string.Empty);

            if (!clipboardSet)
                throw new InvalidOperationException("Could not copy text.");
        }

        protected override void Unload()
        {
            CloseReportPanel();
            ClearChildren(_contentPanel);
            _contentPanel?.Dispose();
            _contentPanel = null;
            _scrollViewport = null;
            _layout = null;
            _status = null;
            _buildPanel = null;
        }

        private const string ReportUnavailableMessage = "Reported Profile not found on SPARK server. Please try again later.";

        // Centralizing the layout measurements
        private sealed class Layout
        {
            private const int ScrollbarWidth = 12;
            private const int ScrollbarGap = 0;
            private const int ViewportMinWidth = 260;
            private const int ViewportRightPadding = 8;
            private const int ViewportY = 144;
            private const int ViewportBottomPadding = 8;
            private const int IconY = 94;
            private const int IconSize = 50;
            private const int IconGap = 8;
            private const int TextXValue = 1;
            private const int TextRightPadding = 24;

            public const int HeaderCharacterLimit = 28;
            public const int WrappedHeaderOffset = 24;

            private readonly int _headerOffset;
            private readonly Point _contentSize;

            public Layout(int headerOffset, Point contentSize)
            {
                _headerOffset = headerOffset;
                _contentSize = contentSize;
            }

            public int TextX => TextXValue;

            public int TextStartY => 8;

            public int TextWidth => ViewportWidth - (TextXValue * 2) - TextRightPadding;

            public int TextLabelWidth => ViewportWidth - TextXValue - 18;

            public int SectionGap => 16;

            public int TextLineHeight => 26;

            public int ParagraphGap => 10;

            private int ViewportHeight => Math.Max(
                160,
                _contentSize.Y - ViewportY - ViewportBottomPadding);

            private int ViewportWidth => Math.Max(
                ViewportMinWidth,
                _contentSize.X - ScrollbarWidth - ScrollbarGap - ViewportRightPadding);

            public Rectangle ViewportBounds => new Rectangle(
                0,
                ViewportY + _headerOffset,
                ViewportWidth,
                ViewportHeight - _headerOffset);

            public Rectangle ScrollbarBounds => new Rectangle(
                ViewportWidth + ScrollbarGap,
                ViewportY + _headerOffset,
                ScrollbarWidth,
                ViewportHeight - _headerOffset);

            public Rectangle ProfileStatusBounds => new Rectangle(
                560,
                IconY + 10 + _headerOffset,
                190,
                28);

            public Rectangle GlanceIconBounds(int index)
            {
                return new Rectangle(
                    index * (IconSize + IconGap),
                    IconY + _headerOffset,
                    IconSize,
                    IconSize);
            }

            public static int SecondaryHeaderY(bool wrappedHeader)
            {
                return wrappedHeader ? 42 : 14;
            }

            public static int CharacterDetailsY(bool wrappedHeader)
            {
                return wrappedHeader ? 66 : 42;
            }

            public static int MetadataY(bool wrappedHeader)
            {
                return wrappedHeader ? 90 : 70;
            }
        }

        private class MouseWheelPanel : Panel
        {
            protected override CaptureType CapturesInput()
            {
                return CaptureType.Mouse | CaptureType.MouseWheel;
            }
        }
    }
}
