using Blish_HUD;
using Blish_HUD.Modules.Managers;
using rp.spark.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace rp.spark.Services
{
    // Profiles are cached locally, and only if the person is online when you open their profile will it update to the freshest copy if it's changed since you last looked
    // If the server is offline, you can still see local copies of the profiles
    // If you were blocked by someone, you still see the last local cached copy of their profile, but it never updates again
    // This behaviour is on purpose, see comment in SparkSettings.cs for GetBlockedAccountNames
    public class ProfileCache
    {
        private static readonly Logger Logger = Logger.GetLogger<ProfileCache>();

        // Adding in cache limits for unbookmarked profiles to keep this growing indefinitely
        private const int MaxRecentProfiles = 250;
        private static readonly TimeSpan RecentProfileMaxAge = TimeSpan.FromDays(180);

        private readonly string _cacheDirectory;
        private readonly ProfileValidator _validator;
        private readonly object _cacheLock = new object();

        // Lookup for cached profiles. This only keeps the list/search/bookmark data and file path to keep it small.
        // Originally, this used the full profile data, but at a large scale that would use a lot of memory so it changed.
        // See SavedProfileIndexFile below
        private Dictionary<string, ProfileCacheEntry> _indexByKey;

        public ProfileCache(DirectoriesManager directoriesManager, ProfileValidator validator)
        {
            _validator = validator;
            var sparkDirectory = directoriesManager.GetFullDirectoryPath("spark");
            _cacheDirectory = Path.Combine(sparkDirectory, "profile-cache");

            FileStore.EnsureDirectory(_cacheDirectory, Logger, "SPARK profile cache");
        }

        public SavedProfile Save(CharacterProfile profile, PlayerPresence presence, bool bookmark = false)
        {
            if (profile == null)
                throw new InvalidOperationException("Cannot cache a missing profile.");

            PresenceMapper.FillProfileFromPresence(profile, presence);

            var validation = _validator.Validate(profile);
            if (!validation.IsValid)
                throw new InvalidOperationException(string.Join(Environment.NewLine, validation.Errors));

            var cacheKey = GetCacheKey(profile, presence);
            var existing = FindSummary(cacheKey);

            var record = new SavedProfile
            {
                CacheKey = cacheKey,
                Profile = profile,
                Presence = presence ?? new PlayerPresence(),
                CachedAt = DateTime.UtcNow,
                IsBookmarked = bookmark || existing?.IsBookmarked == true,
                BookmarkedAt = bookmark
                    ? DateTime.UtcNow
                    : existing?.BookmarkedAt
            };

            Write(record);
            return record;
        }

        public SavedProfile Bookmark(CharacterProfile profile, PlayerPresence presence)
        {
            return Save(profile, presence, true);
        }

        // Full profile data is loaded ONLY when a profile is opened. List views use the summary index instead.
        // Inserting fix for missing/corrupt files by having cache clean the entry
        public SavedProfile Load(string cacheKey)
        {
            cacheKey = NormalizeCacheKey(cacheKey);

            if (string.IsNullOrWhiteSpace(cacheKey))
                return null;

            ProfileCacheEntry entry;

            lock (_cacheLock)
            {
                GetIndexLocked().TryGetValue(cacheKey, out entry);
            }

            if (entry == null)
                return null;

            var record = LoadFromPath(entry.Path);

            if (record == null)
                Remove(cacheKey);

            return record;
        }

        private SavedProfile LoadFromPath(string path)
        {
            var record = FileStore.ReadFile<SavedProfile>(path, Logger, "SPARK cached profile");

            if (record?.SchemaVersion == 1)
                return NormalizeLoadedRecord(record, path);

            if (record != null)
                Logger.Warn("Skipping unsupported SPARK cached profile at {path}.", path);

            return null;
        }

        public bool IsBookmarked(string cacheKey)
        {
            if (string.IsNullOrWhiteSpace(cacheKey))
                return false;

            lock (_cacheLock)
            {
                return GetIndexLocked().TryGetValue(NormalizeCacheKey(cacheKey), out var entry)
                    && entry?.Summary?.IsBookmarked == true;
            }
        }

        public IReadOnlyList<SavedProfileSummary> ListRecent()
        {
            lock (_cacheLock)
            {
                return GetIndexLocked()
                    .Values
                    .Select(entry => CloneSummary(entry.Summary))
                    .Where(summary => summary != null)
                    .OrderByDescending(summary => summary.CachedAt)
                    .ToList();
            }
        }

        public IReadOnlyList<SavedProfileSummary> ListBookmarked()
        {
            lock (_cacheLock)
            {
                return GetIndexLocked()
                    .Values
                    .Select(entry => CloneSummary(entry.Summary))
                    .Where(summary => summary?.IsBookmarked == true)
                    .OrderBy(summary => summary.BookmarkedAt ?? summary.CachedAt)
                    .ToList();
            }
        }

        public void RemoveBookmark(string cacheKey)
        {
            cacheKey = NormalizeCacheKey(cacheKey);

            if (string.IsNullOrWhiteSpace(cacheKey))
                return;

            var record = Load(cacheKey);

            if (record == null)
            {
                Remove(cacheKey);
                return;
            }

            record.IsBookmarked = false;
            record.BookmarkedAt = null;
            Write(record);
        }

        public bool Remove(string cacheKey)
        {
            cacheKey = NormalizeCacheKey(cacheKey);

            if (string.IsNullOrWhiteSpace(cacheKey))
                return false;

            ProfileCacheEntry entry = null;
            var removedFromIndex = false;

            lock (_cacheLock)
            {
                if (_indexByKey == null)
                    _indexByKey = ReadIndex();

                if (_indexByKey.TryGetValue(cacheKey, out entry))
                    removedFromIndex = _indexByKey.Remove(cacheKey);
            }

            var path = entry?.Path ?? FileStore.GetSafePath(_cacheDirectory, cacheKey);
            var removedFile = TryDeleteCacheFile(path);

            return removedFromIndex || removedFile;
        }

        public static string GetCacheKey(CharacterProfile profile, PlayerPresence presence)
        {
            var cacheKey = GetProfileCacheKey(profile, presence);

            return string.IsNullOrWhiteSpace(cacheKey)
                ? Guid.NewGuid().ToString()
                : cacheKey;
        }

        public static string GetProfileCacheKey(CharacterProfile profile, PlayerPresence presence)
        {
            if (!string.IsNullOrWhiteSpace(profile?.ProfileId))
                return profile.ProfileId.Trim();

            if (!string.IsNullOrWhiteSpace(presence?.ActiveProfileId))
                return presence.ActiveProfileId.Trim();

            var accountName = presence?.AccountName ?? profile?.AccountName ?? string.Empty;
            var characterName = presence?.OfficialCharacterName ?? profile?.CharacterName ?? string.Empty;
            var fallbackKey = $"{accountName.Trim()}|{characterName.Trim()}";

            return string.IsNullOrWhiteSpace(fallbackKey.Trim('|'))
                ? string.Empty
                : fallbackKey;
        }

        // Cache files use CacheKey as the file name now (regressing what we did previously)
        // Less user friendly, but less headaches for making things consistent, especially around profile character renames, etc.
        private string GetRecordPath(SavedProfile record)
        {
            return FileStore.GetSafePath(
                _cacheDirectory,
                NormalizeCacheKey(record?.CacheKey));
        }

        private Dictionary<string, ProfileCacheEntry> GetIndexLocked()
        {
            if (_indexByKey == null)
                _indexByKey = ReadIndex();

            return _indexByKey;
        }

        private SavedProfileSummary FindSummary(string cacheKey)
        {
            if (string.IsNullOrWhiteSpace(cacheKey))
                return null;

            lock (_cacheLock)
            {
                return GetIndexLocked().TryGetValue(NormalizeCacheKey(cacheKey), out var entry)
                    ? CloneSummary(entry.Summary)
                    : null;
            }
        }

        // This index contains only what we need to fill in each row, sorting, searching, and bookmark checks to keep it light.
        // If duplicate files use the same cache key, we only keep the newest version and discard the previous.
        private Dictionary<string, ProfileCacheEntry> ReadIndex()
        {
            var indexEntries = FileStore
                .GetFiles(_cacheDirectory, Logger, "SPARK cached profile")
                .Select(ReadIndexEntry)
                .Where(entry =>
                    entry?.Summary != null
                    && !string.IsNullOrWhiteSpace(entry.Summary.CacheKey))
                .ToList();

            var removedCount = 0;

            var expirationCutoff = DateTime.UtcNow.Subtract(RecentProfileMaxAge);

            var expiredEntries = indexEntries
                .Where(entry =>
                    !entry.Summary.IsBookmarked
                    && entry.Summary.CachedAt < expirationCutoff)
                .ToList();

            removedCount += RemoveCacheEntries(indexEntries, expiredEntries);

            var excessEntries = indexEntries
                .Where(entry => !entry.Summary.IsBookmarked)
                .OrderByDescending(entry => entry.Summary.CachedAt)
                .ThenBy(entry => entry.Summary.CacheKey, StringComparer.OrdinalIgnoreCase)
                .Skip(MaxRecentProfiles)
                .ToList();

            removedCount += RemoveCacheEntries(indexEntries, excessEntries);

            if (removedCount > 0)
            {
                Logger.Info(
                    "Pruned {count} old or excess SPARK cached profile(s).",
                    removedCount);
            }

            var index = new Dictionary<string, ProfileCacheEntry>(
                StringComparer.OrdinalIgnoreCase);

            foreach (var entry in indexEntries
                         .OrderByDescending(entry => entry.Summary.CachedAt)
                         .ThenBy(entry => entry.Summary.CacheKey, StringComparer.OrdinalIgnoreCase))
            {
                var cacheKey = NormalizeCacheKey(entry.Summary.CacheKey);

                if (!string.IsNullOrWhiteSpace(cacheKey) && !index.ContainsKey(cacheKey))
                    index[cacheKey] = entry;
            }

            return index;
        }

        private int RemoveCacheEntries(List<ProfileCacheEntry> entries, IEnumerable<ProfileCacheEntry> candidates)
        {
            var removedCount = 0;

            foreach (var entry in candidates
                         ?? Enumerable.Empty<ProfileCacheEntry>())
            {
                if (!TryDeleteCacheEntry(entry))
                    continue;

                entries.Remove(entry);
                removedCount++;
            }

            return removedCount;
        }

        private int TrimRecent()
        {
            if (_indexByKey == null)
                return 0;

            var excessEntries = _indexByKey
                .Values
                .Where(entry =>
                    entry?.Summary != null
                    && !entry.Summary.IsBookmarked)
                .OrderByDescending(entry => entry.Summary.CachedAt)
                .Skip(MaxRecentProfiles)
                .ToList();

            var removedCount = 0;

            foreach (var entry in excessEntries)
            {
                if (!TryDeleteCacheEntry(entry))
                    continue;

                _indexByKey.Remove(
                    NormalizeCacheKey(entry.Summary.CacheKey));

                removedCount++;
            }

            return removedCount;
        }

        // Splitting this into entries and files
        private bool TryDeleteCacheEntry(ProfileCacheEntry entry)
        {
            if (entry?.Summary == null || entry.Summary.IsBookmarked)
                return false;

            return TryDeleteCacheFile(entry.Path);
        }

        private bool TryDeleteCacheFile(string path)
        {
            if (!IsManagedCachePath(path))
                return false;

            try
            {
                if (File.Exists(path))
                    File.Delete(path);

                return true;
            }
            catch (Exception ex)
            {
                Logger.Warn(
                    "Failed to delete a SPARK cached profile ({errorType}).",
                    ex.GetType().Name);

                return false;
            }
        }

        private bool IsManagedCachePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            try
            {
                var fullCacheDirectory = Path
                    .GetFullPath(_cacheDirectory)
                    .TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar)
                    + Path.DirectorySeparatorChar;

                var fullPath = Path.GetFullPath(path);

                return fullPath.StartsWith(
                           fullCacheDirectory,
                           StringComparison.OrdinalIgnoreCase)
                       && string.Equals(
                           Path.GetExtension(fullPath),
                           ".json",
                           StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private ProfileCacheEntry ReadIndexEntry(string path)
        {
            var snapshot = FileStore.ReadFile<SavedProfileIndexFile>(path, Logger, "SPARK cached profile index");

            if (snapshot?.SchemaVersion == 1)
                return new ProfileCacheEntry
                {
                    Path = path,
                    Summary = NormalizeSummary(ToSummary(snapshot), path)
                };

            if (snapshot != null)
                Logger.Warn("Skipping unsupported SPARK cached profile at {path}.", path);

            return null;
        }

        private static SavedProfile NormalizeLoadedRecord(SavedProfile record, string path)
        {
            if (record == null)
                return null;

            if (record.CachedAt == default)
                record.CachedAt = GetFallbackCachedAt(record.BookmarkedAt, record.Presence?.LastSeen ?? default, path);

            return record;
        }

        private static SavedProfileSummary NormalizeSummary(SavedProfileSummary summary, string path)
        {
            if (summary == null)
                return null;

            summary.CacheKey = NormalizeCacheKey(summary.CacheKey);

            if (summary.CachedAt == default)
                summary.CachedAt = GetFallbackCachedAt(summary.BookmarkedAt, summary.LastSeen, path);

            return summary;
        }

        // Write now checks if we have more than 250 profiles
        // If more than 250 non-bookmarked ones, we remove oldest
        private void Write(SavedProfile record)
        {
            record.CacheKey = NormalizeCacheKey(record.CacheKey);
            var path = GetRecordPath(record);

            if (!FileStore.TryWrite(
                    path,
                    record,
                    Logger,
                    "SPARK cached profile"))
            {
                throw new InvalidOperationException(
                    "Could not save the cached profile. Check file permissions and try again.");
            }

            lock (_cacheLock)
            {
                if (_indexByKey == null
                    || string.IsNullOrWhiteSpace(record.CacheKey))
                {
                    return;
                }

                _indexByKey[NormalizeCacheKey(record.CacheKey)] =
                    new ProfileCacheEntry
                    {
                        Path = path,
                        Summary = ToSummary(record)
                    };

                var removedCount =
                    TrimRecent();

                if (removedCount > 0)
                {
                    Logger.Info(
                        "Pruned {count} excess SPARK cached profile(s).",
                        removedCount);
                }
            }
        }

        private static DateTime GetFallbackCachedAt(DateTime? bookmarkedAt, DateTime lastSeen, string path)
        {
            var fileTime = GetFileTime(path);

            if (fileTime != default)
                return fileTime;

            if (bookmarkedAt.HasValue && bookmarkedAt.Value != default)
                return bookmarkedAt.Value.ToUniversalTime();

            if (lastSeen != default)
                return lastSeen.ToUniversalTime();

            return DateTime.UtcNow;
        }

        private static DateTime GetFileTime(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                    return default;

                var lastWrite = File.GetLastWriteTimeUtc(path);

                return lastWrite.Year <= 1900
                    ? default
                    : lastWrite;
            }
            catch
            {
                return default;
            }
        }

        private static string NormalizeCacheKey(string cacheKey)
        {
            return cacheKey?.Trim() ?? string.Empty;
        }

        private static SavedProfileSummary ToSummary(SavedProfile record)
        {
            if (record == null)
                return null;

            return new SavedProfileSummary
            {
                CacheKey = NormalizeCacheKey(record.CacheKey),
                ProfileId = Clean(TextUtil.FirstNonEmpty(record.Profile?.ProfileId, record.Presence?.ActiveProfileId)),
                ProfileName = Clean(TextUtil.FirstNonEmpty(record.Profile?.ProfileName, record.Presence?.ActiveProfileName)),
                IsMature = record.Profile?.IsMature == true || record.Presence?.IsMature == true,
                AccountName = Clean(TextUtil.FirstNonEmpty(record.Presence?.AccountName, record.Profile?.AccountName)),
                OfficialCharacterName = Clean(TextUtil.FirstNonEmpty(record.Presence?.OfficialCharacterName, record.Profile?.CharacterName)),
                DisplayCharacterName = Clean(TextUtil.FirstNonEmpty(record.Presence?.DisplayCharacterName, record.Profile?.DisplayName)),
                Race = Clean(TextUtil.FirstNonEmpty(record.Presence?.Race, record.Profile?.Race)),
                Profession = Clean(TextUtil.FirstNonEmpty(record.Presence?.Profession, record.Profile?.Profession)),
                CustomProfession = Clean(TextUtil.FirstNonEmpty(record.Presence?.CustomProfession, record.Profile?.CustomProfession)),
                ActiveProfileId = Clean(TextUtil.FirstNonEmpty(record.Presence?.ActiveProfileId, record.Profile?.ProfileId)),
                ActiveProfileName = Clean(TextUtil.FirstNonEmpty(record.Presence?.ActiveProfileName, record.Profile?.ProfileName)),
                Status = record.Presence?.Status ?? RPStatus.Online,
                Currently = Clean(TextUtil.FirstNonEmpty(record.Presence?.Currently, record.Profile?.Currently)),
                OutOfCharacterInfo = Clean(TextUtil.FirstNonEmpty(record.Presence?.OutOfCharacterInfo, record.Profile?.OutOfCharacterInfo)),
                LocationName = Clean(record.Presence?.LocationName),
                Region = record.Presence?.Region ?? record.Profile?.Region ?? ProfileRegion.NA,
                LastSeen = record.Presence?.LastSeen ?? default,
                CachedAt = record.CachedAt,
                IsBookmarked = record.IsBookmarked,
                BookmarkedAt = record.BookmarkedAt
            };
        }

        private static SavedProfileSummary ToSummary(SavedProfileIndexFile snapshot)
        {
            if (snapshot == null)
                return null;

            return new SavedProfileSummary
            {
                CacheKey = NormalizeCacheKey(snapshot.CacheKey),
                ProfileId = Clean(TextUtil.FirstNonEmpty(snapshot.Profile?.ProfileId, snapshot.Presence?.ActiveProfileId)),
                ProfileName = Clean(TextUtil.FirstNonEmpty(snapshot.Profile?.ProfileName, snapshot.Presence?.ActiveProfileName)),
                IsMature = snapshot.Profile?.IsMature == true || snapshot.Presence?.IsMature == true,
                AccountName = Clean(TextUtil.FirstNonEmpty(snapshot.Presence?.AccountName, snapshot.Profile?.AccountName)),
                OfficialCharacterName = Clean(TextUtil.FirstNonEmpty(snapshot.Presence?.OfficialCharacterName, snapshot.Profile?.CharacterName)),
                DisplayCharacterName = Clean(TextUtil.FirstNonEmpty(snapshot.Presence?.DisplayCharacterName, snapshot.Profile?.DisplayName)),
                Race = Clean(TextUtil.FirstNonEmpty(snapshot.Presence?.Race, snapshot.Profile?.Race)),
                Profession = Clean(TextUtil.FirstNonEmpty(snapshot.Presence?.Profession, snapshot.Profile?.Profession)),
                CustomProfession = Clean(TextUtil.FirstNonEmpty(snapshot.Presence?.CustomProfession, snapshot.Profile?.CustomProfession)),
                ActiveProfileId = Clean(TextUtil.FirstNonEmpty(snapshot.Presence?.ActiveProfileId, snapshot.Profile?.ProfileId)),
                ActiveProfileName = Clean(TextUtil.FirstNonEmpty(snapshot.Presence?.ActiveProfileName, snapshot.Profile?.ProfileName)),
                Status = snapshot.Presence?.Status ?? RPStatus.Online,
                Currently = Clean(TextUtil.FirstNonEmpty(snapshot.Presence?.Currently, snapshot.Profile?.Currently)),
                OutOfCharacterInfo = Clean(TextUtil.FirstNonEmpty(snapshot.Presence?.OutOfCharacterInfo, snapshot.Profile?.OutOfCharacterInfo)),
                LocationName = Clean(snapshot.Presence?.LocationName),
                Region = snapshot.Presence?.Region ?? snapshot.Profile?.Region ?? ProfileRegion.NA,
                LastSeen = snapshot.Presence?.LastSeen ?? default,
                CachedAt = snapshot.CachedAt,
                IsBookmarked = snapshot.IsBookmarked,
                BookmarkedAt = snapshot.BookmarkedAt
            };
        }

        // Just in case so we don't accidentally mutate any data through the UI or something
        private static SavedProfileSummary CloneSummary(SavedProfileSummary summary)
        {
            if (summary == null)
                return null;

            return new SavedProfileSummary
            {
                CacheKey = summary.CacheKey,
                ProfileId = summary.ProfileId,
                ProfileName = summary.ProfileName,
                IsMature = summary.IsMature,
                AccountName = summary.AccountName,
                OfficialCharacterName = summary.OfficialCharacterName,
                DisplayCharacterName = summary.DisplayCharacterName,
                Race = summary.Race,
                Profession = summary.Profession,
                CustomProfession = summary.CustomProfession,
                ActiveProfileId = summary.ActiveProfileId,
                ActiveProfileName = summary.ActiveProfileName,
                Status = summary.Status,
                Currently = summary.Currently,
                OutOfCharacterInfo = summary.OutOfCharacterInfo,
                LocationName = summary.LocationName,
                Region = summary.Region,
                LastSeen = summary.LastSeen,
                CachedAt = summary.CachedAt,
                IsBookmarked = summary.IsBookmarked,
                BookmarkedAt = summary.BookmarkedAt
            };
        }

        private static string Clean(string value)
        {
            return value?.Trim() ?? string.Empty;
        }

        private sealed class ProfileCacheEntry
        {
            public string Path { get; set; }
            public SavedProfileSummary Summary { get; set; }
        }

        // For indexing saved profiles without using the full profile data
        // Don't need to include potentially large fields like description, known for, at a glance info, etc.
        private sealed class SavedProfileIndexFile
        {
            public SavedProfileIndexFile()
            {
            }

            public int SchemaVersion { get; set; } = 1;
            public string CacheKey { get; set; } = string.Empty;
            public ProfileIndexFields Profile { get; set; }
            public PresenceIndexFields Presence { get; set; }
            public DateTime CachedAt { get; set; }
            public bool IsBookmarked { get; set; }
            public DateTime? BookmarkedAt { get; set; }
        }

        // These are mostly needed for filling in rows, searching and sorting.
        private sealed class ProfileIndexFields
        {
            public ProfileIndexFields()
            {
            }

            public string ProfileId { get; set; } = string.Empty;
            public string ProfileName { get; set; } = string.Empty;
            public bool IsMature { get; set; }
            public string AccountName { get; set; } = string.Empty;
            public string CharacterName { get; set; } = string.Empty;
            public string DisplayName { get; set; } = string.Empty;
            public string Race { get; set; } = string.Empty;
            public string Profession { get; set; } = string.Empty;
            public string CustomProfession { get; set; } = string.Empty;
            public ProfileRegion Region { get; set; } = ProfileRegion.NA;
            public string Currently { get; set; } = string.Empty;
            public string OutOfCharacterInfo { get; set; } = string.Empty;
        }

        // Needed for tooltips from the presence on rows, so we save these.
        private sealed class PresenceIndexFields
        {
            public PresenceIndexFields()
            {
            }

            public string AccountName { get; set; } = string.Empty;
            public string OfficialCharacterName { get; set; } = string.Empty;
            public string DisplayCharacterName { get; set; } = string.Empty;
            public string Race { get; set; } = string.Empty;
            public string Profession { get; set; } = string.Empty;
            public string CustomProfession { get; set; } = string.Empty;
            public string ActiveProfileId { get; set; } = string.Empty;
            public string ActiveProfileName { get; set; } = string.Empty;
            public bool IsMature { get; set; }
            public RPStatus Status { get; set; } = RPStatus.Online;
            public string Currently { get; set; } = string.Empty;
            public string OutOfCharacterInfo { get; set; } = string.Empty;
            public string LocationName { get; set; } = string.Empty;
            public ProfileRegion Region { get; set; } = ProfileRegion.NA;
            public DateTime LastSeen { get; set; }
        }

    }
}
