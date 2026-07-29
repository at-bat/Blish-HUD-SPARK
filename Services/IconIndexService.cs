using Blish_HUD;
using Blish_HUD.Modules.Managers;
using Newtonsoft.Json;
using rp.spark.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace rp.spark.Services
{
    // Maintains a compact searchable GW2 icon index for the 'at a glance' editor.
    // This only uses a local bundled .gzip. Previously it had an update feature, but it added too much extra code.
    // This has been rewritten to search on demand instead of being in-memory to reduce module file size by just over 50% (index alone went from 23mb to 3mb roughly)
    // Search is slightly slower than in-memory but we're not needlessly consuming RAM.
    // Stripped several parts of this that felt redundant or unwieldy to reduce complexity.
    public class IconIndexService : IDisposable
    {
        private static readonly Logger Logger = Logger.GetLogger<IconIndexService>();
        private static readonly char[] SearchWordSeparators = { ' ', '\t', '\r', '\n', ',', ';', ':', '/', '\\', '|', '-', '_' };

        // Max 5MB gzip, and 25MB uncompressed JSON.
        // Current sizes are below this, but leaving this semi-larger in case this changes.
        private const int MaxDecompressedIndexBytes = 25 * 1024 * 1024;
        private const int DownloadBufferSize = 81920;
        // Briefly spoke about the search results in Blish HUD Discord, 50 results should be fine for icons since they're small.
        private const int MaxSearchResults = 50;

        private readonly ContentsManager _contentsManager;
        private volatile bool _isDisposed;

        public IconIndexService(ContentsManager contentsManager)
        {
            _contentsManager = contentsManager;
        }

        public Task<IReadOnlyList<Gw2IconSearchResult>> SearchAsync(
            string query,
            int limit,
            CancellationToken cancellationToken = default)
        {
            return Task.Run<IReadOnlyList<Gw2IconSearchResult>>(
                () => SearchCore(query, limit, cancellationToken),
                cancellationToken);
        }

        private IReadOnlyList<Gw2IconSearchResult> SearchCore(
            string query,
            int limit,
            CancellationToken cancellationToken)
        {
            if (_isDisposed)
                return Array.Empty<Gw2IconSearchResult>();

            limit = Math.Min(limit, MaxSearchResults);

            var terms = GetSearchTerms(query);

            if (terms.Length == 0 || limit <= 0)
                return Array.Empty<Gw2IconSearchResult>();

            var results = new List<Gw2IconSearchResult>(limit);

            // Failed search shouldn't break the editor, just return zero results
            try
            {
                StreamSearch(terms, limit, results, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to search bundled GW2 icon index.");
            }

            return results;
        }

        // I might clean the JSON structure up so jumping to entries is unnecessary, this'll do for now
        private static bool MoveToEntriesArray(JsonReader reader)
        {
            if (!reader.Read() || reader.TokenType != JsonToken.StartObject)
                return false;

            while (reader.Read())
            {
                if (reader.TokenType != JsonToken.PropertyName)
                    continue;

                var propertyName = (string)reader.Value;

                if (!reader.Read())
                    return false;

                if (string.Equals(propertyName, "entries", StringComparison.Ordinal))
                    return reader.TokenType == JsonToken.StartArray;

                reader.Skip();
            }

            return false;
        }

        // Search tries name matching first, then attempts description/source/keywords if it doesn't hit 50 results
        private void StreamSearch(
            string[] terms,
            int limit,
            List<Gw2IconSearchResult> results,
            CancellationToken cancellationToken)
        {
            var nameResults = new List<Gw2IconSearchResult>(limit);
            var fallbackResults = new List<Gw2IconSearchResult>(limit * 4);
            var nameAssetIds = new HashSet<int>();
            var fallbackAssetIds = new HashSet<int>();

            using (var compressed = _contentsManager.GetFileStream(SparkServiceConfig.IconIndexFilename))
            {
                if (compressed == null)
                    return;

                using (var gzip = new GZipStream(compressed, CompressionMode.Decompress))
                using (var limited = new ReadStream(gzip, MaxDecompressedIndexBytes))
                using (var text = new StreamReader(limited, Encoding.UTF8, false, DownloadBufferSize))
                using (var reader = new JsonTextReader(text))
                {
                    if (!MoveToEntriesArray(reader))
                        return;

                    var serializer = JsonSerializer.CreateDefault();

                    while (reader.Read())
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        if (reader.TokenType == JsonToken.EndArray)
                            break;

                        if (reader.TokenType != JsonToken.StartObject)
                        {
                            reader.Skip();
                            continue;
                        }

                        var entry = serializer.Deserialize<Gw2IconIndexEntry>(reader);

                        if (entry == null || entry.AssetId <= 0)
                            continue;

                        var matchingName = FindMatchingName(entry, terms);

                        if (matchingName != null)
                        {
                            if (nameAssetIds.Add(entry.AssetId))
                            {
                                nameResults.Add(ToResult(entry, matchingName));

                                if (nameResults.Count >= limit)
                                    break;
                            }

                            continue;
                        }

                        // Limit backup list of matches so we don't use too much memory
                        if (fallbackResults.Count < limit * 4
                            && EntryContainsAllTerms(entry, terms)
                            && fallbackAssetIds.Add(entry.AssetId))
                            fallbackResults.Add(ToResult(entry, entry.Name));
                    }
                }
            }

            results.AddRange(nameResults);

            foreach (var result in fallbackResults)
            {
                if (results.Count >= limit)
                    break;

                if (!nameAssetIds.Contains(result.AssetId))
                    results.Add(result);
            }
        }

        private static Gw2IconSearchResult ToResult(Gw2IconIndexEntry entry, string matchingName)
        {
            return new Gw2IconSearchResult
            {
                AssetId = entry.AssetId,
                Name = matchingName ?? entry.Name ?? string.Empty
            };
        }

        private static string FindMatchingName(Gw2IconIndexEntry entry, IReadOnlyList<string> terms)
        {
            if (ContainsAllTerms(entry.Name, terms))
                return entry.Name;

            if (entry.Aliases == null)
                return null;

            foreach (var alias in entry.Aliases)
            {
                if (ContainsAllTerms(alias, terms))
                    return alias;
            }

            return null;
        }

        private static bool EntryContainsAllTerms(Gw2IconIndexEntry entry, IReadOnlyList<string> terms)
        {
            foreach (var term in terms)
            {
                if (!EntryContainsTerm(entry, term))
                    return false;
            }

            return true;
        }

        private static bool EntryContainsTerm(Gw2IconIndexEntry entry, string term)
        {
            if (ContainsTerm(entry.Name, term)
                || ContainsTerm(entry.Description, term)
                || ContainsTerm(entry.Source, term))
                return true;

            if (entry.Aliases != null && entry.Aliases.Any(alias => ContainsTerm(alias, term)))
                return true;

            return entry.Keywords != null && entry.Keywords.Any(keyword => ContainsTerm(keyword, term));
        }

        private static bool ContainsAllTerms(string value, IReadOnlyList<string> terms)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            return terms.All(term => ContainsTerm(value, term));
        }

        private static bool ContainsTerm(string value, string term)
        {
            return !string.IsNullOrEmpty(value)
                && value.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string[] GetSearchTerms(string query)
        {
            return (query ?? string.Empty)
                .Trim()
                .ToLowerInvariant()
                .Split(SearchWordSeparators, StringSplitOptions.RemoveEmptyEntries)
                .Take(6)
                .ToArray();
        }

        public void Dispose()
        {
            _isDisposed = true;
        }

        private sealed class ReadStream : Stream
        {
            private readonly Stream _inner;
            private readonly long _maxBytes;
            private long _bytesRead;

            public ReadStream(Stream inner, long maxBytes)
            {
                _inner = inner;
                _maxBytes = maxBytes;
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                var read = _inner.Read(buffer, offset, count);

                if (read > 0)
                {
                    _bytesRead += read;

                    if (_bytesRead > _maxBytes)
                        throw new InvalidDataException("GW2 icon index gzip expands beyond the allowed size.");
                }

                return read;
            }

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();

            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public override void Flush()
            {
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException();
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }
        }
    }
}
