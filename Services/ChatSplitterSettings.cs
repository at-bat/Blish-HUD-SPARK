using Blish_HUD.Settings;

namespace rp.spark.Services
{
    internal sealed class ChatSplitterSettings
    {
        private const string SettingsKey = "chat-splitter";
        private const string BreakOnBlankLinesKey = "BreakOnBlankLines";
        private const string ShortenChatCommandsKey = "ShortenChatCommands";
        private const string RepeatChatCommandKey = "RepeatChatCommand";
        private const string UseContinuationMarkersKey = "UseContinuationMarkers";
        private const string EndMarkerKey = "EndMarker";
        private const string MarkContinuationStartsKey = "MarkContinuationStarts";
        private const string StartMarkerKey = "StartMarker";

        public SettingEntry<bool> BreakOnBlankLines { get; }
        public SettingEntry<bool> ShortenChatCommands { get; }
        public SettingEntry<bool> RepeatChatCommand { get; }
        public SettingEntry<bool> UseMarkers { get; }
        public SettingEntry<string> EndMarker { get; }
        public SettingEntry<bool> UseStartMarkers { get; }
        public SettingEntry<string> StartMarker { get; }

        public ChatSplitterSettings(SettingCollection moduleSettings)
        {
            var settings = moduleSettings.AddSubCollection(
                SettingsKey,
                true,
                () => "Chat Splitter");

            BreakOnBlankLines = settings.DefineSetting(
                BreakOnBlankLinesKey,
                true,
                () => "Blank lines start new messages",
                () => "When disabled, blank lines are treated as spaces. /split will still start a new message.");

            ShortenChatCommands = settings.DefineSetting(
                ShortenChatCommandsKey,
                true,
                () => "Shorten recognized chat commands",
                () => "Changes commands such as /me and /party to /e and /p.");

            RepeatChatCommand = settings.DefineSetting(
                RepeatChatCommandKey,
                true,
                () => "Repeat the starting command",
                () => "Adds the detected starting command to every generated message.");

            UseMarkers = settings.DefineSetting(
                UseContinuationMarkersKey,
                false,
                () => "Use continuation markers",
                () => "Marks messages that continue into another generated message.");

            EndMarker = settings.DefineSetting(
                EndMarkerKey,
                ">",
                () => "End marker",
                () => "Added to the end of messages that continue.");

            UseStartMarkers = settings.DefineSetting(
                MarkContinuationStartsKey,
                false,
                () => "Mark continuation starts",
                () => "Marks generated messages that continue from a previous message.");

            StartMarker = settings.DefineSetting(
                StartMarkerKey,
                ">",
                () => "Start marker",
                () => "Added to the beginning of continued messages.");
        }
    }
}