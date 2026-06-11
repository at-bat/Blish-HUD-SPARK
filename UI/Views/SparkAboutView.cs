using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Graphics.UI;
using Microsoft.Xna.Framework;
using MonoGame.Extended.BitmapFonts;

namespace rp.spark.UI.Views
{
    internal class SparkAboutView : View
    {
        private const int ContentPadding = 20;

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

            AddHeading(
                contentStack,
                "What is SPARK?",
                GameService.Content.DefaultFont18);

            AddParagraph(
                contentStack,
                "SPARK is built for roleplay and discovering potential RP partners. It lets you create character profiles, share profiles, and view profiles from other SPARK users. The name stands for 'Simple Profile and Roleplay Kit', and comes from the precursor of my first legendary, Incinerator.");

            AddHeading(contentStack, "Profile sharing");
            AddParagraph(
                contentStack,
                "When 'Share my profile online' is enabled, SPARK publishes your profile and online presence to the SPARK service, assuming you are on a character with an active profile and your status is not set to Invisible.");

            AddHeading(contentStack, "Privacy");
            AddParagraph(
                contentStack,
                "The 'Invisible' status removes your online presence from the online user list. Hide my location sets your location to 'Hidden' for privacy. If you block someone, all profiles on that GW2 account get filtered. Blocked users can only see the last profile they ever saw from you (if any). They won't ever receive your profile updates or information.");

            AddHeading(contentStack, "Saved Data");
            AddParagraph(
                contentStack,
                "Your profiles and viewed profiles get saved locally to your PC. When your profile syncs to the server, it only transmits the information in the profile, plus your status message (if set), account name, character name, and location (if not hidden).");
            AddParagraph(
                contentStack,
                "Your profile data is removed from the SPARK server after 24 hours of being offline. The webserver does not retain user data, nor does SPARK do any tracking of any kind. The information stored is temporary and only used to transmit profiles from one another.");
            AddParagraph(
                contentStack,
                "SPARK can and will permanently block access to GW2 accounts who misuse the service or make profiles/statuses that promote hate or bigotry.");

            AddHeading(contentStack, "Questions / Feedback");
            AddParagraph(
                contentStack,
                "Any feedback can be sent to Bat.8570 in-game, or to taw@a-bat.com. Online profiles can be reported from the profile viewer, which sends a short reason and snapshots the profile on the SPARK server for review.");
        }

        private static void AddHeading(FlowPanel parent, string text)
        {
            AddHeading(
                parent,
                text,
                GameService.Content.DefaultFont16);
        }

        private static void AddHeading(FlowPanel parent, string text, BitmapFont font)
        {
            new Label
            {
                Text = text ?? string.Empty,
                Width = GetTextWidth(parent),
                Font = font,
                TextColor = Color.White,
                StrokeText = true,
                AutoSizeHeight = true,
                Parent = parent
            };
        }

        private static void AddParagraph(FlowPanel parent, string text)
        {
            new Label
            {
                Text = text ?? string.Empty,
                Width = GetTextWidth(parent),
                Font = GameService.Content.DefaultFont14,
                TextColor = SparkViewUI.SecondaryTextColor,
                WrapText = true,
                AutoSizeHeight = true,
                Parent = parent
            };
        }

        private static int GetTextWidth(FlowPanel parent)
        {
            return parent.ContentRegion.Width - ContentPadding * 2;
        }
    }
}
