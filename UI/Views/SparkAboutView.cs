using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Graphics.UI;
using Microsoft.Xna.Framework;
using MonoGame.Extended.BitmapFonts;

namespace rp.spark.UI.Views
{
    internal class SparkAboutView : View
    {
        private const int ContentPadding = 12;

        protected override void Build(Container buildPanel)
        {
            var contentStack = new FlowPanel
            {
                Parent = buildPanel,
                Size = buildPanel.ContentRegion.Size,
                CanScroll = true,
                FlowDirection = ControlFlowDirection.SingleTopToBottom,
                ControlPadding = new Vector2(0, 10),
                OuterControlPadding = new Vector2(ContentPadding, ContentPadding)
            };

            AddSection(
                contentStack,
                "What is SPARK?",
                true,
                "SPARK is built for roleplay and discovering potential RP partners. It lets you create character profiles, share profiles, and view profiles from other SPARK users. The name stands for 'Simple Profile and Roleplay Kit', and comes from the precursor of my first legendary, Incinerator.");

            AddSection(
                contentStack,
                "SPARK Content Guidelines / Policies",
                false,
                "SPARK offers a report function and a block function to help curate your experience when using the tool.",
                "By using SPARK, you agree to the following:",
                " ",
                "1. No hate speech or bigotry",
                "Profiles may not include racist, sexist, homophobic, transphobic, ableist, or otherwise discriminatory content.",
                " ",
                "2. No harassment or targeted abuse",
                "Do not make profiles that insults, shames, threatens, stalks, or attempts to organize/coordinate harassment against another player.",
                " ",
                "3. NSFW Profiles must be marked as Mature/18+",
                "Profiles should be set to Mature/18+ if it contains explicit content not suitable for minors. Please report any profiles that are not properly set for moderation/review.",
                "SPARK may permanently mark all of the character profiles on your account as 18+ for breaking this rule.",
                " ",
                "4. No abusive sexual content",
                "Sexual content involving minors (real or fictional underage characters) is strictly prohibited.",
                " ",
                "SPARK may permanently block access to this service to any GW2 account that breaks these rules.");

            AddSection(
                contentStack,
                "Profile sharing",
                false,
                "When 'Share my profile' is enabled, SPARK publishes your active profile and information to the SPARK service. If your online status is invisible, this will no longer publish.");

            AddSection(
                contentStack,
                "Privacy",
                false,
                "The 'Invisible' status removes your info from the online user list. Hide my location sets your location to 'Hidden' for privacy.",
                "If you block someone, all profiles on that GW2 account get filtered. Blocked users can only see the last profile they ever saw from you, if any. They won't ever receive your profile updates or information.");

            AddSection(
                contentStack,
                "Saved Data",
                false,
                "Your profiles and any viewed profiles get saved locally to your PC. When your profile syncs to the server, it only transmits the information in the profile, plus your RP status, account name, character name, and location (if not hidden).",
                "Your profile data is removed from the SPARK server after 24 hours of being offline. The webserver does not retain user data, except reported profiles for moderation purposes. Your information only used to transmit profiles to other players.",
                "SPARK does NOT use analytics, tracking, and will never use your data for machine learning or AI.");

            AddSection(
                contentStack,
                "Questions / Feedback",
                false,
                "Any feedback can be sent to Bat.8570 in-game, or emailed to taw@a-bat.com.");
        }
        private static void AddParagraph(FlowPanel parent, string text, Color? textColor = null, BitmapFont font = null)
        {
            new Label
            {
                Text = text ?? string.Empty,
                Width = GetTextWidth(parent),
                Font = font ?? GameService.Content.DefaultFont14,
                TextColor = textColor ?? SparkViewUI.SecondaryTextColor,
                WrapText = true,
                AutoSizeHeight = true,
                Parent = parent
            };
        }

        private static bool IsRuleHeading(string text)
        {
            return !string.IsNullOrWhiteSpace(text)
                && text.Length >= 3
                && char.IsDigit(text[0])
                && text[1] == '.'
                && text[2] == ' ';
        }

        private static int GetTextWidth(FlowPanel parent)
        {
            return parent.ContentRegion.Width - ContentPadding * 2;
        }

        private static void AddSection(FlowPanel parent, string title, bool expandedByDefault, params string[] paragraphs)
        {
            var section = new FlowPanel
            {
                Parent = parent,
                Width = GetTextWidth(parent),
                HeightSizingMode = SizingMode.AutoSize,
                AutoSizePadding = new Point(0, 10),
                FlowDirection = ControlFlowDirection.SingleTopToBottom,
                ControlPadding = new Vector2(0, 8),
                OuterControlPadding = new Vector2(8, 10),
                ShowBorder = true,
                CanCollapse = true,
                Title = title ?? string.Empty
            };

            // Adding colour and sizing for the rules to be fancy
            foreach (var paragraph in paragraphs)
            {
                var isRuleHeading = IsRuleHeading(paragraph);

                AddParagraph(
                    section,
                    paragraph,
                    isRuleHeading
                        ? new Color(255, 190, 0)
                        : SparkViewUI.SecondaryTextColor,
                    isRuleHeading
                        ? GameService.Content.DefaultFont16
                        : GameService.Content.DefaultFont14);
            }

            if (expandedByDefault)
                section.Expand();
            else
                section.Collapse();
        }
    }
}
