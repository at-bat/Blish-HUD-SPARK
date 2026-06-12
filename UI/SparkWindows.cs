using Blish_HUD;
using Blish_HUD.Controls;
using Microsoft.Xna.Framework;
using rp.spark.Models;
using rp.spark.Services;
using rp.spark.UI.Views;
using System;

namespace rp.spark.UI
{
    internal sealed class SparkWindows : IDisposable
    {
        private const int ProfileSelectionIcon = 156727;
        private const int EditProfileIcon = 156714;
        private const int ProfilePrefsIcon = 155052;
        private const int CurrentInfoIcon = 440023;
        private const int RecentlySeenIcon = 156680;
        private const int BookmarkIcon = 156722;

        private readonly WindowBuilder _windowBuilder;
        private readonly ProfileRepository _profileRepository;
        private readonly ProfileCache _profileCache;
        private readonly ProfileNotes _notes;
        private readonly PlayerStateService _playerState;
        private readonly IconIndexService _iconIndexService;
        private readonly SparkSettings _settings;
        private readonly ProfileLoader _profileLoader;
        private readonly ProfileActions _profileActions;

        private ProfileEditorSession _profileEditorSession;
        private TabbedWindow2 _profileWindow;
        private TabbedWindow2 _savedProfilesWindow;
        private TabbedWindow2 _profileViewerWindow;
        private ProfileViewerView _profileViewerView;
        private ProfileNotesView _profileNotesView;
        private StandardWindow _onlineListWindow;
        private StandardWindow _aboutWindow;
        private CharacterProfile _viewedProfile;
        private PlayerPresence _viewedPresence;

        public SparkWindows(
            WindowBuilder windowBuilder,
            ProfileRepository profileRepository,
            ProfileCache profileCache,
            ProfileNotes notes,
            PlayerStateService playerState,
            IconIndexService iconIndexService,
            SparkSettings settings,
            ProfileLoader profileLoader,
            ProfileActions profileActions)
        {
            _windowBuilder = windowBuilder;
            _profileRepository = profileRepository;
            _profileCache = profileCache;
            _notes = notes;
            _playerState = playerState;
            _iconIndexService = iconIndexService;
            _settings = settings;
            _profileLoader = profileLoader;
            _profileActions = profileActions;
        }

        public string ViewedProfileId { get; private set; }

        public string ViewedOfficialCharacterName { get; private set; }

        public bool IsProfileViewerVisible => _profileViewerWindow != null && _profileViewerWindow.Visible;

        private static readonly Logger Logger = Logger.GetLogger<SparkWindows>();
        private bool _isDisposed;

        internal bool ShouldHideGameplayWindows()
        {
            return !GameService.GameIntegration.Gw2Instance.IsInGame
                || ShouldHideForGameUi();
        }

        private bool ShouldHideForGameUi()
        {
            return (_settings?.AutoHideGameUi.Value ?? true)
                && GameService.Gw2Mumble.UI.IsMapOpen;
        }

        private bool CanShowGameplayWindow()
        {
            return !ShouldHideGameplayWindows();
        }

        public void OpenProfileManager()
        {
            if (!CanShowGameplayWindow())
                return;

            var state = _playerState.GetCached();

            if (_profileWindow != null && _profileWindow.Visible)
            {
                _profileWindow.BringWindowToFront();
                return;
            }

            _profileEditorSession = new ProfileEditorSession(_profileRepository, _playerState, state);
            CreateProfileWindow();
            _profileWindow.Show();
        }

        public void OpenMyProfile()
        {
            if (!CanShowGameplayWindow())
                return;

            ShowProfileViewer(_profileLoader.LoadMyProfile());
        }

        public void OpenOnlineList()
        {
            if (!CanShowGameplayWindow())
                return;

            if (_onlineListWindow == null)
                CreateOnlineListWindow();

            _onlineListWindow.Show(new OnlineProfilesView(
                _profileLoader.LoadOnlineAsync,
                OpenPresence,
                _profileActions.IsPresenceBookmarked,
                _profileActions.WatchSavedProfiles,
                _profileActions.UnwatchSavedProfiles));
        }

