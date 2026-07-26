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
        private const string OfficialDeveloperAccount = "Bat.8570";
        private const int DeveloperBadgeAssetId = 3307061;
        private const int DeveloperBadgeSlot = 5;

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
        private bool _isSubmittingReport;

        private static readonly Logger Logger = Logger.GetLogger<ProfileViewerView>();

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
            var contentPanel = _contentPanel;
            if (contentPanel == null)
                return;

            ClearChildren(contentPanel);

            if (!ReferenceEquals(_contentPanel, contentPanel))
                return;

            _scrollViewport = null;
            _layout = null;
            _status = null;

            _headerOffset = BuildHeader(contentPanel);

            if (!ReferenceEquals(_contentPanel, contentPanel))
                return;

            _layout = new Layout(_headerOffset, contentPanel.Size);
            BuildGlance(contentPanel);
            BuildStatus(contentPanel);
            BuildBody(contentPanel);
            BuildScrollBar(contentPanel);
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
            var profileTraitsY = Layout.ProfileTraitsY(wrappedHeader);
            var showProfileTraits = HasProfileTraits();

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

            if (showProfileTraits)
                BuildProfileTraits(buildPanel, profileTraitsY);

            // Removed virtual keypress system to attempt to set up /w <name> since it was buggy
            var copyNameButton = new StandardButton
            {
                Text = "Copy Name",
                Location = new Point(405, 8),
                Size = new Point(90, 30),
                Parent = buildPanel
            };

            SparkUiActions.BindClick(
                copyNameButton,
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

            return (wrappedHeader ? Layout.WrappedHeaderOffset : 0)
                 + (showProfileTraits ? Layout.ProfileTraitsOffset : 0);
        }

        private bool HasProfileTraits()
        {
            return _profile.Experience != ProfileExperience.Hidden
                || _profile.Preferences != ProfilePreferenceFlags.None
                || _profile.Themes != ProfileThemeFlags.None
                || _profile.Styles != ProfileStyleFlags.None
                || IsMatureProfile();
        }

        private bool IsMatureProfile()
        {
            return _profile?.IsMature == true
                || _presence?.IsMature == true;
        }

        private bool IsOfficialDeveloper()
        {
            return string.Equals(
                _presence?.AccountName?.Trim(),
                OfficialDeveloperAccount,
                StringComparison.OrdinalIgnoreCase);
        }

        private void BuildGlance(Container buildPanel)
        {
            var layout = _layout;
            if (buildPanel == null || layout == null)
                return;

            var entries = GetGlanceEntries().ToList();

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var bounds = layout.GlanceIconBounds(i);
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

            if (IsOfficialDeveloper())
                AddDeveloperBadge(buildPanel, layout);
        }

        private static void AddDeveloperBadge(Container parent, Layout layout)
        {
            const int borderThickness = 2;

            var bounds = layout.GlanceIconBounds(DeveloperBadgeSlot);
            var tooltip = new Tooltip(new ProfileTooltipView(
                "Official SPARK Developer",
                "This account belongs to an official developer of SPARK.",
                "At A Glance"));

            var border = new Panel
            {
                ShowBorder = false,
                Location = bounds.Location,
                Size = bounds.Size,
                Parent = parent,
                BackgroundColor = new Color(255, 194, 55),
                Tooltip = tooltip
            };

            var icon = new AssetIcon
            {
                Location = new Point(borderThickness, borderThickness),
                Size = new Point(
                    bounds.Width - borderThickness * 2,
                    bounds.Height - borderThickness * 2),
                Parent = border,
                BackgroundColor = new Color(20, 20, 20, 180),
                Tooltip = tooltip
            };

            icon.SetAssetId(DeveloperBadgeAssetId);
        }

        private void BuildStatus(Container buildPanel)
        {
            var layout = _layout;
            if (buildPanel == null || layout == null)
                return;

            if (_presence.Status == RPStatus.Invisible)
                return;

            var bounds = layout.ProfileStatusBounds;
            var statusText = ProfileLabels.StatusLabel(_presence.Status);
            var statusWidth = GetStatusTextWidth(statusText);
            var prefixWidth = GetStatusTextWidth("Status:");
            var statusX = bounds.Right - statusWidth;

            new Label
            {
                Text = "Status:",
                Font = GameService.Content.DefaultFont18,
                TextColor = new Color(255, 233, 180),
                StrokeText = true,
                WrapText = false,
                HorizontalAlignment = HorizontalAlignment.Right,
                Location = new Point(statusX - prefixWidth - Layout.StatusGap, bounds.Y),
                Size = new Point(prefixWidth, bounds.Height),
                Parent = buildPanel
            };

            new Label
            {
                Text = statusText,
                Font = GameService.Content.DefaultFont18,
                TextColor = ProfileStatusColors.Get(_presence.Status),
                StrokeText = true,
                WrapText = false,
                HorizontalAlignment = HorizontalAlignment.Right,
                Location = new Point(statusX, bounds.Y),
                Size = new Point(statusWidth, bounds.Height),
                Parent = buildPanel
            };
        }

        private static int GetStatusTextWidth(string text)
        {
            return (int)Math.Ceiling(
                GameService.Content.DefaultFont18.MeasureString(text ?? string.Empty).Width) + 2;
        }

        private void BuildBody(Container buildPanel)
        {
            var layout = _layout;
            if (buildPanel == null || layout == null)
                return;

            var viewportBounds = layout.ViewportBounds;

            var scrollViewport = new MouseWheelPanel
            {
                ShowBorder = false,
                Location = viewportBounds.Location,
                Size = viewportBounds.Size,
                Parent = buildPanel,
                ClipsBounds = true,
                BackgroundColor = new Color(0, 0, 0, 60)
            };

            _scrollViewport = scrollViewport;

            var y = layout.TextStartY;
            var currently = GetCurrentlyText();
            if (!string.IsNullOrWhiteSpace(currently))
            {
                y = AddSection(scrollViewport, layout, "Currently:", currently, y);
                y += layout.SectionGap;
            }

            y = AddSection(scrollViewport, layout, "Known for:", GetKnownForText(), y);
            y += layout.SectionGap;
            y = AddSection(scrollViewport, layout, "Description:", GetDescriptionText(), y);

            var outOfCharacterInfo = GetOtherInfoText();
            if (!string.IsNullOrWhiteSpace(outOfCharacterInfo))
            {
                y += layout.SectionGap;
                AddSection(scrollViewport, layout, "Other information:", outOfCharacterInfo, y);
            }

            scrollViewport.VerticalScrollOffset = 0;
        }

        private void BuildScrollBar(Container buildPanel)
        {
            var layout = _layout;
            var scrollViewport = _scrollViewport;

            if (buildPanel == null || layout == null || scrollViewport == null)
                return;

            var bounds = layout.ScrollbarBounds;

            new Scrollbar(scrollViewport)
            {
                Location = bounds.Location,
                Size = bounds.Size,
                Parent = buildPanel
            };
        }

        private int AddSection(Container parent, Layout layout, string title, string text, int y)
        {
            if (parent == null || layout == null)
                return y;

            new Label
            {
                Text = title,
                Font = GameService.Content.DefaultFont18,
                TextColor = new Color(255, 233, 180),
                StrokeText = true,
                Location = new Point(layout.TextX, y),
                Size = new Point(layout.TextLabelWidth, 28),
                Parent = parent
            };

            y += 32;
            return AddWrappedLabel(parent, layout, text, y, GameService.Content.DefaultFont16, layout.TextLineHeight);
        }

        private void BuildProfileTraits(Container parent, int y)
        {
            var experienceSet = _profile.Experience != ProfileExperience.Hidden;
            var preferences = SelectedPreferences(_profile.Preferences, Environment.NewLine);
            var themes = SelectedThemes(_profile.Themes, Environment.NewLine);
            var styles = SelectedStyles(_profile.Styles, Environment.NewLine);
            var x = 0;

            AddProfileTraitLabel(
                parent,
                ref x,
                y,
                experienceSet
                    ? $"Experience: {ProfileLabels.GetExperienceLabel(_profile.Experience)}"
                    : "Experience",
                experienceSet
                    ? null
                    : MakeProfileTraitTooltip("Experience", "No experience set."),
                experienceSet);

            AddProfileTraitSeparator(parent, ref x, y);
            AddProfileTraitLabel(
                parent,
                ref x,
                y,
                "Preferences",
                MakeProfileTraitTooltip(
                    "Preferences",
                    string.IsNullOrWhiteSpace(preferences) ? "No preferences set." : preferences),
                !string.IsNullOrWhiteSpace(preferences));

            AddProfileTraitSeparator(parent, ref x, y);
            AddProfileTraitLabel(
                parent,
                ref x,
                y,
                "Themes",
                MakeProfileTraitTooltip(
                    "Themes",
                    string.IsNullOrWhiteSpace(themes) ? "No themes set." : themes),
                !string.IsNullOrWhiteSpace(themes));

            AddProfileTraitSeparator(parent, ref x, y);
            AddProfileTraitLabel(
                parent,
                ref x,
                y,
                "Styles",
                MakeProfileTraitTooltip(
                    "Styles",
                    string.IsNullOrWhiteSpace(styles) ? "No styles set." : styles),
                !string.IsNullOrWhiteSpace(styles));

            if (IsMatureProfile())
            {
                AddProfileTraitSeparator(parent, ref x, y);
                AddProfileTraitLabel(
                    parent,
                    ref x,
                    y,
                    "Mature",
                    MakeProfileTraitTooltip("Mature", "This profile is marked Mature/18+."),
                    true);
            }
        }

        private static void AddProfileTraitLabel(
            Container parent,
            ref int x,
            int y,
            string text,
            Tooltip tooltip,
            bool isSet)
        {
            var width = GetProfileTraitWidth(text);

            new Label
            {
                Text = text,
                Font = GameService.Content.DefaultFont16,
                TextColor = isSet
                    ? new Color(220, 220, 220)
                    : new Color(125, 125, 125),
                WrapText = false,
                Location = new Point(x, y),
                Size = new Point(width, 25),
                Tooltip = tooltip,
                Parent = parent
            };

            x += width;
        }

        private static Tooltip MakeProfileTraitTooltip(string title, string description)
        {
            return new Tooltip(new ProfileTooltipView(title, description));
        }

        private static void AddProfileTraitSeparator(Container parent, ref int x, int y)
        {
            const string separator = " | ";
            var width = GetProfileTraitWidth(separator);

            new Label
            {
                Text = separator,
                Font = GameService.Content.DefaultFont16,
                TextColor = new Color(150, 150, 150),
                WrapText = false,
                Location = new Point(x, y),
                Size = new Point(width, 25),
                Parent = parent
            };

            x += width;
        }

        private static int GetProfileTraitWidth(string text)
        {
            return (int)Math.Ceiling(
                GameService.Content.DefaultFont16.MeasureString(text ?? string.Empty).Width) + 2;
        }

        private static string SelectedPreferences(ProfilePreferenceFlags flags, string separator)
        {
            return string.Join(
                separator,
                ProfileLabels.PreferenceOptions
                    .Where(option => (flags & option.Key) == option.Key)
                    .Select(option => option.Value));
        }

        private static string SelectedThemes(ProfileThemeFlags flags, string separator)
        {
            return string.Join(
                separator,
                ProfileLabels.ThemeOptions
                    .Where(option => (flags & option.Key) == option.Key)
                    .Select(option => option.Value));
        }

        private static string SelectedStyles(ProfileStyleFlags flags, string separator)
        {
            return string.Join(
                separator,
                ProfileLabels.StyleOptions
                    .Where(option => (flags & option.Key) == option.Key)
                    .Select(option => option.Value));
        }

        private int AddWrappedLabel(Container parent, Layout layout, string text, int y, MonoGame.Extended.BitmapFonts.BitmapFont font, int lineHeight)
        {
            if (parent == null || layout == null)
                return y;

            foreach (var line in WrapTextLines(text, layout.TextWidth, font))
            {
                if (line.Length == 0)
                {
                    y += layout.ParagraphGap;
                    continue;
                }

                new Label
                {
                    Text = line,
                    Font = font,
                    TextColor = Color.White,
                    WrapText = false,
                    Location = new Point(layout.TextX, y),
                    Size = new Point(layout.TextLabelWidth, lineHeight),
                    Parent = parent
                };

                y += lineHeight;
            }

            return y + layout.ParagraphGap;
        }

        private IEnumerable<AtAGlanceEntry> GetGlanceEntries()
        {
            return (_profile.AtAGlance ?? new List<AtAGlanceEntry>())
                .Where(entry => entry != null && entry.AssetId > 0)
                .Take(ProfileLimits.MaxAtAGlanceEntries);
        }

        private static Tooltip MakeGlanceTooltip(AtAGlanceEntry entry)
        {
            if (entry == null
                || (string.IsNullOrWhiteSpace(entry.Title)
                    && string.IsNullOrWhiteSpace(entry.Description)))
                return null;

            return new Tooltip(new ProfileTooltipView(entry.Title, entry.Description, "At A Glance"));
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

            reasonBox.EnterPressed += (s, e) => _ = SubmitReportSafelyAsync(reasonBox.Text, popupStatus);
        }

        private async Task SubmitReportAsync(string reason, Label popupStatus)
        {
            reason = reason?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(reason))
            {
                popupStatus.Text = "Please add a reason for reporting this character.";
                return;
            }

            if (_isSubmittingReport)
            {
                if (popupStatus != null)
                    popupStatus.Text = "Report is already being submitted.";
                return;
            }

            _isSubmittingReport = true;

            try
            {
                if (popupStatus != null)
                    popupStatus.Text = "Submitting report...";

                var message = _reportProfile == null
                    ? "Report failed."
                    : await _reportProfile(_profile, _presence, reason);

                SparkUiThread.Queue(() =>
                {
                    if (_contentPanel == null)
                        return;

                    SetStatusText(message);
                    CloseReportPanel();
                });
            }
            finally
            {
                _isSubmittingReport = false;
            }
        }

        // Forgot to fix this earlier when adding in report button functionality. Enter bypassed failures previously.
        private async Task SubmitReportSafelyAsync(string reason, Label popupStatus)
        {
            try
            {
                await SubmitReportAsync(reason, popupStatus);
            }
            catch (OperationCanceledException)
            {
                // Closing this while in flight is fine.
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Spark report failed");
                SparkUiThread.Queue(() =>
                {
                    if (_contentPanel != null && popupStatus != null)
                        popupStatus.Text = "Report failed. Please try again.";
                });
            }
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
            return !string.IsNullOrWhiteSpace(TextUtil.FirstNonEmpty(_presence?.AccountName, _profile?.AccountName))
                && !string.IsNullOrWhiteSpace(TextUtil.FirstNonEmpty(_presence?.OfficialCharacterName, _profile?.CharacterName))
                && !string.IsNullOrWhiteSpace(TextUtil.FirstNonEmpty(_presence?.ActiveProfileId, _profile?.ProfileId));
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
            var outOfCharacterInfo = _profile.UseGlobalOutOfCharacterInfo
                ? _presence?.OutOfCharacterInfo
                : _profile.OutOfCharacterInfo;

            return string.IsNullOrWhiteSpace(outOfCharacterInfo)
                ? string.Empty
                : outOfCharacterInfo.Trim();
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

            string statusText;

            try
            {
                await CopyTextAsync(accountName.Trim());
                statusText = $"Copied {accountName.Trim()}.";
            }
            catch
            {
                statusText = "Couldn't copy the account name right now.";
            }

            SparkUiThread.Queue(() => SetStatusText(statusText));
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
            private const int StatusWidth = 220;
            private const int TextXValue = 1;
            private const int TextRightPadding = 24;

            public const int HeaderCharacterLimit = 28;
            public const int WrappedHeaderOffset = 24;
            public const int ProfileTraitsOffset = 28;
            public const int StatusGap = 5;

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
                Math.Max(0, ViewportWidth - StatusWidth),
                IconY + 10 + _headerOffset,
                Math.Min(StatusWidth, ViewportWidth),
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

            public static int ProfileTraitsY(bool wrappedHeader)
            {
                return wrappedHeader ? 118 : 94;
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
