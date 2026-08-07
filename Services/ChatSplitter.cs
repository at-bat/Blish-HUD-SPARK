using System;
using System.Collections.Generic;
using System.Text;

namespace rp.spark.Services
{
    internal static class ChatSplitter
    {
        public const int DefaultMaxLength = 199;
        public const string ManualBreak = "/split";
        private const int MaxMarkerLength = 3;

        private static readonly Dictionary<string, string> ChatCommands = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "/e", "/e" },
                { "/em", "/e" },
                { "/emote", "/e" },
                { "/me", "/e" },

                { "/s", "/s" },
                { "/say", "/s" },
                { "/l", "/s" },
                { "/local", "/s" },

                { "/p", "/p" },
                { "/party", "/p" },
                { "/gr", "/p" },
                { "/groupe", "/p" },

                { "/d", "/d" },
                { "/squad", "/d" }
            };

        public static bool TrySplit(string input, ChatSplitterOptions options, out IReadOnlyList<string> chunks, out string error)
        {
            var splitOptions = options ?? new ChatSplitterOptions();

            if (splitOptions.MaxLength <= 0)
                throw new ArgumentOutOfRangeException(nameof(options), "Maximum length must be positive.");

            var segments = ParseSegments(input, splitOptions.BreakOnBlankLines);
            var generatedChunks = new List<string>();

            if (!TryExtractCommand(
                segments,
                splitOptions.ShortenChatCommands,
                out var command,
                out error))
            {
                chunks = generatedChunks;
                return false;
            }

            if (!TryGetMarkers(splitOptions, out var endMarker, out var startMarker, out error))
            {
                chunks = generatedChunks;
                return false;
            }

            var reservedLength = command.Length > 0
                ? command.Length + 1
                : 0;

            if (splitOptions.UseMarkers)
                reservedLength += endMarker.Length + 1;

            if (splitOptions.UseMarkers &&
                splitOptions.UseStartMarkers)
            {
                reservedLength += startMarker.Length + 1;
            }

            if (reservedLength >= splitOptions.MaxLength)
            {
                chunks = generatedChunks;
                error = "The maximum message length is too short for the selected command and continuation markers.";
                return false;
            }

            for (var index = 0; index < segments.Count; index++)
            {
                SplitSegment(
                    segments[index],
                    index < segments.Count - 1,
                    command,
                    endMarker,
                    startMarker,
                    splitOptions,
                    generatedChunks);
            }

            chunks = generatedChunks;
            return true;
        }

        private static bool TryExtractCommand(IList<string> segments, bool shortenCommand, out string command, out string error)
        {
            command = string.Empty;
            error = string.Empty;

            if (segments.Count == 0 || segments[0].Length == 0 || segments[0][0] != '/')
            {
                return true;
            }

            var spaceAt = segments[0].IndexOf(' ');
            var enteredCommand = spaceAt < 0
                ? segments[0]
                : segments[0].Substring(0, spaceAt);

            if (!ChatCommands.TryGetValue(enteredCommand, out var shortCommand))
            {
                error = $"'{enteredCommand}' isn't a supported chat prefix. Use /e, /s, /p, or /d, or remove the starting slash command.";
                return false;
            }

            command = shortenCommand ? shortCommand : enteredCommand;

            var remainingText = spaceAt < 0
                ? string.Empty
                : segments[0].Substring(spaceAt + 1).TrimStart();

            if (remainingText.Length == 0)
                segments.RemoveAt(0);
            else
                segments[0] = remainingText;

            return true;
        }

        private static bool TryGetMarkers(ChatSplitterOptions options, out string endMarker, out string startMarker, out string error)
        {
            endMarker = (options.EndMarker ?? string.Empty).Trim();
            startMarker = (options.StartMarker ?? string.Empty).Trim();
            error = string.Empty;

            if (!options.UseMarkers)
            {
                endMarker = string.Empty;
                startMarker = string.Empty;
                return true;
            }

            if (!IsValidMarker(endMarker))
            {
                error = "The end marker must contain 1 to 3 characters and cannot contain spaces or /.";
                return false;
            }

            if (!options.UseStartMarkers)
            {
                startMarker = string.Empty;
                return true;
            }

            if (!IsValidMarker(startMarker))
            {
                error = "The start marker must contain 1 to 3 characters and cannot contain spaces or /.";
                return false;
            }

            return true;
        }

        private static bool IsValidMarker(string marker)
        {
            if (string.IsNullOrEmpty(marker) || marker.Length > MaxMarkerLength || marker.IndexOf('/') >= 0)
            {
                return false;
            }

            foreach (var character in marker)
            {
                if (char.IsWhiteSpace(character))
                    return false;
            }

            return true;
        }

        private static List<string> ParseSegments(string input, bool breakOnBlankLines)
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
                        AddSegment(segments, currentSegment);

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

        private static void SplitSegment(
            string segment,
            bool hasLaterSegments,
            string command,
            string endMarker,
            string startMarker,
            ChatSplitterOptions options,
            ICollection<string> chunks)
        {
            var remaining = segment ?? string.Empty;

            while (remaining.Length > 0)
            {
                var beginning = command.Length > 0 && (options.RepeatChatCommand || chunks.Count == 0)
                    ? command + " "
                    : string.Empty;

                if (options.UseMarkers && options.UseStartMarkers && chunks.Count > 0)
                {
                    beginning += startMarker + " ";
                }

                var finalBodyLimit = options.MaxLength - beginning.Length;
                var hasMore = hasLaterSegments || remaining.Length > finalBodyLimit;
                var ending = options.UseMarkers && hasMore ? " " + endMarker : string.Empty;

                var bodyLimit = finalBodyLimit - ending.Length;

                if (remaining.Length <= bodyLimit)
                {
                    chunks.Add(beginning + remaining + ending);
                    return;
                }

                var searchStart = Math.Min(bodyLimit, remaining.Length - 1);
                var splitAt = remaining.LastIndexOf(' ', searchStart);

                if (splitAt > 0)
                {
                    chunks.Add(beginning + remaining.Substring(0, splitAt) + ending);
                    remaining = remaining.Substring(splitAt + 1).TrimStart();
                    continue;
                }

                chunks.Add(beginning + remaining.Substring(0, bodyLimit) + ending);

                remaining = remaining.Substring(bodyLimit).TrimStart();
            }
        }
    }
}