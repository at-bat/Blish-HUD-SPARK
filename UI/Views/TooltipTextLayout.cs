using MonoGame.Extended.BitmapFonts;
using System;
using System.Collections.Generic;

namespace rp.spark.UI.Views
{
    internal static class TooltipTextLayout
    {
        public static IEnumerable<string> WrapLines(
            string text,
            float maximumWidth,
            BitmapFont font)
        {
            if (string.IsNullOrWhiteSpace(text))
                yield break;

            var normalized = text
                .Trim()
                .Replace("\r\n", "\n")
                .Replace('\r', '\n');

            foreach (var paragraph in normalized.Split('\n'))
            {
                if (string.IsNullOrWhiteSpace(paragraph))
                {
                    yield return string.Empty;
                    continue;
                }

                var currentLine = string.Empty;

                foreach (var word in paragraph.Split(
                    new[] { ' ' },
                    StringSplitOptions.RemoveEmptyEntries))
                {
                    var candidate = string.IsNullOrEmpty(currentLine)
                        ? word
                        : $"{currentLine} {word}";

                    if (font.MeasureString(candidate).Width <= maximumWidth)
                    {
                        currentLine = candidate;
                        continue;
                    }

                    if (!string.IsNullOrEmpty(currentLine))
                        yield return currentLine;

                    currentLine = word;
                }

                if (!string.IsNullOrEmpty(currentLine))
                    yield return currentLine;
            }
        }
    }
}