        public void OpenSavedProfiles()
        {
            if (!CanShowGameplayWindow())
                return;

            if (_savedProfilesWindow != null && _savedProfilesWindow.Visible)
            {
                _savedProfilesWindow.BringWindowToFront();
                return;
            }

            if (_savedProfilesWindow == null)
                CreateSavedWindow();

            _savedProfilesWindow.Show();
        }

        public void OpenAbout()
        {
            if (_aboutWindow != null && _aboutWindow.Visible)
            {
                _aboutWindow.BringWindowToFront();
                return;
            }

            if (_aboutWindow == null)
                CreateAboutWindow();

            _aboutWindow.Show(new SparkAboutView());
        }

        public void ShowProfileViewer(ProfileViewData viewData)
        {
            if (viewData == null)
                return;

            ShowProfileViewer(viewData.Profile, viewData.Presence);
        }

        public void ShowProfileViewer(CharacterProfile profile, PlayerPresence presence)
        {
            if (!CanShowGameplayWindow())
                return;

            if (profile == null)
                profile = new CharacterProfile();

            if (presence == null)
                presence = new PlayerPresence();

            if (_profileViewerWindow == null)
                CreateViewerWindow();

            _viewedProfile = profile;
            _viewedPresence = presence;
            ViewedProfileId = profile.ProfileId;
            ViewedOfficialCharacterName = profile.CharacterName;

            _profileActions.SaveToRecent(profile, presence);

            _profileViewerView?.SetProfile(profile, presence);
            _profileNotesView?.SetProfile(profile, presence);
            _profileViewerWindow.Show();
        }

        public bool IsViewingProfile(CharacterProfile profile)
        {
            if (profile == null)
                return false;

            return !string.IsNullOrWhiteSpace(ViewedProfileId)
                && string.Equals(ViewedProfileId, profile.ProfileId, StringComparison.OrdinalIgnoreCase);
        }

        public bool IsViewingCharacter(string officialCharacterName)
        {
            return !string.IsNullOrWhiteSpace(ViewedOfficialCharacterName)
                && string.Equals(
                    ViewedOfficialCharacterName.Trim(),
                    officialCharacterName?.Trim(),
                    StringComparison.OrdinalIgnoreCase);
        }

        private void CreateProfileWindow()
        {
            _windowBuilder.DisposeWindow(_profileWindow);
            _profileWindow = _windowBuilder.MakeTabbedWindow("Profile Editor", "rp.spark.profile-window");

            _profileWindow.Tabs.Add(new Tab(
                _windowBuilder.IconFromAsset(ProfileSelectionIcon),
                () => new ProfileManagementView(_profileEditorSession),
                "Profile Selection",
                100));

            _profileWindow.Tabs.Add(new Tab(
                _windowBuilder.IconFromAsset(EditProfileIcon),
                () => new ProfileEditorView(_profileEditorSession, _iconIndexService),
                "Edit Profile",
                110));

            _profileWindow.Tabs.Add(new Tab(
                _windowBuilder.IconFromAsset(ProfilePrefsIcon),
                () => new ProfileEditorPreferencesView(_profileEditorSession),
                "Preferences",
                120));

            _profileWindow.Tabs.Add(new Tab(
                _windowBuilder.IconFromAsset(CurrentInfoIcon),
                () => new ProfileEditorCurrentInfoView(_profileEditorSession),
                "Current Info",
                130));
        }

