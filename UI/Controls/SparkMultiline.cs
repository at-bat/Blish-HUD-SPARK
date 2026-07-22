using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using System;
using System.Collections.Generic;
using System.Text;

namespace rp.spark.UI.Controls
{
    // SPARK version of a wrapped multiline textbox based on PR #984
    // https://github.com/blish-hud/Blish-HUD/pull/984
    // Text remains unchanged when stored, only changed for rendering in the UI
    // Cursor/selection positions are translated between text and display indices.
    internal class SparkMultiline : MultilineTextBox
    {
        private const int TextTopPadding = 7;
        private const int TextLeftPadding = 10;
        private const int ScrollbarWidth = 4;
        private const int ScrollbarInset = 3;
        private const int MinThumbHeight = 18;
        private const int WheelStepPixels = 42;
        private const int WrapPadding = 6;
        private const char NewLine = '\n';

        private string _displayText = string.Empty;
        private string[] _displayLines = new[] { string.Empty };
        private int[] _displayNewLineIndices = Array.Empty<int>();
        private Rectangle _textRegion = Rectangle.Empty;
        private Rectangle[] _highlightRegions = Array.Empty<Rectangle>();
        private Rectangle _cursorRegion = Rectangle.Empty;
        private int _verticalScrollOffset;
        private int _maxVerticalScrollOffset;
        private bool _isManualScroll;
        private int _manualScrollCaretIndex;
        private Container _wheelSource;

        // Attaching to the parent lets this scroll before the textbox is focused/clicked.
        public void AttachWheelSource(Container wheelSource)
        {
            if (_wheelSource == wheelSource)
                return;

            if (_wheelSource != null)
                _wheelSource.MouseWheelScrolled -= HandleWheelScrolled;

            _wheelSource = wheelSource;

            if (_wheelSource != null)
                _wheelSource.MouseWheelScrolled += HandleWheelScrolled;
        }

        private void HandleWheelScrolled(object sender, MouseEventArgs e)
        {
            TryScrollWheel();
        }

        public SparkMultiline()
        {
            TextChanged += (s, e) => RecalculateLayout();
        }

        protected override void DisposeControl()
        {
            AttachWheelSource(null);
            base.DisposeControl();
        }

        protected override CaptureType CapturesInput()
        {
            return CaptureType.Mouse | CaptureType.MouseWheel;
        }

        protected override void MoveLine(int delta)
        {
            var newDisplayIndex = 0;
            var lines = _displayLines ?? new[] { string.Empty };
            var (line, character) = GetSplitIndex(_cursorIndex);
            var targetLine = line + delta;

            if (targetLine >= lines.Length)
            {
                newDisplayIndex = _displayText.Length;
            }
            else if (targetLine >= 0)
            {
                var sourceLine = lines[MathHelper.Clamp(line, 0, lines.Length - 1)];
                var targetLineText = lines[targetLine];
                var cursorCharacter = MathHelper.Clamp(character, 0, sourceLine.Length);
                var cursorLeft = MeasureStringWidth(sourceLine.Substring(0, cursorCharacter));

                newDisplayIndex = GetLineStartIndex(targetLine) + GetCharacterFromX(targetLineText, cursorLeft);
            }

            UserSetCursorIndex(GetCursorIndexFromDisplayIndex(newDisplayIndex));
            UpdateSelectionIfShiftDown();
        }

        public override int GetCursorIndexFromPosition(int x, int y)
        {
            x -= TextLeftPadding;
            y -= TextTopPadding;
            y += _verticalScrollOffset;

            var lines = _displayLines ?? new[] { string.Empty };
            var clickedLine = Math.Max(0, y / Math.Max(1, _font.LineHeight));

            if (clickedLine > lines.Length - 1)
                return _text.Length;

            var displayIndex = GetLineStartIndex(clickedLine) + GetCharacterFromX(lines[clickedLine], x);

            return GetCursorIndexFromDisplayIndex(displayIndex);
        }

        public override void RecalculateLayout()
        {
            // Making it always consider the scrollbar so the text doesn't rewrap between two different widths while Blish is drawing
            // PaintDisplayText still keeps its own line array in case recalculation happens while Blish is drawing.
            _textRegion = CalculateTextRegion(true);
            RebuildDisplayLayout();

            SetVerticalScrollOffset(_verticalScrollOffset, false);
            UpdateScrolling();

            _highlightRegions = CalculateHighlightRegions();
            _cursorRegion = CalculateCursorRegion();
        }

