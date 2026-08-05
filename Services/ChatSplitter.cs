using System;
using System.Collections.Generic;
using System.Text;

namespace rp.spark.Services
{
    internal static class ChatSplitter
    {
        public const int DefaultMaxLength = 199;
        public const string ManualBreak = "/split";

        public static IReadOnlyList<string> Split(string input, ChatSplitterOptions options = null)
        {
            var splitOptions = options ?? new ChatSplitterOptions();

            if (splitOptions.MaxLength <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(options), "Maximum length must be positive.");
            }

            var chunks = new List<string>();

            foreach (var segment in ParseSegments(input, splitOptions.BreakOnBlankLines))
            {
                SplitSegment(segment, splitOptions.MaxLength, chunks);
            }

            return chunks;
        }

        private static IReadOnlyList<string> ParseSegments(string input, bool breakOnBlankLines)
        {
            var segments = new List<string>();

            if (string.IsNullOrWhiteSpace(input))
                return segments;

            var normalizedLineEndings = input
                .Replace("\r\n", "\n")
                .Replace('\r', '\n');

            var lines = normalizedLineEndings.Split('\n');
            var currentSegment = new StringBuilder();

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                var isBlankLine = trimmedLine.Length == 0;
                var isManualBreak = string.Equals(trimmedLine, ManualBreak, StringComparison.OrdinalIgnoreCase);

                if (isManualBreak)
                {
                    AddSegment(segments, currentSegment);
                    continue;
                }

                if (isBlankLine)
                {
                    if (breakOnBlankLines)
                    {
                        AddSegment(segments, currentSegment);
                    }

                    continue;
                }

                var normalizedLine = NormalizeInlineWhitespace(line);

                if (normalizedLine.Length == 0)
                    continue;

                if (currentSegment.Length > 0)
                    currentSegment.Append(' ');

                currentSegment.Append(normalizedLine);
            }

            AddSegment(segments, currentSegment);
            return segments;
        }

        private static void AddSegment(ICollection<string> segments, StringBuilder currentSegment)
        {
            if (currentSegment.Length == 0)
                return;

            segments.Add(currentSegment.ToString());
            currentSegment.Clear();
        }

        private static string NormalizeInlineWhitespace(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            var normalized = new StringBuilder(input.Length);
            var hasPendingSpace = false;

            foreach (var character in input)
            {
                if (char.IsWhiteSpace(character))
                {
                    hasPendingSpace = normalized.Length > 0;

                    continue;
                }

                if (hasPendingSpace)
                {
                    normalized.Append(' ');
                    hasPendingSpace = false;
                }

                normalized.Append(character);
            }

            return normalized.ToString();
        }

        private static void SplitSegment(string segment, int maxLength, ICollection<string> chunks)
        {
            var remaining = segment ?? string.Empty;

            while (remaining.Length > maxLength)
            {
                var searchStart = Math.Min(maxLength, remaining.Length - 1);

                var splitAt = remaining.LastIndexOf(' ', searchStart);

                if (splitAt > 0)
                {
                    chunks.Add(remaining.Substring(0, splitAt));
                    remaining = remaining.Substring(splitAt + 1).TrimStart();
                    continue;
                }

                chunks.Add(remaining.Substring(0, maxLength));
                remaining = remaining.Substring(maxLength).TrimStart();
            }

            if (remaining.Length > 0)
                chunks.Add(remaining);
        }
    }
}