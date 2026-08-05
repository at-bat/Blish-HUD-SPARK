using System;
using System.Collections.Generic;
using System.Linq;

namespace rp.spark.Services
{
    // Because we don't want to lose what the player might have typed when swapping back and forth between sessions we retrain this only when the window is open and tabs are swapped.
    internal sealed class ChatSplitterSession
    {
        private string _sourceText = string.Empty;
        private IReadOnlyList<string> _generatedChunks = Array.Empty<string>();

        public string SourceText
        {
            get => _sourceText;
            set => _sourceText = value ?? string.Empty;
        }

        public IReadOnlyList<string> GeneratedChunks => _generatedChunks;

        public void SetGeneratedChunks(IEnumerable<string> chunks)
        {
            _generatedChunks = chunks == null
                ? Array.Empty<string>()
                : chunks
                    .Select(chunk => chunk ?? string.Empty)
                    .ToArray();
        }

        public void ClearGeneratedChunks()
        {
            _generatedChunks = Array.Empty<string>();
        }

        public void Clear()
        {
            SourceText = string.Empty;
            ClearGeneratedChunks();
        }
    }
}