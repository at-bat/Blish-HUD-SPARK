using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Graphics.UI;
using Microsoft.Xna.Framework;
using rp.spark.Services;
using System;

namespace rp.spark.UI.Views
{
    internal sealed class ChatSplitterSettingsView : View
    {
        private const int ContentPadding = 12;
        private const int SectionPadding = 12;
        private const int ScrollbarAllowance = 12;

        private readonly ChatSplitterSettings _settings;

        public ChatSplitterSettingsView(ChatSplitterSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        protected override void Build(Container buildPanel)
        {
            var contentWidth = Math.Max(0, buildPanel.ContentRegion.Width- ContentPadding * 2 - ScrollbarAllowance);

            var contentStack = new FlowPanel
            {
                Parent = buildPanel,
                Size = buildPanel.ContentRegion.Size,
                CanScroll = true,
                FlowDirection = ControlFlowDirection.SingleTopToBottom,
                ControlPadding = new Vector2(0, 10),
                OuterControlPadding = new Vector2(ContentPadding, ContentPadding)
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
                Text = "These settings are saved and apply the next time you split a response.",
                Width = contentWidth,
                Height = 38,
                Font = GameService.Content.DefaultFont14,
                TextColor = SparkViewUI.SecondaryTextColor,
                WrapText = true,
                Parent = contentStack
            };

            BuildMessageBreakSettings(contentStack, contentWidth);
        }

        private void BuildMessageBreakSettings(FlowPanel parent, int contentWidth)
        {
            var innerWidth = Math.Max(0, contentWidth - SectionPadding * 2);

            var section = new FlowPanel
            {
                Parent = parent,
                Width = contentWidth,
                HeightSizingMode = SizingMode.AutoSize,
                AutoSizePadding = new Point(0, 12),
                FlowDirection = ControlFlowDirection.SingleTopToBottom,
                ControlPadding = new Vector2(0, 8),
                OuterControlPadding = new Vector2(SectionPadding, SectionPadding),
                ShowBorder = true
            };

            new Label
            {
                Text = "Message Breaks",
                Width = innerWidth,
                Height = 28,
                Font = GameService.Content.DefaultFont16,
                TextColor = Color.White,
                StrokeText = true,
                Parent = section
            };

            var blankLinesCheckbox =
                SparkFormLayout.AddCheckbox(
                    section,
                    "Blank lines start new messages",
                    _settings.BreakOnBlankLines.Value,
                    innerWidth,
                    30);

            blankLinesCheckbox.BasicTooltipText = "When disabled, blank lines are treated as spaces instead of splitting the message forcibly.";

            blankLinesCheckbox.CheckedChanged += (s, e) =>
            {
                if (_settings.BreakOnBlankLines.Value == blankLinesCheckbox.Checked)
                    return;

                _settings.BreakOnBlankLines.Value = blankLinesCheckbox.Checked;
            };
        }
    }
}