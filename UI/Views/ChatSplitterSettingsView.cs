using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Graphics.UI;
using Microsoft.Xna.Framework;
using System;

namespace rp.spark.UI.Views
{
    internal sealed class ChatSplitterSettingsView : View
    {
        private const int ContentPadding = 12;
        private const int ScrollbarAllowance = 12;

        protected override void Build(Container buildPanel)
        {
            var contentWidth = Math.Max(
                0,
                buildPanel.ContentRegion.Width
                - ContentPadding * 2
                - ScrollbarAllowance);

            var contentStack = new FlowPanel
            {
                Parent = buildPanel,
                Size = buildPanel.ContentRegion.Size,
                CanScroll = true,
                FlowDirection =
                    ControlFlowDirection.SingleTopToBottom,
                ControlPadding = new Vector2(0, 10),
                OuterControlPadding = new Vector2(
                    ContentPadding,
                    ContentPadding)
            };

            new Label
            {
                Text = "Chat Splitter Settings",
                Width = contentWidth,
                Height = 30,
                Font = GameService.Content.DefaultFont18,
                TextColor = Color.White,
                StrokeText = true,
                Parent = contentStack
            };

            new Label
            {
                Text = "Placeholder",
                Width = contentWidth,
                Height = 48,
                Font = GameService.Content.DefaultFont14,
                TextColor = SparkViewUI.SecondaryTextColor,
                WrapText = true,
                Parent = contentStack
            };

            var placeholder = new FlowPanel
            {
                Parent = contentStack,
                Width = contentWidth,
                HeightSizingMode = SizingMode.AutoSize,
                AutoSizePadding = new Point(0, 12),
                FlowDirection =
                    ControlFlowDirection.SingleTopToBottom,
                ControlPadding = new Vector2(0, 8),
                OuterControlPadding = new Vector2(12, 12),
                ShowBorder = true
            };

            new Label
            {
                Text = "Settings will be available here.",
                Width = Math.Max(0, contentWidth - 24),
                Height = 28,
                Font = GameService.Content.DefaultFont16,
                TextColor = Color.White,
                Parent = placeholder
            };

            new Label
            {
                Text = "Placeholder.",
                Width = Math.Max(0, contentWidth - 24),
                Height = 48,
                Font = GameService.Content.DefaultFont14,
                TextColor = SparkViewUI.SecondaryTextColor,
                WrapText = true,
                Parent = placeholder
            };
        }
    }
}