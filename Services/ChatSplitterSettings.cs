using Blish_HUD.Settings;

namespace rp.spark.Services
{
    internal sealed class ChatSplitterSettings
    {
        private const string SettingsKey = "chat-splitter";
        private const string BreakOnBlankLinesKey = "BreakOnBlankLines";
        private const string ShortenChatCommandsKey = "ShortenChatCommands";
        private const string RepeatChatCommandKey = "RepeatChatCommand";

        public SettingEntry<bool> BreakOnBlankLines { get; }
        public SettingEntry<bool> ShortenChatCommands { get; }
        public SettingEntry<bool> RepeatChatCommand { get; }

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
        }
    }
}