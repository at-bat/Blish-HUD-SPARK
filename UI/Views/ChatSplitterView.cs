using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Graphics.UI;
using Microsoft.Xna.Framework;
using rp.spark.Services;
using rp.spark.UI.Controls;
using System;
using System.Collections.Generic;
using System.Linq;

namespace rp.spark.UI.Views
{
    internal sealed class ChatSplitterView : View
    {
        private const int ContentPadding = 12;
        private const int SectionPadding = 12;
        private const int CardPadding = 10;

        private const int EditorTop = 58;
        private const int EditorHeight = 236;
        private const int EditorInputHeight = 140;

        private const int ResultsHeadingTop = 310;
        private const int ResultsTop = 342;

        private const int GenerateButtonWidth = 100;
        private const int ClearButtonWidth = 75;
        private const int ControlGap = 8;
        private const int ControlHeight = 32;

        private readonly ChatSplitterSession _session;

        private SparkMultiline _responseInput;
        private FlowPanel _resultsShell;
        private Label _status;
        private int _resultsContentWidth;

        public ChatSplitterView(
            ChatSplitterSession session)
        {
            _session = session
                ?? throw new ArgumentNullException(nameof(session));
        }

        protected override void Build(Container buildPanel)
        {
            var contentSize = buildPanel.ContentRegion.Size;
            var contentWidth = Math.Max(
                0,
                contentSize.X - ContentPadding * 2);

            var sectionContentWidth = Math.Max(
                0,
                contentWidth - SectionPadding * 2 - 12);

            BuildHeader(buildPanel, contentWidth);

            BuildEditor(
                buildPanel,
                contentWidth,
                sectionContentWidth);

            BuildResults(
                buildPanel,
                contentSize,
                contentWidth,
                sectionContentWidth);

            RenderEmptyState(
                "Enter a response above and select 'Split Message'.");

            SetStatus("Ready.");
        }

        private static void BuildHeader(
            Container parent,
            int contentWidth)
        {
            new Label
            {
                Text = "Write a response and turn it into individually copyable messages that fit into GW2's chat box.",
                Location = new Point(ContentPadding, 16),
                Size = new Point(contentWidth, 38),
                Font = GameService.Content.DefaultFont14,
                TextColor = SparkViewUI.SecondaryTextColor,
                WrapText = true,
                Parent = parent
            };
        }

        private void BuildEditor(
            Container parent,
            int contentWidth,
            int sectionContentWidth)
        {
            var editorShell = new FlowPanel
            {
                Parent = parent,
                Location = new Point(ContentPadding, EditorTop),
                Size = new Point(contentWidth, EditorHeight),
                FlowDirection = ControlFlowDirection.SingleTopToBottom,
                ControlPadding = new Vector2(0, 6),
                OuterControlPadding = new Vector2(
                    SectionPadding,
                    SectionPadding),
                ShowBorder = true
            };

            new Label
            {
                Text = "Response Editor",
                Width = sectionContentWidth,
                Height = 22,
                Font = GameService.Content.DefaultFont16,
                TextColor = Color.White,
                StrokeText = true,
                Parent = editorShell
            };

            _responseInput = SparkFormLayout.AddMultilineTextBox(
                editorShell,
                _session.SourceText,
                "Write or paste your RP response here...",
                sectionContentWidth,
                EditorInputHeight);

            _responseInput.TextChanged += (s, e) =>
            {
                _session.SourceText =
                    _responseInput.Text;
            };

            var controls = SparkFormLayout.AddRow(
                editorShell,
                sectionContentWidth,
                ControlHeight,
                ControlGap);

            var generateButton = SparkFormLayout.AddButton(
                controls,
                "Split Message",
                GenerateButtonWidth,
                ControlHeight);

            var clearButton = SparkFormLayout.AddButton(
                controls,
                "Clear",
                ClearButtonWidth,
                ControlHeight);

            var statusWidth = Math.Max(
                0,
                sectionContentWidth
                - GenerateButtonWidth
                - ClearButtonWidth
                - ControlGap * 2);

            _status = SparkFormLayout.AddLabel(
                controls,
                string.Empty,
                statusWidth,
                ControlHeight,
                GameService.Content.DefaultFont14,
                SparkViewUI.SecondaryTextColor);

            generateButton.Click += (s, e) =>
                GenerateChunks();

            clearButton.Click += (s, e) =>
                ClearEditor();
        }

