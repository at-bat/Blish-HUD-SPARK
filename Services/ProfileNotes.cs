using Blish_HUD;
using Blish_HUD.Modules.Managers;
using rp.spark.Models;
using System;
using System.IO;

namespace rp.spark.Services
{
    public class ProfileNotes
    {
        private static readonly Logger Logger = Logger.GetLogger<ProfileNotes>();

        private readonly string _notesDirectory;

        public ProfileNotes(DirectoriesManager directoriesManager)
        {
            var sparkDirectory = directoriesManager.GetFullDirectoryPath("spark");
            _notesDirectory = Path.Combine(sparkDirectory, "profile-notes");

            FileStore.EnsureDirectory(_notesDirectory, Logger, "SPARK profile notes");
        }

        public ProfileNote Load(CharacterProfile profile, PlayerPresence presence)
        {
            var cacheKey = GetNoteKey(profile, presence);

            if (string.IsNullOrWhiteSpace(cacheKey))
                return null;

            return Load(cacheKey) ?? CreateEmptyNote(cacheKey, profile, presence);
        }

        public ProfileNote Save(CharacterProfile profile, PlayerPresence presence, string text)
        {
            var cacheKey = GetNoteKey(profile, presence);

            if (string.IsNullOrWhiteSpace(cacheKey))
                throw new InvalidOperationException("Cannot save profile notes without a profile identity.");

            var existing = Load(cacheKey);
            var now = DateTime.UtcNow;
            var note = existing ?? CreateEmptyNote(cacheKey, profile, presence);

            note.Text = text ?? string.Empty;
            note.UpdatedAt = now;

            if (note.CreatedAt == default)
                note.CreatedAt = now;

            ApplyIdentity(note, profile, presence);

            if (!FileStore.TryWrite(GetNotePath(cacheKey), note, Logger, "SPARK profile notes"))
                throw new InvalidOperationException("Could not save profile notes. Check file permissions and try again.");

            return note;
        }

        public static string GetNoteKey(CharacterProfile profile, PlayerPresence presence)
        {
            return ProfileCache.GetProfileCacheKey(profile, presence);
        }

        private ProfileNote Load(string cacheKey)
        {
            var note = FileStore.ReadFile<ProfileNote>(GetNotePath(cacheKey), Logger, "SPARK profile notes");

            if (note?.SchemaVersion == 1)
                return note;

            if (note != null)
                Logger.Warn("Skipping unsupported SPARK profile notes file for {cacheKey}.", cacheKey);

            return null;
        }

        private ProfileNote CreateEmptyNote(string cacheKey, CharacterProfile profile, PlayerPresence presence)
        {
            var note = new ProfileNote
            {
                CacheKey = cacheKey,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            ApplyIdentity(note, profile, presence);
            return note;
        }

        private static void ApplyIdentity(ProfileNote note, CharacterProfile profile, PlayerPresence presence)
        {
            if (note == null)
                return;

            note.ProfileId = TextUtil.FirstNonEmpty(profile?.ProfileId, presence?.ActiveProfileId);
            note.AccountName = TextUtil.FirstNonEmpty(profile?.AccountName, presence?.AccountName);
            note.OfficialCharacterName = TextUtil.FirstNonEmpty(profile?.CharacterName, presence?.OfficialCharacterName);
        }

        private string GetNotePath(string cacheKey)
        {
            return FileStore.GetSafePath(_notesDirectory, cacheKey);
        }
    }
}
