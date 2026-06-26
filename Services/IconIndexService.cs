using Blish_HUD;
using Blish_HUD.Modules.Managers;
using Newtonsoft.Json;
using rp.spark.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace rp.spark.Services
{
    // Maintains a compact searchable GW2 icon index for the 'at a glance' editor.
    // The local gzip is loaded first, then we check the webserver for a newer version
    // Files are hash and size checked via the server manifest
    public class IconIndexService : IDisposable
    {
        private static readonly Logger Logger = Logger.GetLogger<IconIndexService>();
        private static readonly TimeSpan RefreshInterval = TimeSpan.FromDays(1);
        private static readonly TimeSpan StartupRefreshDelay = TimeSpan.FromSeconds(10);
        private static readonly char[] SearchWordSeparators = { ' ', '\t', '\r', '\n', ',', ';', ':', '/', '\\', '|', '-', '_' };

        private const string BundledIndexPath = SparkServiceConfig.IconIndexFilename;
        private const string CachedIndexFileName = SparkServiceConfig.IconIndexFilename;
        private const string CachedManifestFileName = SparkServiceConfig.IconIndexManifest;
        private const int MaxIndexUrlLength = 2048;

        // Max 64KB manifest, 5MB gzip, and 25MB uncompressed JSON.
        // Current sizes are below this, but leaving this semi-larger in case this changes.
        private const int MaxManifestBytes = 64 * 1024;
        private const int MaxCompressedIndexBytes = 5 * 1024 * 1024;
        private const int MaxDecompressedIndexBytes = 25 * 1024 * 1024;
        private const int DownloadBufferSize = 81920;

        private static readonly HttpClient SharedHttpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(20)
        };

        private readonly ContentsManager _contentsManager;
        private readonly string _cacheFolder;
        private readonly string _cacheIndexPath;
        private readonly string _cacheManifestPath;
        private readonly object _indexLock = new object();
        private readonly SemaphoreSlim _loadGate = new SemaphoreSlim(1, 1);

        private List<SearchableIcons> _searchableIcons = new List<SearchableIcons>();
        private bool _isLoading;
        private CancellationTokenSource _refreshCancellation;
        private Task _refreshTask;

        public IconIndexService(ContentsManager contentsManager, DirectoriesManager directoriesManager)
        {
            _contentsManager = contentsManager;

            var sparkDirectory = directoriesManager.GetFullDirectoryPath("spark");
            _cacheFolder = Path.Combine(sparkDirectory, "icon-index");
            _cacheIndexPath = Path.Combine(_cacheFolder, CachedIndexFileName);
            _cacheManifestPath = Path.Combine(_cacheFolder, CachedManifestFileName);

            FileStore.EnsureDirectory(_cacheFolder, Logger, "SPARK icon index cache");
        }

        public bool IsLoaded
        {
            get { lock (_indexLock) return _searchableIcons.Count > 0; }
        }

        public bool IsLoading
        {
            get { lock (_indexLock) return _isLoading; }
        }

        public int EntryCount
        {
            get { lock (_indexLock) return _searchableIcons.Count; }
        }

        public DateTime? GeneratedAtUtc { get; private set; }
        public int Gw2BuildId { get; private set; }

        public Task LoadAsync(CancellationToken cancellationToken = default)
        {
            return EnsureLoadedAsync(cancellationToken);
        }

        public async Task EnsureLoadedAsync(CancellationToken cancellationToken = default)
        {
            if (IsLoaded)
                return;

            await _loadGate.WaitAsync(cancellationToken);

            try
            {
                if (IsLoaded)
                    return;

                SetLoading(true);

                if (File.Exists(_cacheIndexPath))
                {
                    try
                    {
                        LoadGzipIcons(File.ReadAllBytes(_cacheIndexPath), "cached icon index", cancellationToken);
                        return;
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn(ex, "Failed to load cached GW2 icon index. Falling back to bundled index.");
                        DeleteBadCacheFile(_cacheIndexPath);
                        DeleteBadCacheFile(_cacheManifestPath);
                    }
                }

                using (var stream = _contentsManager.GetFileStream(BundledIndexPath))
                {
                    if (stream == null)
                    {
                        Logger.Warn("Bundled GW2 icon index file {path} was not found.", BundledIndexPath);
                        return;
                    }

                    using (var memory = new MemoryStream())
                    {
                        await stream.CopyToAsync(memory);
                        LoadGzipIcons(memory.ToArray(), "bundled icon index", cancellationToken);
                    }
                }
            }
            finally
            {
                SetLoading(false);
                _loadGate.Release();
            }
        }

        private void SetLoading(bool isLoading)
        {
            lock (_indexLock)
                _isLoading = isLoading;
        }

        public void Start()
        {
            if (_refreshTask != null && !_refreshTask.IsCompleted)
                return;

            Stop();
            _refreshCancellation = new CancellationTokenSource();
            _refreshTask = RunRefreshLoopAsync(_refreshCancellation.Token);
        }

        public void Stop()
        {
            var cancellation = _refreshCancellation;
            var worker = _refreshTask;
            _refreshCancellation = null;
            _refreshTask = null;

            if (cancellation == null)
                return;

            cancellation.Cancel();
            TaskCleanup.DisposeWhenComplete(worker, cancellation);
        }

        public IReadOnlyList<Gw2IconSearchResult> Search(string query, int limit)
        {
            if (limit <= 0)
                return Array.Empty<Gw2IconSearchResult>();

            var terms = GetSearchTerms(query);

            if (terms.Length == 0)
                return Array.Empty<Gw2IconSearchResult>();

            List<SearchableIcons> entries;

            lock (_indexLock)
                entries = _searchableIcons;

            if (entries.Count == 0)
                return Array.Empty<Gw2IconSearchResult>();

            var results = new List<Gw2IconSearchResult>(limit);
            var seenAssetIds = new HashSet<int>();

            AddMatches(entries, limit, results, seenAssetIds, entry => ContainsAllTerms(entry.Name, terms));
            AddMatches(entries, limit, results, seenAssetIds, entry => ContainsAllTerms(entry.SearchText, terms));

            return results;
        }

        private static void AddMatches(
            IReadOnlyList<SearchableIcons> entries,
            int limit,
            List<Gw2IconSearchResult> results,
            HashSet<int> seenAssetIds,
            Func<SearchableIcons, bool> predicate)
        {
            if (results.Count >= limit)
                return;

            foreach (var entry in entries)
            {
                if (results.Count >= limit)
                    return;

                if (entry.AssetId <= 0 || seenAssetIds.Contains(entry.AssetId))
                    continue;

                if (!predicate(entry))
                    continue;

                seenAssetIds.Add(entry.AssetId);
                results.Add(entry.ToResult());
            }
        }

        private async Task RunRefreshLoopAsync(CancellationToken cancellationToken)
        {
            await WaitUntilNextRefreshAsync(cancellationToken);

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await ServerRefreshAsync(cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "GW2 icon index refresh failed.");
                }

                try
                {
                    await Task.Delay(RefreshInterval, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
            }
        }

        private async Task WaitUntilNextRefreshAsync(CancellationToken cancellationToken)
        {
            if (!File.Exists(_cacheManifestPath))
            {
                await Task.Delay(StartupRefreshDelay, cancellationToken);
                return;
            }

            var nextRefreshAt = File.GetLastWriteTimeUtc(_cacheManifestPath).Add(RefreshInterval);
            var delay = nextRefreshAt - DateTime.UtcNow;

            if (delay <= TimeSpan.Zero)
            {
                await Task.Delay(StartupRefreshDelay, cancellationToken);
                return;
            }

            await Task.Delay(delay, cancellationToken);
        }

        // Attempt to download a new version only from the SPARK endpoint
        // If unable to, the locally bundled version is the fallback
        private async Task ServerRefreshAsync(CancellationToken cancellationToken)
        {
            var remoteManifest = await DownloadJSONAsync<GW2IconIndexManifest>(
                SparkServiceConfig.IconIndexManifestUrl,
                MaxManifestBytes,
                cancellationToken);

            if (remoteManifest == null || remoteManifest.Schema != 2)
                return;

            var cachedManifest = ReadLocalManifest();

            if (cachedManifest != null
                && File.Exists(_cacheIndexPath)
                && !ShouldDownloadIndex(remoteManifest, cachedManifest))
            {
                File.SetLastWriteTimeUtc(_cacheManifestPath, DateTime.UtcNow);
                return;
            }

            var indexUrl = GetAllowedIndexUrl(remoteManifest.IndexUrl);
            var compressedBytes = await DownloadFileAsync(indexUrl, MaxCompressedIndexBytes, cancellationToken);

            if (compressedBytes == null || compressedBytes.Length == 0)
                return;

            if (!HashCheck(compressedBytes, remoteManifest.Sha256))
            {
                Logger.Warn("Downloaded GW2 icon index failed compressed SHA-256 validation.");
                return;
            }

            var uncompressedJson = ReadGzip(compressedBytes);
            var uncompressedBytes = Encoding.UTF8.GetBytes(uncompressedJson);

            if (!HashCheck(uncompressedBytes, remoteManifest.UncompressedSha256))
            {
                Logger.Warn("Downloaded GW2 icon index failed uncompressed SHA-256 validation.");
                return;
            }

            var document = JsonConvert.DeserializeObject<Gw2IconIndexDocument>(uncompressedJson);

            if (!TryLoadIcons(document, "downloaded icon index"))
                return;

            if (!FileStore.TryWriteBytes(_cacheIndexPath, compressedBytes, Logger, "SPARK icon index cache"))
                return;

            FileStore.TryWriteText(
                _cacheManifestPath,
                JsonConvert.SerializeObject(remoteManifest, Formatting.Indented),
                Logger,
                "SPARK icon index manifest");
        }

        private GW2IconIndexManifest ReadLocalManifest()
        {
            try
            {
                if (!File.Exists(_cacheManifestPath))
                    return null;

                return JsonConvert.DeserializeObject<GW2IconIndexManifest>(File.ReadAllText(_cacheManifestPath));
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to read cached GW2 icon index manifest.");
                return null;
            }
        }

        private static bool ShouldDownloadIndex(GW2IconIndexManifest remoteManifest, GW2IconIndexManifest cachedManifest)
        {
            if (cachedManifest == null)
                return true;

            if (!string.Equals(remoteManifest.Sha256, cachedManifest.Sha256, StringComparison.OrdinalIgnoreCase))
                return true;

            if (remoteManifest.Gw2BuildId > cachedManifest.Gw2BuildId)
                return true;

            return !string.Equals(remoteManifest.Version, cachedManifest.Version, StringComparison.OrdinalIgnoreCase);
        }

        private void LoadGzipIcons(byte[] compressedBytes, string description, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var json = ReadGzip(compressedBytes);
            cancellationToken.ThrowIfCancellationRequested();
            var document = JsonConvert.DeserializeObject<Gw2IconIndexDocument>(json);
            TryLoadIcons(document, description);
        }

        private bool TryLoadIcons(Gw2IconIndexDocument document, string description)
        {
            if (document == null || document.Schema != 2 || document.Entries == null)
            {
                Logger.Warn("Skipping invalid GW2 {description}.", description);
                return false;
            }

            var entries = document.Entries
                .Where(entry => entry != null && entry.AssetId > 0)
                .Select(entry => new SearchableIcons(entry))
                .ToList();

            lock (_indexLock)
                _searchableIcons = entries;

            GeneratedAtUtc = document.GeneratedAt?.ToUniversalTime();
            Gw2BuildId = document.Gw2BuildId;

            Logger.Info("Loaded {count} entries from GW2 {description}.", entries.Count, description);
            return true;
        }

        private static string GetAllowedIndexUrl(string manifestIndexUrl)
        {
            if (string.IsNullOrWhiteSpace(manifestIndexUrl))
                return SparkServiceConfig.DefaultIconIndexUrl;

            var indexUrl = manifestIndexUrl.Trim();

            if (IsAllowedIndexUrl(indexUrl))
                return indexUrl;

            Logger.Warn("URL doesn't match, ignoring.");
            return SparkServiceConfig.DefaultIconIndexUrl;
        }

        private static bool IsAllowedIndexUrl(string indexUrl)
        {
            if (string.IsNullOrWhiteSpace(indexUrl) || indexUrl.Length > MaxIndexUrlLength)
                return false;

            if (!Uri.TryCreate(indexUrl, UriKind.Absolute, out var uri))
                return false;

            return string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                && string.Equals(uri.Host, SparkServiceConfig.ServerHost, StringComparison.OrdinalIgnoreCase)
                && uri.IsDefaultPort
                && string.IsNullOrEmpty(uri.UserInfo)
                && string.IsNullOrEmpty(uri.Query)
                && string.IsNullOrEmpty(uri.Fragment)
                && uri.AbsolutePath.StartsWith(SparkServiceConfig.IconIndexPath, StringComparison.Ordinal)
                && uri.AbsolutePath.EndsWith(".json.gz", StringComparison.Ordinal);
        }

        private static async Task<T> DownloadJSONAsync<T>(string url, int maxBytes, CancellationToken cancellationToken) where T : class
        {
            var bytes = await DownloadFileAsync(url, maxBytes, cancellationToken);

            if (bytes == null || bytes.Length == 0)
                return null;

            return JsonConvert.DeserializeObject<T>(Encoding.UTF8.GetString(bytes));
        }

        private static async Task<byte[]> DownloadFileAsync(string url, int maxBytes, CancellationToken cancellationToken)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, url))
            using (var response = await SharedHttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
            {
                if (!response.IsSuccessStatusCode)
                {
                    Logger.Warn("GW2 icon index GET {url} failed with status {status}.", url, response.StatusCode);
                    return null;
                }

                var contentLength = response.Content.Headers.ContentLength;

                if (contentLength.HasValue && contentLength.Value > maxBytes)
                {
                    Logger.Warn("GW2 icon index GET {url} was too large: {bytes} bytes.", url, contentLength.Value);
                    return null;
                }

                using (var stream = await response.Content.ReadAsStreamAsync())
                {
                    return await ReadLimitedStreamAsync(stream, maxBytes, url, cancellationToken);
                }
            }
        }

        private static async Task<byte[]> ReadLimitedStreamAsync(
            Stream stream,
            int maxBytes,
            string url,
            CancellationToken cancellationToken)
        {
            using (var memory = new MemoryStream())
            {
                var buffer = new byte[DownloadBufferSize];
                int bytesRead;

                while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                {
                    if (memory.Length + bytesRead > maxBytes)
                    {
                        Logger.Warn("GW2 icon index GET {url} exceeded the {limit} byte download limit.", url, maxBytes);
                        return null;
                    }

                    memory.Write(buffer, 0, bytesRead);
                }

                return memory.ToArray();
            }
        }

        private static string ReadGzip(byte[] compressedBytes)
        {
            if (compressedBytes == null || compressedBytes.Length == 0)
                return string.Empty;

            if (compressedBytes.Length > MaxCompressedIndexBytes)
                throw new InvalidDataException("GW2 icon index gzip is larger than the allowed compressed size.");

            using (var source = new MemoryStream(compressedBytes))
            using (var gzip = new GZipStream(source, CompressionMode.Decompress))
            using (var output = new MemoryStream())
            {
                var buffer = new byte[DownloadBufferSize];
                int bytesRead;

                while ((bytesRead = gzip.Read(buffer, 0, buffer.Length)) > 0)
                {
                    if (output.Length + bytesRead > MaxDecompressedIndexBytes)
                        throw new InvalidDataException("GW2 icon index gzip expands beyond the allowed size.");

                    output.Write(buffer, 0, bytesRead);
                }

                return Encoding.UTF8.GetString(output.ToArray());
            }
        }

        private static bool HashCheck(byte[] bytes, string expectedHash)
        {
            if (string.IsNullOrWhiteSpace(expectedHash))
                return true;

            using (var sha = SHA256.Create())
            {
                var actualHash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty);
                return string.Equals(actualHash, expectedHash.Trim(), StringComparison.OrdinalIgnoreCase);
            }
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

        private static bool ContainsAllTerms(string value, IReadOnlyList<string> terms)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            return terms.All(term => value.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static string BuildSearchText(params string[] parts)
        {
            return string.Join(
                    " ",
                    parts.Where(part => !string.IsNullOrWhiteSpace(part))
                         .Select(part => part.Trim()))
                .ToLowerInvariant();
        }

        private static void DeleteBadCacheFile(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
            }
        }

        public void Dispose()
        {
            Stop();
        }

        private sealed class SearchableIcons
        {
            public SearchableIcons(Gw2IconIndexEntry entry)
            {
                AssetId = entry.AssetId;
                Name = entry.Name ?? string.Empty;

                var keywordText = entry.Keywords == null || entry.Keywords.Count == 0
                    ? string.Empty
                    : string.Join(" ", entry.Keywords);

                SearchText = BuildSearchText(
                    Name,
                    entry.Description,
                    entry.Source,
                    keywordText);
            }

            public int AssetId { get; }
            public string Name { get; }
            public string SearchText { get; }

            public Gw2IconSearchResult ToResult()
            {
                return new Gw2IconSearchResult
                {
                    AssetId = AssetId,
                    Name = Name
                };
            }
        }
    }
}
