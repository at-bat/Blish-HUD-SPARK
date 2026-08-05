using System;
using System.Collections.Generic;
using System.Text;

namespace rp.spark.Services
{
    internal static class ChatSplitter
    {
        public const int DefaultMaxLength = 199;

        public static IReadOnlyList<string> Split(
            string input,
            int maxLength = DefaultMaxLength)
        {
            if (maxLength <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxLength),
                    "Maximum length must be positive.");
            }

            var normalized = NormalizeWhitespace(input);
            var chunks = new List<string>();

            if (normalized.Length == 0)
                return chunks;

            var remaining = normalized;

            while (remaining.Length > maxLength)
            {
                var searchStart = Math.Min(
                    maxLength,
                    remaining.Length - 1);

                var splitAt = remaining.LastIndexOf(
                    ' ',
                    searchStart);

                if (splitAt > 0)
                {
                    chunks.Add(remaining.Substring(0, splitAt));
                    remaining = remaining
                        .Substring(splitAt + 1)
                        .TrimStart();

                    continue;
                }

                // No whitespace was available, so the current word itself must be split
                var hardSplitLength = GetSafeHardSplitLength(
                    remaining,
                    maxLength);

                chunks.Add(remaining.Substring(0, hardSplitLength));
                remaining = remaining
                    .Substring(hardSplitLength)
                    .TrimStart();
            }

            if (remaining.Length > 0)
                chunks.Add(remaining);

            return chunks;
        }

        private static string NormalizeWhitespace(string input)
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

        private static int GetSafeHardSplitLength(
            string text,
            int maxLength)
        {
            var splitLength = Math.Min(maxLength, text.Length);

            if (splitLength > 1
                && splitLength < text.Length
                && char.IsHighSurrogate(text[splitLength - 1])
                && char.IsLowSurrogate(text[splitLength]))
            {
                splitLength--;
            }

            return Math.Max(1, splitLength);
        }
    }
}