        protected override void OnMouseWheelScrolled(MouseEventArgs e)
        {
            TryScrollWheel();
            base.OnMouseWheelScrolled(e);
        }

        private bool TryScrollWheel()
        {
            if (_maxVerticalScrollOffset <= 0 || !Visible || !AbsoluteBounds.Contains(GameService.Input.Mouse.Position))
                return false;

            var scrollValue = GameService.Input.Mouse.State.ScrollWheelValue;

            if (scrollValue == 0)
                return false;

            return ScrollBy(scrollValue > 0 ? -WheelStepPixels : WheelStepPixels);
        }

        // Caret movement keeps the caret visible, but manually scrolling can move it off screen.
        protected override void UpdateScrolling()
        {
            if (_textRegion == Rectangle.Empty || _displayLines.Length == 0)
                return;

            if (_isManualScroll && _cursorIndex == _manualScrollCaretIndex)
                return;

            _isManualScroll = false;

            var cursorTop = GetSplitIndex(_cursorIndex).Line * _font.LineHeight;
            var cursorBottom = cursorTop + _font.LineHeight;

            if (cursorTop < _verticalScrollOffset)
            {
                SetVerticalScrollOffset(cursorTop, false);
            }
            else if (cursorBottom > _verticalScrollOffset + _textRegion.Height)
            {
                SetVerticalScrollOffset(cursorBottom - _textRegion.Height, false);
            }
        }

        protected override void Paint(SpriteBatch spriteBatch, Rectangle bounds)
        {
            PaintBackground(spriteBatch, bounds);
            PaintDisplayText(spriteBatch);

            if (_highlightRegions.Length > 0)
            {
                foreach (var highlightRegion in _highlightRegions)
                    PaintHighlight(spriteBatch, highlightRegion);
            }
            else
            {
                PaintCursor(spriteBatch, _cursorRegion);
            }

            PaintScrollbar(spriteBatch);
        }

        private void RebuildDisplayLayout()
        {
            _displayText = ProcessDisplayText(_text);
            _displayLines = _displayText.Split(NewLine);
            var contentHeight = Math.Max(_font.LineHeight, _displayLines.Length * _font.LineHeight);
            _maxVerticalScrollOffset = Math.Max(0, contentHeight - _textRegion.Height);
        }

        private string ProcessDisplayText(string value)
        {
            if (_textRegion.Width <= 0)
            {
                _displayNewLineIndices = Array.Empty<int>();
                return value ?? string.Empty;
            }

            var wrapWidth = Math.Max(1, _textRegion.Width - WrapPadding);
            return WrapText(_font, value ?? string.Empty, wrapWidth, out _displayNewLineIndices);
        }

        private int GetCursorIndexFromDisplayIndex(int displayIndex)
        {
            var cursorIndex = MathHelper.Clamp(displayIndex, 0, _displayText.Length);

            foreach (var displayNewLineIndex in _displayNewLineIndices)
            {
                if (displayNewLineIndex > displayIndex)
                    break;

                cursorIndex--;
            }

            return MathHelper.Clamp(cursorIndex, 0, _text.Length);
        }

        private int GetDisplayIndexFromCursorIndex(int cursorIndex)
        {
            var displayIndex = MathHelper.Clamp(cursorIndex, 0, _text.Length);

            foreach (var displayNewLineIndex in _displayNewLineIndices)
            {
                if (displayNewLineIndex > displayIndex)
                    break;

                displayIndex++;
            }

            return MathHelper.Clamp(displayIndex, 0, _displayText.Length);
        }

        private (int Line, int Character) GetSplitIndex(int index)
        {
            var displayIndex = GetDisplayIndexFromCursorIndex(index);
            var lineIndex = 0;
            var charIndex = 0;

            for (var i = 0; i < displayIndex && i < _displayText.Length; i++)
            {
                if (_displayText[i] == NewLine)
                {
                    lineIndex++;
                    charIndex = 0;
                    continue;
                }

                charIndex++;

                if (i < _displayText.Length - 1 && char.IsSurrogatePair(_displayText, i))
                {
                    i++;
                    charIndex++;
                }
            }

            return (MathHelper.Clamp(lineIndex, 0, Math.Max(0, _displayLines.Length - 1)), charIndex);
        }

