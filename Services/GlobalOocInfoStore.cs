using Blish_HUD;
using Blish_HUD.Modules.Managers;
using rp.spark.Models;
using System;
using System.Collections.Generic;
using System.IO;

namespace rp.spark.Services
{
    public class GlobalOocInfoStore
    {
        private static readonly Logger Logger = Logger.GetLogger<GlobalOocInfoStore>();

        private readonly string _path;
        private readonly object _storeLock = new object();
        private Dictionary<string, string> _entries;

        public event Action<string> GlobalOocInfoChanged;

        public GlobalOocInfoStore(DirectoriesManager directoriesManager)
        {
            var sparkDirectory = directoriesManager.GetFullDirectoryPath("spark");
            _path = Path.Combine(sparkDirectory, "global-ooc-info.json");

            FileStore.EnsureDirectory(sparkDirectory, Logger, "SPARK");
        }

        public string Get(string accountName)
        {
            var key = NormalizeAccountName(accountName);

            if (string.IsNullOrWhiteSpace(key))
                return string.Empty;

            lock (_storeLock)
            {
                EnsureLoaded();

                return _entries.TryGetValue(key, out var value)
                    ? value ?? string.Empty
                    : string.Empty;
            }
        }

        public void Save(string accountName, string outOfCharacterInfo)
        {
            var key = NormalizeAccountName(accountName);

            if (string.IsNullOrWhiteSpace(key))
                throw new InvalidOperationException("Cannot save global OOC information without an account name.");

            var value = outOfCharacterInfo?.Trim() ?? string.Empty;

            if (value.Length > ProfileLimits.MaxOutOfCharacterInfoLength)
                throw new InvalidOperationException(
                    $"Global OOC information must be {ProfileLimits.MaxOutOfCharacterInfoLength} characters or fewer.");

            lock (_storeLock)
            {
                EnsureLoaded();

                if (_entries.TryGetValue(key, out var existing)
                    && string.Equals(existing ?? string.Empty, value, StringComparison.Ordinal))
                    return;

                var updatedEntries = new Dictionary<string, string>(
                    _entries,
                    StringComparer.OrdinalIgnoreCase)
                {
                    [key] = value
                };

                if (!FileStore.TryWrite(_path, updatedEntries, Logger, "SPARK global OOC information"))
                    throw new InvalidOperationException(
                        "Could not save global OOC information. Check file permissions and try again.");

                _entries = updatedEntries;
            }

            GlobalOocInfoChanged?.Invoke(key);
        }

        public string GetEffective(CharacterProfile profile)
        {
            if (profile == null)
                return string.Empty;

            return profile.UseGlobalOutOfCharacterInfo
                ? Get(profile.AccountName)
                : profile.OutOfCharacterInfo?.Trim() ?? string.Empty;
        }

        private void EnsureLoaded()
        {
            if (_entries != null)
                return;

            var entries = FileStore.ReadFile<Dictionary<string, string>>(
                _path,
                Logger,
                "SPARK global OOC information");

            _entries = entries == null
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(entries, StringComparer.OrdinalIgnoreCase);
        }

        private static string NormalizeAccountName(string accountName)
        {
            return accountName?.Trim().ToLowerInvariant() ?? string.Empty;
        }
    }
}