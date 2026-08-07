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
        private int[] _copyCounts = Array.Empty<int>();

        public string SourceText
        {
            get => _sourceText;
            set => _sourceText = value ?? string.Empty;
        }

        public IReadOnlyList<string> GeneratedChunks => _generatedChunks;

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

        public void SetGeneratedChunks(IEnumerable<string> chunks)
        {
            _generatedChunks = chunks == null
                ? Array.Empty<string>()
                : chunks
                    .Select(chunk => chunk ?? string.Empty)
                    .ToArray();

            _copyCounts = new int[_generatedChunks.Count];
        }

        public void ClearGeneratedChunks()
        {
            _generatedChunks = Array.Empty<string>();
            _copyCounts = Array.Empty<int>();
        }

        public void Clear()
        {
            SourceText = string.Empty;
            ClearGeneratedChunks();
        }
    }
}