        private Rectangle[] CalculateHighlightRegions()
        {
            var selectionStart = Math.Min(_selectionStart, _selectionEnd);
            var selectionLength = Math.Abs(_selectionStart - _selectionEnd);

            if (selectionLength <= 0 || selectionStart + selectionLength > _text.Length)
                return Array.Empty<Rectangle>();

            var lines = _displayLines ?? new[] { string.Empty };
            var (startLine, startChar) = GetSplitIndex(selectionStart);
            var (endLine, endChar) = GetSplitIndex(selectionStart + selectionLength);
            var lineSpanCount = endLine - startLine;
            var regions = new Rectangle[lineSpanCount + 1];

            if (lineSpanCount == 0)
            {
                var line = lines[startLine];
                var startCharacter = MathHelper.Clamp(startChar, 0, line.Length);
                var endCharacter = MathHelper.Clamp(endChar, startCharacter, line.Length);
                var highlightLeft = MeasureStringWidth(line.Substring(0, startCharacter));
                var highlightWidth = MeasureStringWidth(line.Substring(startCharacter, endCharacter - startCharacter)) + 1;

                regions[0] = new Rectangle(
                    _textRegion.Left + (int)highlightLeft,
                    GetLineTop(startLine),
                    (int)highlightWidth,
                    _font.LineHeight - 1);
            }
            else
            {
                var firstLine = lines[startLine];
                var firstCharacter = MathHelper.Clamp(startChar, 0, firstLine.Length);
                var firstLeft = MeasureStringWidth(firstLine.Substring(0, firstCharacter));
                var firstWidth = MeasureStringWidth(firstLine.Substring(firstCharacter)) + 1;

                regions[0] = new Rectangle(
                    _textRegion.Left + (int)firstLeft,
                    GetLineTop(startLine),
                    (int)firstWidth,
                    _font.LineHeight - 1);

                for (var i = startLine + 1; i < endLine; i++)
                {
                    var fullWidth = MeasureStringWidth(lines[i]) + 1;

                    regions[i - startLine] = new Rectangle(
                        _textRegion.Left,
                        GetLineTop(i),
                        (int)fullWidth,
                        _font.LineHeight - 1);
                }

                var lastLine = lines[endLine];
                var lastCharacter = MathHelper.Clamp(endChar, 0, lastLine.Length);
                var lastWidth = MeasureStringWidth(lastLine.Substring(0, lastCharacter)) + 1;

                regions[lineSpanCount] = new Rectangle(
                    _textRegion.Left,
                    GetLineTop(endLine),
                    (int)lastWidth,
                    _font.LineHeight - 1);
            }

            return regions;
        }

        private Rectangle CalculateCursorRegion()
        {
            var (cursorLine, cursorCharacter) = GetSplitIndex(_cursorIndex);
            var lines = _displayLines ?? new[] { string.Empty };
            var line = lines[MathHelper.Clamp(cursorLine, 0, lines.Length - 1)];
            var character = MathHelper.Clamp(cursorCharacter, 0, line.Length);
            var cursorLeft = MeasureStringWidth(line.Substring(0, character));

            return new Rectangle(
                _textRegion.X + (int)cursorLeft,
                GetLineTop(cursorLine) + 2,
                2,
                _font.LineHeight - 4);
        }

        private Rectangle CalculateTextRegion(bool reserveScrollbar)
        {
            var rightPadding = TextLeftPadding * 2 + (reserveScrollbar ? ScrollbarWidth + ScrollbarInset : 0);

            return new Rectangle(
                TextLeftPadding,
                TextTopPadding,
                _size.X - rightPadding,
                _size.Y - TextTopPadding * 2);
        }

        private int GetCharacterFromX(string line, float x)
        {
            line = line ?? string.Empty;

            if (x <= 0 || line.Length == 0)
                return 0;

            var characterIndex = 0;
            var previousWidth = 0f;

            while (characterIndex < line.Length)
            {
                var charCount = characterIndex < line.Length - 1 && char.IsSurrogatePair(line, characterIndex) ? 2 : 1;
                var nextIndex = Math.Min(line.Length, characterIndex + charCount);
                var nextWidth = MeasureStringWidth(line.Substring(0, nextIndex));
                var midpoint = previousWidth + (nextWidth - previousWidth) / 2f;

                if (x < midpoint)
                    break;

                characterIndex = nextIndex;
                previousWidth = nextWidth;
            }

            return characterIndex;
        }
        private int GetLineStartIndex(int line)
        {
            var startIndex = 0;
            var targetLine = MathHelper.Clamp(line, 0, Math.Max(0, _displayLines.Length - 1));

            for (var i = 0; i < targetLine; i++)
                startIndex += _displayLines[i].Length + 1;

            return startIndex;
        }

