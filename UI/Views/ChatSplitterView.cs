using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Graphics.UI;
using Microsoft.Xna.Framework;
using rp.spark.Services;
using rp.spark.UI.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace rp.spark.UI.Views
{
    internal sealed class ChatSplitterView : View
    {
        private const int ContentPadding = 12;
        private const int SectionPadding = 12;
        private const int CardPadding = 8;

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
        private readonly ChatSplitterSettings _settings;
        private readonly List<StandardButton> _copyButtons = new List<StandardButton>();

        private SparkMultiline _responseInput;
        private FlowPanel _resultsShell;
        private Label _status;
        private int _resultsContentWidth;

        public ChatSplitterView(ChatSplitterSession session, ChatSplitterSettings settings)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));

            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        protected override void Build(Container buildPanel)
        {
            var contentSize = buildPanel.ContentRegion.Size;
            var contentWidth = Math.Max(0, contentSize.X - ContentPadding * 2);

            var sectionContentWidth = Math.Max(0, contentWidth - SectionPadding * 2 - 12);

            BuildHeader(buildPanel, contentWidth);

            BuildEditor(buildPanel, contentWidth, sectionContentWidth);

            BuildResults(buildPanel, contentSize, contentWidth, sectionContentWidth);

            if (_session.GeneratedChunks.Count > 0)
            {
                RenderResults(_session.GeneratedChunks);

                SetStatus(_session.GeneratedChunks.Count == 1
                    ? "Generated 1 message."
                    : $"Generated {_session.GeneratedChunks.Count} messages.");
            }
            else
            {
                RenderEmptyState("Enter a response above and select 'Split Message'.");
                SetStatus("Ready.");
            }
        }

        private static void BuildHeader(Container parent, int contentWidth)
        {
            new Label
            {
                Text = "Write a response and split it into GW2-sized messages. Write '/split' on its own line to force a new message early.",
                Location = new Point(ContentPadding, 16),
                Size = new Point(contentWidth, 38),
                Font = GameService.Content.DefaultFont14,
                TextColor = SparkViewUI.SecondaryTextColor,
                WrapText = true,
                Parent = parent
            };
        }

        private void BuildEditor(Container parent, int contentWidth, int sectionContentWidth)
        {
            var editorShell = new FlowPanel
            {
                Parent = parent,
                Location = new Point(ContentPadding, EditorTop),
                Size = new Point(contentWidth, EditorHeight),
                FlowDirection = ControlFlowDirection.SingleTopToBottom,
                ControlPadding = new Vector2(0, 6),
                OuterControlPadding = new Vector2(SectionPadding, SectionPadding),
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
                _session.SourceText = _responseInput.Text;
                ResetCopyFeedback();
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

            generateButton.Click += (s, e) => GenerateChunks();

            clearButton.Click += (s, e) => ClearEditor();
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
                Location = new Point(ContentPadding, ResultsHeadingTop),
                Size = new Point(contentWidth, 28),
                Font = GameService.Content.DefaultFont16,
                TextColor = Color.White,
                StrokeText = true,
                Parent = parent
            };

            var resultsHeight = Math.Max(0, contentSize.Y - ResultsTop - ContentPadding);

            _resultsContentWidth = sectionContentWidth;

            _resultsShell = new FlowPanel
            {
                Parent = parent,
                Location = new Point(ContentPadding, ResultsTop),
                Size = new Point(contentWidth, resultsHeight),
                CanScroll = true,
                FlowDirection = ControlFlowDirection.SingleTopToBottom,
                ControlPadding = new Vector2(0, 3),
                OuterControlPadding = new Vector2(SectionPadding, 6),
                ShowBorder = true
            };
        }

        private void GenerateChunks()
        {
            _session.SourceText = _responseInput?.Text ?? string.Empty;

            var options = new ChatSplitterOptions
            {
                BreakOnBlankLines = _settings.BreakOnBlankLines.Value,
                ShortenChatCommands = _settings.ShortenChatCommands.Value,
                RepeatChatCommand = _settings.RepeatChatCommand.Value,
                UseMarkers = _settings.UseMarkers.Value,
                EndMarker = _settings.EndMarker.Value,
                UseStartMarkers = _settings.UseStartMarkers.Value,
                StartMarker = _settings.StartMarker.Value
            };

            if (!ChatSplitter.TrySplit(
                _session.SourceText,
                options,
                out var chunks,
                out var error))
            {
                _session.ClearGeneratedChunks();
                RenderEmptyState(error);
                SetStatus("Check the response or splitter settings.", true);
                return;
            }

            if (chunks.Count == 0)
            {
                _session.ClearGeneratedChunks();
                RenderEmptyState("No messages were generated.");
                SetStatus("Enter a response before generating.", true);
                return;
            }

            _session.SetGeneratedChunks(chunks);
            RenderResults(_session.GeneratedChunks);

            SetStatus(chunks.Count == 1
                ? "Generated 1 message."
                : $"Generated {chunks.Count} messages.");
        }

        private void RenderResults(IReadOnlyList<string> chunks)
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

        private void AddChunkCard(string chunk, int number, int total)
        {
            const int headerHeight = 28;
            const int headerGap = 6;
            const int countWidth = 72;
            const int copyButtonWidth = 94;

            var cardWidth = _resultsContentWidth;
            var innerWidth = Math.Max(0, cardWidth - CardPadding * 2);

            var card = new FlowPanel
            {
                Parent = _resultsShell,
                Width = cardWidth,
                HeightSizingMode = SizingMode.AutoSize,
                AutoSizePadding = new Point(0, 6),
                FlowDirection = ControlFlowDirection.SingleTopToBottom,
                ControlPadding = new Vector2(0, 2),
                OuterControlPadding = new Vector2(CardPadding, 6),
                ShowBorder = false,
                BackgroundColor = number % 2 == 1
                    ? new Color(0, 0, 0, 65)
                    : new Color(20, 20, 20, 55)
            };

            var header = SparkFormLayout.AddRow(
                card,
                innerWidth,
                headerHeight,
                headerGap);

            var titleWidth = Math.Max(
                0,
                innerWidth -
                countWidth -
                copyButtonWidth -
                headerGap * 2);

            SparkFormLayout.AddLabel(
                header,
                $"Message {number} of {total}",
                titleWidth,
                headerHeight,
                GameService.Content.DefaultFont14,
                Color.White,
                true);

            var characterCount = SparkFormLayout.AddLabel(
                header,
                $"{chunk.Length}/{ChatSplitter.DefaultMaxLength}",
                countWidth,
                headerHeight,
                GameService.Content.DefaultFont14,
                SparkViewUI.SecondaryTextColor);

            characterCount.HorizontalAlignment = HorizontalAlignment.Right;

            var copyCount = _session.GetCopyCount(number - 1);

            var copyButton = SparkFormLayout.AddButton(
                header,
                copyCount > 0 ? $"Copied ({copyCount})" : "Copy",
                copyButtonWidth,
                26);

            _copyButtons.Add(copyButton);

            copyButton.Click += async (s, e) =>
                await CopyChunkAsync(copyButton, chunk, number - 1);

            var chunkLabel = SparkFormLayout.AddLabel(
                card,
                chunk,
                innerWidth,
                24,
                GameService.Content.DefaultFont14,
                SparkViewUI.SecondaryTextColor);

            chunkLabel.WrapText = true;
            chunkLabel.AutoSizeHeight = true;
        }

        private async Task CopyChunkAsync(
            StandardButton copyButton,
            string chunk,
            int chunkIndex)
        {
            bool copied;

            try
            {
                copied = await ClipboardUtil.WindowsClipboardService
                    .SetTextAsync(chunk ?? string.Empty);
            }
            catch
            {
                copied = false;
            }

            SparkUiThread.Queue(() =>
            {
                if (!copied)
                {
                    if (copyButton.Parent != null)
                        SetStatus("Couldn't copy that message right now.", true);

                    return;
                }

                var copyCount = _session.IncrementCopyCount(chunkIndex);

                if (copyButton.Parent == null)
                    return;

                copyButton.Text = $"Copied ({copyCount})";
                SetStatus($"Copied message {chunkIndex + 1}.");
            });
        }

        private void ResetCopyFeedback()
        {
            _session.ResetCopyCounts();

            foreach (var copyButton in _copyButtons)
            {
                if (copyButton.Parent != null)
                    copyButton.Text = "Copy";
            }
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

            RenderEmptyState("Enter a response above and select 'Split Message'.");
            SetStatus("Cleared.");
        }

        private void ClearResults()
        {
            if (_resultsShell == null)
                return;

            _copyButtons.Clear();

            foreach (var child in _resultsShell.Children.ToArray())
            {
                child.Dispose();
            }

            _resultsShell.VerticalScrollOffset = 0;
        }

        private void SetStatus(string text, bool warning = false)
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