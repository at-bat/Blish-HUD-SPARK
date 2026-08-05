namespace rp.spark.Services
{
    internal sealed class ChatSplitterOptions
    {
        public int MaxLength { get; set; } =
            ChatSplitter.DefaultMaxLength;

        public bool BreakOnBlankLines { get; set; } = true;
    }
}