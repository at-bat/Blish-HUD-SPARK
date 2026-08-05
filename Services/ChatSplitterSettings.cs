using Blish_HUD.Settings;

namespace rp.spark.Services
{
    internal sealed class ChatSplitterSettings
    {
        private const string SettingsKey = "chat-splitter";
        private const string BreakOnBlankLinesKey = "BreakOnBlankLines";

        public SettingEntry<bool> BreakOnBlankLines { get; }

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
        }
    }
}