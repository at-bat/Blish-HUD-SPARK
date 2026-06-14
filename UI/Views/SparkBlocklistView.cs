using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Graphics.UI;
using rp.spark.Services;
using System;
using Microsoft.Xna.Framework;

namespace rp.spark.UI.Views
{
    internal sealed class SparkBlocklistView : View
    {
        private const int ContentWidth = 760;
        private const int ContentHeight = 610;
        private const int ListWidth = 390;
        private const int InfoWidth = 330;
        private const int ListHeight = 525;

        private readonly SparkBlocklist _blocklist;

        public SparkBlocklistView(
            SparkSettings settings,
            Func<string, string> blockAccount,
            Func<string, string> unblockAccount,
            Action<Action> watchBlockedAccountsChanged,
            Action<Action> unwatchBlockedAccountsChanged)
        {
            _blocklist = new SparkBlocklist(
                settings,
                blockAccount,
                unblockAccount,
                watchBlockedAccountsChanged,
                unwatchBlockedAccountsChanged);
        }

        protected override void Build(Container buildPanel)
        {
            var row = new FlowPanel
            {
                Parent = buildPanel,
                Width = ContentWidth,
                Height = ContentHeight,
                FlowDirection = ControlFlowDirection.SingleLeftToRight,
                ControlPadding = new Vector2(20, 0)
            };

            var blockStack = SparkFormLayout.AddVerticalStack(
                row,
                0,
                0,
                ListWidth,
                ContentHeight,
                8);

            _blocklist.Build(blockStack, ListWidth, ListHeight);

            BuildExplanation(row);
        }

        private void BuildExplanation(Container parent)
        {
            var infoStack = SparkFormLayout.AddVerticalStack(
                parent,
                0,
                0,
                InfoWidth,
                ContentHeight,
                10);

            SparkFormLayout.AddLabel(
                infoStack,
                "How blocking works:",
                InfoWidth,
                28,
                GameService.Content.DefaultFont16,
                Color.White,
                true);

            AddInfoParagraph(
                infoStack,
                "Blocks are done locally first, then sent to SPARK when you are online. This lets the server prevent blocked accounts from seeing your profiles, online status, and location. You also will not see them in SPARK.");

            AddInfoParagraph(
                infoStack,
                "Someone you blocked might have a copy of your profile in bookmarks or in recently viewed. Once blocked, they will not receive a new copy of your profile anymore. SPARK does this on purpose so someone cannot tell that they have been blocked.");

            AddInfoParagraph(
                infoStack,
                "If someone's profile breaks any of SPARK's content policies, please report them for moderation. Users who misuse SPARK will be banned from accessing the service.");
        }

        private static void AddInfoParagraph(Container parent, string text)
        {
            var label = SparkFormLayout.AddLabel(
                parent,
                text,
                InfoWidth,
                95,
                GameService.Content.DefaultFont14,
                SparkViewUI.SecondaryTextColor);

            label.WrapText = true;
        }

        protected override void Unload()
        {
            _blocklist.Dispose();
        }
    }
}