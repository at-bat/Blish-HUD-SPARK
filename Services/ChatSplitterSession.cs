using System;
using System.Collections.Generic;
using System.Linq;

namespace rp.spark.Services
{
    // Keep the draft and generated messages while the user switches between tabs.
    internal sealed class ChatSplitterSession
    {
        private string _sourceText = string.Empty;
        private string _lastSplitText = string.Empty;
        private IReadOnlyList<string> _generatedChunks = Array.Empty<string>();
        private int[] _copyCounts = Array.Empty<int>();

        public string SourceText
        {
            get => _sourceText;
            set => _sourceText = value ?? string.Empty;
        }

        public IReadOnlyList<string> GeneratedChunks => _generatedChunks;
        public bool NeedsUpdate => _generatedChunks.Count > 0 && !string.Equals(_sourceText, _lastSplitText, StringComparison.Ordinal);

        public int GetCopyCount(int index)
        {
            return index >= 0 && index < _copyCounts.Length
                ? _copyCounts[index]
                : 0;
        }

        public int IncrementCopyCount(int index)
        {
            if (index < 0 || index >= _copyCounts.Length)
                return 0;

            return ++_copyCounts[index];
        }

        public void ResetCopyCounts()
        {
            Array.Clear(_copyCounts, 0, _copyCounts.Length);
        }

        public void SetGeneratedChunks(IEnumerable<string> chunks, string sourceText)
        {
            _generatedChunks = chunks == null
                ? Array.Empty<string>()
                : chunks
                    .Select(chunk => chunk ?? string.Empty)
                    .ToArray();

            _lastSplitText = sourceText ?? string.Empty;
            _copyCounts = new int[_generatedChunks.Count];
        }

        public void ClearGeneratedChunks()
        {
            _generatedChunks = Array.Empty<string>();
            _lastSplitText = string.Empty;
            _copyCounts = Array.Empty<int>();
        }

        public void Clear()
        {
            SourceText = string.Empty;
            ClearGeneratedChunks();
        }
    }
}