        private int GetLineTop(int line)
        {
            return _textRegion.Top + line * _font.LineHeight - _verticalScrollOffset;
        }

        private bool ScrollBy(int pixels)
        {
            var didScroll = SetVerticalScrollOffset(_verticalScrollOffset + pixels, true);

            if (didScroll)
            {
                _isManualScroll = true;
                _manualScrollCaretIndex = _cursorIndex;
            }

            return didScroll;
        }

        private bool SetVerticalScrollOffset(int value, bool invalidate)
        {
            var clampedValue = MathHelper.Clamp(value, 0, _maxVerticalScrollOffset);

            if (_verticalScrollOffset == clampedValue)
                return false;

            _verticalScrollOffset = clampedValue;
            _highlightRegions = CalculateHighlightRegions();
            _cursorRegion = CalculateCursorRegion();

            if (invalidate)
                Invalidate();

            return true;
        }

        private void PaintBackground(SpriteBatch spriteBatch, Rectangle bounds)
        {
            if (HideBackground)
                return;

            spriteBatch.DrawOnCtrl(this, ContentService.Textures.Pixel, new Rectangle(1, 1, bounds.Width - 2, bounds.Height - 2), Color.Black * 0.5f);
            spriteBatch.DrawOnCtrl(this, ContentService.Textures.Pixel, new Rectangle(1, 0, bounds.Width - 2, 2), Color.Black * 0.3f);
            spriteBatch.DrawOnCtrl(this, ContentService.Textures.Pixel, new Rectangle(1, 0, bounds.Width - 2, 1), Color.Black * 0.2f);
            spriteBatch.DrawOnCtrl(this, ContentService.Textures.Pixel, new Rectangle(0, 1, 2, bounds.Height - 2), Color.Black * 0.3f);
            spriteBatch.DrawOnCtrl(this, ContentService.Textures.Pixel, new Rectangle(0, 1, 1, bounds.Height - 2), Color.Black * 0.2f);
            spriteBatch.DrawOnCtrl(this, ContentService.Textures.Pixel, new Rectangle(1, bounds.Height - 2, bounds.Width - 2, 2), Color.Black * 0.3f);
            spriteBatch.DrawOnCtrl(this, ContentService.Textures.Pixel, new Rectangle(1, bounds.Height - 2, bounds.Width - 2, 1), Color.Black * 0.2f);
            spriteBatch.DrawOnCtrl(this, ContentService.Textures.Pixel, new Rectangle(bounds.Width - 2, 1, 2, bounds.Height - 2), Color.Black * 0.3f);
            spriteBatch.DrawOnCtrl(this, ContentService.Textures.Pixel, new Rectangle(bounds.Width - 2, 1, 1, bounds.Height - 2), Color.Black * 0.2f);
        }

        private void PaintDisplayText(SpriteBatch spriteBatch)
        {
            if (!_focused && string.IsNullOrEmpty(_text))
            {
                spriteBatch.DrawStringOnCtrl(this, _placeholderText, _font, _textRegion, Color.LightGray, false, false, 0, HorizontalAlignment.Left, VerticalAlignment.Top);
                return;
            }

            // RecalculateLayout can replace _displayLines while Blish is still drawing
            // which caused the old line count to be used on a newer/shorter array and crash.
            // We're gonna just keep the same line array for this whole paint.
            var displayLines = _displayLines ?? Array.Empty<string>();

            if (displayLines.Length == 0)
                return;

            var lineHeight = Math.Max(1, _font.LineHeight);
            var firstVisibleLine = Math.Max(
                0,
                _verticalScrollOffset / lineHeight);

            var lastVisibleLine = Math.Min(
                displayLines.Length - 1,
                (_verticalScrollOffset + _textRegion.Height) / lineHeight + 1);

            if (firstVisibleLine > lastVisibleLine)
                return;

            for (var i = firstVisibleLine; i <= lastVisibleLine; i++)
            {
                var lineTop = GetLineTop(i);

                if (lineTop + lineHeight < _textRegion.Top || lineTop > _textRegion.Bottom)
                {
                    continue;
                }

                // Use the saved line array instead of reading _displayLines again in case it changed while drawing.
                var lineText = displayLines[i] ?? string.Empty;

                spriteBatch.DrawStringOnCtrl(
                    this,
                    lineText,
                    _font,
                    new Rectangle(_textRegion.X, lineTop, _textRegion.Width, lineHeight),
                    _foreColor,
                    false,
                    false,
                    0,
                    HorizontalAlignment.Left,
                    VerticalAlignment.Top);
            }
        }

