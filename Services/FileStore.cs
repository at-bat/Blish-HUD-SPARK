using Blish_HUD;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace rp.spark.Services
{
    internal static class FileStore
    {
        public static void EnsureDirectory(string directory, Logger logger, string description)
        {
            try
            {
                Directory.CreateDirectory(directory);
            }
            catch (UnauthorizedAccessException ex)
            {
                BlishWarnings.FileSaveBlocked(ex, directory, $"create the {description} directory");
                logger.Warn(ex, "Failed to create {description} directory at {directory}.", description, directory);
                throw;
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "Failed to create {description} directory at {directory}.", description, directory);
                throw;
            }
        }

        public static IReadOnlyList<string> GetFiles(string directory, Logger logger, string description)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(directory))
                    return new List<string>();

                return Directory.GetFiles(directory, "*.json").OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList();
            }
            catch (DirectoryNotFoundException)
            {
                return new List<string>();
            }
            catch (UnauthorizedAccessException ex)
            {
                BlishWarnings.FileSaveBlocked(ex, directory, $"read {description} files");
                logger.Warn(ex, "Failed to enumerate {description} JSON files in {directory}.", description, directory);
                return new List<string>();
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "Failed to enumerate {description} JSON files in {directory}.", description, directory);
                return new List<string>();
            }
        }

        public static T ReadFile<T>(string path, Logger logger, string description)
            where T : class
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path))
                    return null;

                return JsonConvert.DeserializeObject<T>(File.ReadAllText(path));
            }
            catch (FileNotFoundException)
            {
                return null;
            }
            catch (DirectoryNotFoundException)
            {
                return null;
            }
            catch (UnauthorizedAccessException ex)
            {
                BlishWarnings.FileSaveBlocked(ex, path, $"read {description} data");
                logger.Warn(ex, "Failed to read {description} JSON file at {path}.", description, path);
                return null;
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "Failed to read {description} JSON file at {path}.", description, path);
                return null;
            }
        }

        public static bool TryWrite(string path, object value, Logger logger, string description)
        {
            return TryWriteFile(path, logger, description, "JSON", () =>
            {
                var json = JsonConvert.SerializeObject(value, Formatting.Indented);
                WriteText(path, json);
            });
        }

        public static bool TryWriteText(string path, string text, Logger logger, string description)
        {
            return TryWriteFile(path, logger, description, "text", () => WriteText(path, text ?? string.Empty));
        }

        public static bool TryWriteBytes(string path, byte[] bytes, Logger logger, string description)
        {
            return TryWriteFile(path, logger, description, "binary", () =>
            {
                if (bytes == null)
                    throw new InvalidOperationException("Cannot write bytes without file content.");

                WriteBytes(path, bytes);
            });
        }

        private static bool TryWriteFile(string path, Logger logger, string description, string fileKind, Action writeFile)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path))
                    throw new InvalidOperationException($"Cannot write {fileKind} without a safe file path.");

                writeFile();
                return true;
            }
            catch (UnauthorizedAccessException ex)
            {
                BlishWarnings.FileSaveBlocked(ex, path, $"save {description} data");
                logger.Warn(ex, "Failed to write {description} {fileKind} file at {path}.", description, fileKind, path);
                return false;
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "Failed to write {description} {fileKind} file at {path}.", description, fileKind, path);
                return false;
            }
        }

        // Keep file path inside proper directory in case something tries to push it out
        public static string GetSafePath(string directory, string key)
        {
            if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(key))
                return null;

            var safeFileName = GetSafeFileName(key.Trim()) + ".json";
            var fullDirectory = Path.GetFullPath(directory);
            var fullPath = Path.GetFullPath(Path.Combine(fullDirectory, safeFileName));
            var requiredPrefix = fullDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                               + Path.DirectorySeparatorChar;

            if (!fullPath.StartsWith(requiredPrefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("JSON path invalid.");

            return fullPath;
        }

        // Write to temp file, then replace file to safely save data and prevent corrupt data on crash or module being disabled with data in flight.
        private static void WriteText(string path, string text)
        {
            AtomicWrite(path, temporaryPath => File.WriteAllText(temporaryPath, text ?? string.Empty));
        }

        private static void WriteBytes(string path, byte[] bytes)
        {
            AtomicWrite(path, temporaryPath => File.WriteAllBytes(temporaryPath, bytes));
        }

        private static void AtomicWrite(string path, Action<string> writeTemporaryFile)
        {
            var directory = Path.GetDirectoryName(path);

            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
            writeTemporaryFile(temporaryPath);
            ReplaceFile(temporaryPath, path);
        }

        private static void ReplaceFile(string temporaryPath, string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Replace(temporaryPath, path, null);
                else
                    File.Move(temporaryPath, path);
            }
            catch
            {
                if (!File.Exists(temporaryPath))
                    throw;

                File.Copy(temporaryPath, path, true);
                File.Delete(temporaryPath);
            }
        }

        private static string GetSafeFileName(string value)
        {
            var safe = value ?? string.Empty;

            foreach (var invalidChar in Path.GetInvalidFileNameChars())
                safe = safe.Replace(invalidChar, '_');

            return string.IsNullOrWhiteSpace(safe)
                ? "unknown"
                : safe;
        }
    }
}