        private void CreateViewerWindow()
        {
            _profileViewerWindow = _windowBuilder.MakeTabbedWindow("Profile Viewer", "rp.spark.profile-viewer-window");

            _profileViewerWindow.Tabs.Add(new Tab(
                _windowBuilder.IconFromAsset(EditProfileIcon),
                () =>
                {
                    _profileViewerView = new ProfileViewerView(
                        _viewedProfile,
                        _viewedPresence,
                        _profileActions.ToggleProfileBookmark,
                        _profileActions.IsProfileBookmarked,
                        _profileActions.ToggleProfileBlock,
                        _profileActions.IsProfileBlocked,
                        _profileActions.ReportProfile);

                    return _profileViewerView;
                },
                "View Profile",
                100));

            _profileViewerWindow.Tabs.Add(new Tab(
                _windowBuilder.IconFromAsset(ProfileSelectionIcon),
                () =>
                {
                    _profileNotesView = new ProfileNotesView(
                        _notes,
                        _viewedProfile,
                        _viewedPresence);

                    return _profileNotesView;
                },
                "Notes",
                110));
        }

        private void CreateOnlineListWindow()
        {
            _onlineListWindow = _windowBuilder.MakeWindow(
                "Online Profiles",
                "rp.spark.online-list-window",
                new Rectangle(70, 60, 839, 610));
        }

        private void CreateSavedWindow()
        {
            _savedProfilesWindow = _windowBuilder.MakeTabbedWindow("Saved Profiles", "rp.spark.saved-profiles-window");

            _savedProfilesWindow.Tabs.Add(new Tab(
                _windowBuilder.IconFromAsset(RecentlySeenIcon),
                () => new SavedProfilesView(
                    _profileCache.ListRecent,
                    OpenSavedProfile,
                    SavedProfilesMode.Recent,
                    _settings.IsBlockedAccount,
                    null,
                    _profileActions.WatchSavedProfiles,
                    _profileActions.UnwatchSavedProfiles),
                "Recent",
                100));

            _savedProfilesWindow.Tabs.Add(new Tab(
                _windowBuilder.IconFromAsset(BookmarkIcon),
                () => new SavedProfilesView(
                    _profileCache.ListBookmarked,
                    OpenSavedProfile,
                    SavedProfilesMode.Bookmarks,
                    _settings.IsBlockedAccount,
                    _profileActions.RemoveBookmark,
                    _profileActions.WatchSavedProfiles,
                    _profileActions.UnwatchSavedProfiles),
                "Bookmarks",
                110));
        }

        private async void OpenPresence(PlayerPresence presence)
        {
            try
            {
                var viewData = await _profileLoader.LoadOnlineProfileAsync(presence);

                if (_isDisposed)
                    return;

                ShowProfileViewer(viewData);
            }
            catch (OperationCanceledException)
            {
                // Window or module closed while profile was loading probably
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to open SPARK profile");
            }
        }

        private async void OpenSavedProfile(SavedProfileSummary summary)
        {
            try
            {
                var record = _profileCache.Load(summary?.CacheKey);
                var viewData = await _profileLoader.LoadSavedProfileAsync(record);

                if (_isDisposed)
                    return;

                ShowProfileViewer(viewData);
            }
            catch (OperationCanceledException)
            {
                // Window/module closed while profile was loading probably
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to open saved SPARK profile");
            }
        }

        private void CreateAboutWindow()
        {
            _aboutWindow = _windowBuilder.MakeWindow(
                "About",
                "rp.spark.about-window",
                new Rectangle(70, 60, 760, 520));
        }

        public void CloseGameplayWindows()
        {
            _windowBuilder.DisposeWindow(_profileWindow);
            _profileWindow = null;
            _profileEditorSession = null;

            _windowBuilder.DisposeWindow(_savedProfilesWindow);
            _savedProfilesWindow = null;

            _windowBuilder.DisposeWindow(_profileViewerWindow);
            _profileViewerWindow = null;
            _profileViewerView = null;
            _profileNotesView = null;

            _windowBuilder.DisposeWindow(_onlineListWindow);
            _onlineListWindow = null;

            _viewedProfile = null;
            _viewedPresence = null;
            ViewedProfileId = null;
            ViewedOfficialCharacterName = null;
        }

        public void Dispose()
        {
            _isDisposed = true;

            CloseGameplayWindows();

            _windowBuilder.DisposeWindow(_aboutWindow);
            _aboutWindow = null;
            
            _windowBuilder.Clear();
        }
    }
}