        private void PaintScrollbar(SpriteBatch spriteBatch)
        {
            var contentHeight = Math.Max(_font.LineHeight, _displayLines.Length * _font.LineHeight);

            if (_maxVerticalScrollOffset <= 0 || contentHeight <= 0)
                return;

            var trackHeight = _textRegion.Height;
            var track = new Rectangle(_size.X - ScrollbarInset - ScrollbarWidth, _textRegion.Top, ScrollbarWidth, trackHeight);
            var visibleRatio = MathHelper.Clamp(_textRegion.Height / (float)contentHeight, 0f, 1f);
            var thumbHeight = MathHelper.Clamp((int)(trackHeight * visibleRatio), MinThumbHeight, trackHeight);
            var thumbTravel = Math.Max(0, trackHeight - thumbHeight);
            var scrollRatio = _maxVerticalScrollOffset == 0 ? 0f : _verticalScrollOffset / (float)_maxVerticalScrollOffset;
            var thumb = new Rectangle(track.X, track.Y + (int)(thumbTravel * scrollRatio), ScrollbarWidth, thumbHeight);

            spriteBatch.DrawOnCtrl(this, ContentService.Textures.Pixel, track, Color.Black * 0.35f);
            spriteBatch.DrawOnCtrl(this, ContentService.Textures.Pixel, thumb, new Color(210, 210, 210, 150));
        }

        // Space-only wrapping, not splitting long words.
        // Long, unbroken text might clip with this but it shouldn't be an issue.
        private static string WrapText(BitmapFont spriteFont, string text, float maxLineWidth, out int[] newLineIndices)
        {
            newLineIndices = Array.Empty<int>();

            if (string.IsNullOrEmpty(text))
                return string.Empty;

            var wrappedText = new StringBuilder();
            var wrapIndices = new List<int>();
            var sourceOffset = 0;
            var lines = text.Split(NewLine);

            for (var i = 0; i < lines.Length; i++)
            {
                wrappedText.Append(WrapTextSegment(spriteFont, lines[i], maxLineWidth, out var segmentWrapIndices));

                var indexOffset = sourceOffset + wrapIndices.Count;

                foreach (var segmentIndex in segmentWrapIndices)
                    wrapIndices.Add(segmentIndex + indexOffset);

                sourceOffset += lines[i].Length;

                if (i < lines.Length - 1)
                {
                    wrappedText.Append(NewLine);
                    sourceOffset++;
                }
            }

            newLineIndices = wrapIndices.ToArray();
            return wrappedText.ToString();
        }

        private static string WrapTextSegment(BitmapFont spriteFont, string text, float maxLineWidth, out int[] newLineIndices)
        {
            newLineIndices = Array.Empty<int>();

            if (string.IsNullOrEmpty(text) || maxLineWidth <= 0)
                return text ?? string.Empty;

            var words = text.Split(' ');
            var sb = new StringBuilder();
            var indices = new List<int>();
            var lineWidth = 0f;
            var spaceWidth = MeasureStringWidth(spriteFont, " ");
            var sourceOffset = 0;

            for (var i = 0; i < words.Length; i++)
            {
                var word = words[i];
                var wordWidth = MeasureStringWidth(spriteFont, word);

                if (lineWidth > 0 && lineWidth + wordWidth > maxLineWidth)
                {
                    if (sb.Length > 0 && sb[sb.Length - 1] == ' ')
                    {
                        sb[sb.Length - 1] = NewLine;
                    }
                    else
                    {
                        sb.Append(NewLine);
                        indices.Add(sourceOffset + indices.Count);
                    }

                    lineWidth = 0f;
                }

                sb.Append(word);
                lineWidth += wordWidth;
                sourceOffset += word.Length;

                if (i < words.Length - 1)
                {
                    sb.Append(' ');
                    lineWidth += spaceWidth;
                    sourceOffset++;
                }
            }

            newLineIndices = indices.ToArray();
            return sb.ToString();
        }

        private static float MeasureStringWidth(BitmapFont spriteFont, string text)
        {
            return spriteFont.MeasureString(text ?? string.Empty).Width;
        }
    }
}
