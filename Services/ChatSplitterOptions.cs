namespace rp.spark.Services
{
    internal sealed class ChatSplitterOptions
    {
        public int MaxLength { get; set; } = ChatSplitter.DefaultMaxLength;
        public bool BreakOnBlankLines { get; set; } = true;
        public bool ShortenChatCommands { get; set; } = true;
        public bool RepeatChatCommand { get; set; } = true;
    }
}