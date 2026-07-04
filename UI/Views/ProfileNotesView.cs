using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Graphics.UI;
using Microsoft.Xna.Framework;
using rp.spark.Models;
using rp.spark.Services;
using rp.spark.UI.Controls;
using System;

namespace rp.spark.UI.Views
{
    // These are stored locally, never sent to the server
    public class ProfileNotesView : View
    {
        private const int MaxNoteLength = 8000;
        private static readonly Logger Logger = Logger.GetLogger<ProfileNotesView>();

        private readonly ProfileNotes _notesRepository;

        private CharacterProfile _profile;
        private PlayerPresence _presence;
        private Label _title;
        private SparkMultiline _notesBox;
        private Label _status;
        private bool _isLoading;
        private bool _isDirty;

        public ProfileNotesView(
            ProfileNotes notesRepository,
            CharacterProfile profile,
            PlayerPresence presence)
        {
            _notesRepository = notesRepository;
            RememberProfile(profile, presence);
        }

        protected override void Build(Container buildPanel)
        {
            _title = new Label
            {
                Text = GetTitleText(),
                Font = GameService.Content.DefaultFont18,
                TextColor = Color.White,
                StrokeText = true,
                Location = new Point(0, 0),
                Size = new Point(760, 30),
                Parent = buildPanel
            };

            _notesBox = new SparkMultiline
            {
                PlaceholderText = "Private notes for this profile.",
                MaxLength = MaxNoteLength,
                Location = new Point(0, 40),
                Size = new Point(760, 430),
                Parent = buildPanel
            };

            _notesBox.AttachWheelSource(buildPanel);

            _notesBox.TextChanged += (s, e) =>
            {
                if (_isLoading)
                    return;

                _isDirty = true;
                SetStatusText(GetDraftStatusText());
            };

            var saveButton = new StandardButton
            {
                Text = "Save Notes",
                Location = new Point(0, 490),
                Size = new Point(120, 35),
                Parent = buildPanel
            };

            saveButton.Click += (s, e) => SaveNotes();

            var clearButton = new StandardButton
            {
                Text = "Clear",
                Location = new Point(130, 490),
                Size = new Point(90, 35),
                Parent = buildPanel
            };

            clearButton.Click += (s, e) =>
            {
                _notesBox.Text = string.Empty;
                SaveNotes();
            };

            _status = new Label
            {
                Text = string.Empty,
                Font = GameService.Content.DefaultFont12,
                TextColor = new Color(220, 220, 220),
                Location = new Point(0, 535),
                Size = new Point(760, 24),
                Parent = buildPanel
            };

            LoadNotes();
        }

        // Save the note draft before we switch to another profile
        public void SetProfile(CharacterProfile profile, PlayerPresence presence)
        {
            SaveIfDirty();
            RememberProfile(profile, presence);

            if (_title != null)
                _title.Text = GetTitleText();

            if (_notesBox != null)
                LoadNotes();
        }

        private void RememberProfile(CharacterProfile profile, PlayerPresence presence)
        {
            _profile = profile ?? new CharacterProfile();
            _presence = presence ?? new PlayerPresence();
        }

        private void LoadNotes()
        {
            _isLoading = true;

            try
            {
                var note = _notesRepository?.Load(_profile, _presence);

                if (_notesBox != null)
                    _notesBox.Text = note?.Text ?? string.Empty;

                _isDirty = false;
                SetStatusText(GetLoadedStatusText(note));
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to load SPARK private notes for this profile.");
                SetStatusText("Couldn't load notes for this profile.");
            }
            finally
            {
                _isLoading = false;
            }
        }

        private void SaveNotes()
        {
            if (_notesBox == null)
                return;

            try
            {
                var note = _notesRepository?.Save(_profile, _presence, _notesBox.Text ?? string.Empty);
                _isDirty = false;
                SetStatusText(GetSavedStatusText(note));
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to save SPARK private notes for this profile.");
                SetStatusText("Couldn't save notes for this profile.");
            }
        }

        private void SaveIfDirty()
        {
            if (_isDirty)
                SaveNotes();
        }

        private string GetTitleText()
        {
            var characterName = _presence.VisibleName();

            if (string.IsNullOrWhiteSpace(characterName))
                characterName = _profile.DisplayName;

            if (string.IsNullOrWhiteSpace(characterName))
                characterName = _profile.CharacterName;

            return string.IsNullOrWhiteSpace(characterName)
                ? "Private Notes"
                : $"Private Notes: {characterName.Trim()}";
        }

        private string GetLoadedStatusText(ProfileNote note)
        {
            if (string.IsNullOrWhiteSpace(ProfileNotes.GetNoteKey(_profile, _presence)))
                return "Notes unavailable for this profile.";

            if (note == null || note.UpdatedAt == default || string.IsNullOrEmpty(note.Text))
                return "No notes saved yet.";

            return $"Last saved {ProfileText.FormatShortTime(note.UpdatedAt)}.";
        }

        private string GetSavedStatusText(ProfileNote note)
        {
            return note == null
                ? "Notes saved."
                : $"Notes saved {ProfileText.FormatShortTime(note.UpdatedAt)}.";
        }

        private string GetDraftStatusText()
        {
            var length = Math.Min((_notesBox?.Text ?? string.Empty).Length, MaxNoteLength);

            return $"Unsaved notes. {length}/{MaxNoteLength}";
        }

        private void SetStatusText(string text)
        {
            if (_status != null)
                _status.Text = text ?? string.Empty;
        }

        protected override void Unload()
        {
            SaveIfDirty();
            _title = null;
            _notesBox = null;
            _status = null;
            _isDirty = false;
        }
    }
}