        private void BuildResults(
            Container parent,
            Point contentSize,
            int contentWidth,
            int sectionContentWidth)
        {
            new Label
            {
                Text = "Split Messages",
                Location = new Point(
                    ContentPadding,
                    ResultsHeadingTop),
                Size = new Point(contentWidth, 28),
                Font = GameService.Content.DefaultFont16,
                TextColor = Color.White,
                StrokeText = true,
                Parent = parent
            };

            var resultsHeight = Math.Max(
                0,
                contentSize.Y - ResultsTop - ContentPadding);

            _resultsContentWidth = sectionContentWidth;

            _resultsShell = new FlowPanel
            {
                Parent = parent,
                Location = new Point(
                    ContentPadding,
                    ResultsTop),
                Size = new Point(
                    contentWidth,
                    resultsHeight),
                CanScroll = true,
                FlowDirection =
                    ControlFlowDirection.SingleTopToBottom,
                ControlPadding = new Vector2(0, 8),
                OuterControlPadding = new Vector2(
                    SectionPadding,
                    SectionPadding),
                ShowBorder = true
            };
        }

        private void GenerateChunks()
        {
            _session.SourceText =
                _responseInput?.Text ?? string.Empty;

            var chunks = ChatSplitter.Split(
                _session.SourceText);

            if (chunks.Count == 0)
            {
                _session.ClearGeneratedChunks();

                RenderEmptyState(
                    "No messages were generated.");

                SetStatus(
                    "Enter a response before generating.",
                    true);

                return;
            }

            _session.SetGeneratedChunks(chunks);
            RenderResults(_session.GeneratedChunks);

            SetStatus(
                chunks.Count == 1
                    ? "Generated 1 message."
                    : $"Generated {chunks.Count} messages.");
        }

        private void RenderResults(
            IReadOnlyList<string> chunks)
        {
            ClearResults();

            for (var index = 0; index < chunks.Count; index++)
            {
                AddChunkCard(
                    chunks[index],
                    index + 1,
                    chunks.Count);
            }
        }

        private void AddChunkCard(
            string chunk,
            int number,
            int total)
        {
            var cardWidth = _resultsContentWidth;
            var innerWidth = Math.Max(
                0,
                cardWidth - CardPadding * 2);

            var card = new FlowPanel
            {
                Parent = _resultsShell,
                Width = cardWidth,
                HeightSizingMode = SizingMode.AutoSize,
                AutoSizePadding = new Point(0, CardPadding),
                FlowDirection =
                    ControlFlowDirection.SingleTopToBottom,
                ControlPadding = new Vector2(0, 6),
                OuterControlPadding = new Vector2(
                    CardPadding,
                    CardPadding),
                ShowBorder = true
            };

            var header = SparkFormLayout.AddRow(
                card,
                innerWidth,
                24,
                0);

            var countWidth = 100;
            var titleWidth = Math.Max(
                0,
                innerWidth - countWidth);

            SparkFormLayout.AddLabel(
                header,
                $"Message {number} of {total}",
                titleWidth,
                24,
                GameService.Content.DefaultFont14,
                Color.White,
                true);

            var characterCount = SparkFormLayout.AddLabel(
                header,
                $"{chunk.Length}/{ChatSplitter.DefaultMaxLength}",
                countWidth,
                24,
                GameService.Content.DefaultFont14,
                SparkViewUI.SecondaryTextColor);

            characterCount.HorizontalAlignment =
                HorizontalAlignment.Right;

            var chunkLabel = SparkFormLayout.AddLabel(
                card,
                chunk,
                innerWidth,
                25,
                GameService.Content.DefaultFont14,
                SparkViewUI.SecondaryTextColor);

            chunkLabel.WrapText = true;
            chunkLabel.AutoSizeHeight = true;
        }

        private void RenderEmptyState(string message)
        {
            ClearResults();

            new Label
            {
                Text = message ?? string.Empty,
                Width = _resultsContentWidth,
                Height = 40,
                Font = GameService.Content.DefaultFont14,
                TextColor = SparkViewUI.SecondaryTextColor,
                WrapText = true,
                Parent = _resultsShell
            };
        }

        private void ClearEditor()
        {
            _session.Clear();

            if (_responseInput != null)
                _responseInput.Text = string.Empty;

            RenderEmptyState(
                "Enter a response above and select Generate.");

            SetStatus("Cleared.");
        }

        private void ClearResults()
        {
            if (_resultsShell == null)
                return;

            foreach (var child in
                     _resultsShell.Children.ToArray())
            {
                child.Dispose();
            }

            _resultsShell.VerticalScrollOffset = 0;
        }

        private void SetStatus(
            string text,
            bool warning = false)
        {
            if (_status == null)
                return;

            _status.Text = text ?? string.Empty;
            _status.TextColor = warning
                ? SparkViewUI.WarningTextColor
                : SparkViewUI.SecondaryTextColor;
        }
    }
}