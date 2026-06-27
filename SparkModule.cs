using Blish_HUD;
using Blish_HUD.Graphics.UI;
using Blish_HUD.Modules;
using Blish_HUD.Modules.Managers;
using Blish_HUD.Settings;
using rp.spark.Models;
using rp.spark.Services;
using rp.spark.UI;
using rp.spark.UI.Views;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using TokenPermission = Gw2Sharp.WebApi.V2.Models.TokenPermission;
// SPARK Module General Details:
// The name stands for Simple Profile and Roleplay Kit. It's also a slight nod towards the first legendary I made, Incinerator, since Spark is the precursor.
// This module uses an external webserver to save profile and presence data, as well as pull other data from other players using SPARK
// Presence = a snippet of your profile for tooltips on the online list. This is things like status, out of character (OOC) info, character name, race, etc.
// Players can make multiple profiles tied to each of their characters.
// SPARK makes use of subtokens to identify someone with GW2's API based on Account+Character before uploading or transmitting data to them.


namespace rp.spark
{
    [Export(typeof(Blish_HUD.Modules.Module))]
    public class SparkModule : Blish_HUD.Modules.Module
    {
        private static readonly Logger Logger = Logger.GetLogger<SparkModule>();
        private static readonly TimeSpan GameplayVisibilityCheckInterval = TimeSpan.FromMilliseconds(250);
        
        // This is to check UI ticks, if they stop, game is likely on a load screen.
        private TimeSpan _gameplayVisibilityCheckElapsed;

        internal SettingsManager SettingsManager => this.ModuleParameters.SettingsManager;
        internal ContentsManager ContentsManager => this.ModuleParameters.ContentsManager;
        internal DirectoriesManager DirectoriesManager => this.ModuleParameters.DirectoriesManager;
        internal Gw2ApiManager Gw2ApiManager => this.ModuleParameters.Gw2ApiManager;

        private ProfileValidator _profileValidator;
        private ProfileRepository _profileRepository;
        private ProfileCache _profileCache;
        private ProfileNotes _notes;
        private PlayerStateService _playerState;
        private PresenceService _presenceService;
        private PresenceLoop _presenceLoop;
        private ServerSync _sync;
        private GW2TokenVerification _tokens;
        private SparkClient _sparkClient;
        private IconIndexService _iconIndex;
        private SparkSettings _sparkSettings;
        private ProfileActions _profileActions;
        private ProfileLoader _profileLoader;
        private ServiceHost _serviceHost;
        private SparkWindows _windows;
        private Task<PlayerState> _initialStateTask;
        private CancellationTokenSource _stateLoadCancel;

        // From the example module: Ideally you should keep the constructor as is (empty). Instead use LoadAsync() to handle initializing the module.
        [ImportingConstructor]
        public SparkModule([Import("ModuleParameters")] ModuleParameters moduleParameters) : base(moduleParameters) { }

        protected override void DefineSettings(SettingCollection settings)
        {
            _sparkSettings = new SparkSettings(settings);
        }

        protected override void Initialize()
        {
            _profileValidator = new ProfileValidator();
            _profileRepository = new ProfileRepository(this.DirectoriesManager, _profileValidator);
            _profileRepository.ProfileSaved += ProfileSaved;
            _profileRepository.ActiveProfileChanged += ActiveProfileChanged;

            _profileCache = new ProfileCache(this.DirectoriesManager, _profileValidator);
            _notes = new ProfileNotes(this.DirectoriesManager);
            _playerState = new PlayerStateService(this.Gw2ApiManager, _sparkSettings);
            _presenceService = new PresenceService(_profileRepository, _playerState, _sparkSettings);
            _sparkClient = new SparkClient(_sparkSettings.GetServerBaseUrl());
            _iconIndex = new IconIndexService(this.ContentsManager);
            _presenceLoop = new PresenceLoop(_presenceService);
            _tokens = new GW2TokenVerification(this.Gw2ApiManager);
            _sync = new ServerSync(
                _sparkClient,
                _sparkSettings,
                _presenceLoop,
                _profileRepository,
                _tokens);

            _profileActions = new ProfileActions(
                _profileCache,
                _sparkSettings,
                _playerState,
                _sparkClient,
                _tokens,
                _sync);
            _sync.SetPrivacyCheck(_profileActions.EnsureBlocksSyncedAsync);

            _profileLoader = new ProfileLoader(
                _profileRepository,
                _playerState,
                _presenceService,
                _presenceLoop,
                _sync,
                _sparkSettings,
                _profileActions);

            _serviceHost = new ServiceHost();
            _serviceHost.Add(_presenceLoop, service => service.Start());
            _serviceHost.Add(_tokens);
            _serviceHost.Add(_profileActions, service => service.Start());
            _serviceHost.Add(_sync, service => service.Start());

            _windows = new SparkWindows(
                new WindowBuilder(),
                _profileRepository,
                _profileCache,
                _notes,
                _playerState,
                _iconIndex,
                _sparkSettings,
                _profileLoader,
                _profileActions);
        }

        protected override Task LoadAsync()
        {
            return Task.CompletedTask;
        }

        // Doing a small check to track the UITick, if it halts, assume we're on a map load
        protected override void Update(GameTime gameTime)
        {
            if (_windows == null || gameTime == null)
                return;

            _gameplayVisibilityCheckElapsed += gameTime.ElapsedGameTime;

            if (_gameplayVisibilityCheckElapsed < GameplayVisibilityCheckInterval)
                return;

            _gameplayVisibilityCheckElapsed = TimeSpan.Zero;

            if (_windows.ShouldHideGameplayWindows())
                _windows.CloseGameplayWindows();
        }

        protected override void OnModuleLoaded(EventArgs e)
        {
            base.OnModuleLoaded(e);
            this.Gw2ApiManager.SubtokenUpdated += HandleSubtokenUpdated;
            GameService.Gw2Mumble.IsAvailableChanged += HandleMumbleAvailableChanged;
            GameService.GameIntegration.Gw2Instance.IsInGameChanged += HandleIsInGameChanged;
            GameService.Gw2Mumble.UI.IsMapOpenChanged += HandleMapOpenChanged;
            EnsurePlayerStateLoad();
            _serviceHost.Start();
        }

        public override IView GetSettingsView()
        {
            EnsurePlayerStateLoad();

            return new SparkSettingsView(
                _windows.OpenProfileManager,
                _windows.OpenMyProfile,
                _windows.OpenOnlineList,
                _windows.OpenSavedProfiles,
                _windows.OpenAbout,
                _windows.OpenBlocklist,
                WaitForPlayerStateAsync,
                GetPlayerStateMessage,
                ReloadPlayerState,
                _sparkSettings,
                GetServerSyncStatus,
                WatchServerSyncStatus,
                UnwatchServerSyncStatus,
                RefreshPresenceSoon,
                GetImportantSettingsNotice,
                _windows.ShouldHideGameplayWindows,
                CloseGameplayWindowsIfUnavailableSoon,
                _profileActions.WatchBlockedAccounts,
                _profileActions.UnwatchBlockedAccounts,
                _windows.HandleMaturePreferenceChanged);
        }

        private void ProfileSaved(CharacterProfile savedProfile)
        {
            RefreshPresenceSoon();

            SparkUiThread.Queue(() =>
            {
                if (_windows == null
                    || !_windows.IsProfileViewerVisible
                    || !_windows.IsViewingProfile(savedProfile))
                    return;

                var state = _playerState.GetCached();
                _windows.ShowProfileViewer(_profileLoader.BuildLocal(savedProfile, state));
            });
        }

        private void ActiveProfileChanged(string accountName, string officialCharacterName, string profileId)
        {
            RefreshPresenceSoon();

            SparkUiThread.Queue(() =>
            {
                if (_windows == null || !_windows.IsProfileViewerVisible)
                    return;

                if (!_windows.IsViewingCharacter(officialCharacterName))
                    return;

                var state = _playerState.GetCached();
                var profile = string.IsNullOrWhiteSpace(profileId)
                    ? null
                    : _profileRepository.Load(profileId);

                _windows.ShowProfileViewer(_profileLoader.BuildLocal(profile, state));
            });
        }

        private void EnsurePlayerStateLoad()
        {
            if (_initialStateTask == null || _initialStateTask.IsFaulted || _initialStateTask.IsCanceled || LoadedNoCharacter(_initialStateTask))
                LoadPlayerState();
        }

        private void ReloadPlayerState()
        {
            LoadPlayerState();
        }

        private async void RefreshPresenceSoon()
        {
            if (_presenceLoop == null)
                return;

            try
            {
                await _presenceLoop.RefreshAsync();
                SyncSoon();
            }
            catch (OperationCanceledException)
            {
                // This should be fine
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to refresh SPARK data after a profile change.");
            }
        }

        private void LoadPlayerState()
        {
            if (_playerState == null)
                return;

            CancelPlayerStateLoad();
            _stateLoadCancel = new CancellationTokenSource();
            _initialStateTask = _playerState.GetCurrentAsync(_stateLoadCancel.Token);
        }

        private void CancelPlayerStateLoad()
        {
            var cancellation = _stateLoadCancel;
            var stateTask = _initialStateTask;
            _stateLoadCancel = null;

            if (cancellation == null)
                return;

            try
            {
                cancellation.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Already disposed
            }
            finally
            {
                if (stateTask == null)
                {
                    cancellation.Dispose();
                }
                else
                {
                    TaskCleanup.DisposeWhenComplete(stateTask, cancellation);
                }
            }
        }

        private async void HandleSubtokenUpdated(object sender, ValueEventArgs<IEnumerable<TokenPermission>> e)
        {
            try
            {
                _tokens?.Clear();
                _profileActions?.SyncBlocks();
                ReloadPlayerState();
                var stateTask = _initialStateTask;

                if (stateTask != null)
                    await stateTask;
            }
            catch (OperationCanceledException)
            {
                // A newer state or module unload happened.
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to refresh SPARK player state after API subtoken update.");
            }
        }

        private void HandleMumbleAvailableChanged(object sender, ValueEventArgs<bool> e)
        {
            CloseGameplayWindowsIfUnavailableSoon();
            ReloadPlayerState();
        }

        private void HandleIsInGameChanged(object sender, ValueEventArgs<bool> e)
        {
            CloseGameplayWindowsIfUnavailableSoon();
            ReloadPlayerState();
        }

        private void HandleMapOpenChanged(object sender, ValueEventArgs<bool> e)
        {
            CloseGameplayWindowsIfUnavailableSoon();
        }

        private void CloseGameplayWindowsIfUnavailableSoon()
        {
            SparkUiThread.Queue(() =>
            {
                if (_windows?.ShouldHideGameplayWindows() == true)
                    _windows.CloseGameplayWindows();
            });
        }

        private static bool LoadedNoCharacter(Task<PlayerState> task)
        {
            return task != null
                && task.IsCompleted
                && !task.IsFaulted
                && !task.IsCanceled
                && !task.Result.CanEditProfile;
        }

        private async Task<string> WaitForPlayerStateAsync()
        {
            EnsurePlayerStateLoad();
            var stateTask = _initialStateTask;

            if (stateTask == null)
                return "Profile tools unavailable";

            try
            {
                var state = await stateTask;

                return GetUnavailableReason(state);
            }
            catch (OperationCanceledException)
            {
                return "Loading character info...";
            }
            catch
            {
                return "Profile tools unavailable";
            }
        }

        private string GetPlayerStateMessage()
        {
            return GetUnavailableReason(_playerState.GetCached());
        }

        private ServerSyncStatus GetServerSyncStatus()
        {
            var setupStatus = GetProfileSetupStatus();

            if (setupStatus != null)
                return setupStatus;

            return _sync?.CurrentStatus
                   ?? ServerSyncStatus.Disconnected("Server sync is not connected.");
        }

        private ServerSyncStatus GetProfileSetupStatus()
        {
            if (_playerState == null || _profileRepository == null)
                return null;

            var state = _playerState.GetCached();

            if (state == null || !state.CanEditProfile)
                return null;

            var profiles = _profileRepository.ListForCharacter(
                state.AccountName,
                state.OfficialCharacterName);

            if (profiles.Count == 0)
            {
                return new ServerSyncStatus(
                    ServerSyncState.Info,
                    "Click 'Open Profile Editor' and make your first profile to begin!");
            }

            var activeProfile = _profileRepository.LoadActiveForCharacter(
                state.AccountName,
                state.OfficialCharacterName);

            if (activeProfile == null)
            {
                return new ServerSyncStatus(
                    ServerSyncState.Info,
                    "No active profile. Open Profile Editor, pick a profile, and click 'Set Active'.");
            }

            return null;
        }

        private void WatchServerSyncStatus(Action<ServerSyncStatus> handler)
        {
            if (_sync != null && handler != null)
                _sync.StatusChanged += handler;
        }

        private void UnwatchServerSyncStatus(Action<ServerSyncStatus> handler)
        {
            if (_sync != null && handler != null)
                _sync.StatusChanged -= handler;
        }

        private void SyncSoon()
        {
            _sync?.SyncSoon();
        }

        private bool HasValidApiKey()
        {
            try
            {
                var state = _playerState?.GetCached();

                // Suppress message if you aren't loaded onto a character to prevent confusion
                if (state == null || !state.CanEditProfile)
                    return true;

                return _tokens?.HasValidApiKey() == true;
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to check SPARK GW2 API key status.");
                return false;
            }
        }

        private string GetImportantSettingsNotice()
        {
            if (IsGameplayUiBlockingSpark())
                return "SPARK windows closed due to map, vista, or other game UI.";

            var state = _playerState?.GetCached();
            var unavailableReason = GetUnavailableReason(state);

            if (!string.IsNullOrWhiteSpace(unavailableReason))
                return unavailableReason;

            return GetApiStatus(state);
        }

        private bool IsGameplayUiBlockingSpark()
        {
            if (_windows?.ShouldHideGameplayWindows() != true)
                return false;

            if (GameService.Gw2Mumble.UI.IsMapOpen)
                return true;

            var state = _playerState?.GetCached();

            return state != null
                && state.IsMumbleAvailable
                && !string.IsNullOrWhiteSpace(state.OfficialCharacterName);
        }

        private static string GetUnavailableReason(PlayerState state)
        {
            if (state == null)
                return "Load into the game on a character to use SPARK.";

            if (state.CanEditProfile)
                return string.Empty;

            return SparkWindows.IsLoadingScreen()
                ? "SPARK profile tools are unavailable during loading screens or character select."
                : "Load into the game on a character to use SPARK.";
        }

        // Adding new status messages based on API for clarity
        private string GetApiStatus(PlayerState state)
        {
            var stateTask = _initialStateTask;

            if (stateTask != null && !stateTask.IsCompleted)
            {
                return _profileActions?.IsBlockSyncInProgress == true
                    ? "Checking GW2 API access..."
                    : "Checking GW2 API account and character permissions...";
            }

            if (!HasValidApiKey())
                return "Waiting for GW2 API access from Blish HUD. Add an API key with account and characters permissions.";

            if (state == null || !state.HasCharactersPermission)
                return "Refreshing GW2 API permissions...";

            if (string.IsNullOrWhiteSpace(state.AccountName))
                return "Waiting for GW2 account verification...";

            if (!state.IsCharacterApiVerified)
                return "Waiting for current character verification from the GW2 API...";

            if (_profileActions?.IsBlockSyncInProgress == true)
                return "Syncing SPARK settings...";

            return string.Empty;
        }

        protected override void Unload()
        {
            this.Gw2ApiManager.SubtokenUpdated -= HandleSubtokenUpdated;
            GameService.Gw2Mumble.IsAvailableChanged -= HandleMumbleAvailableChanged;
            GameService.GameIntegration.Gw2Instance.IsInGameChanged -= HandleIsInGameChanged;
            GameService.Gw2Mumble.UI.IsMapOpenChanged -= HandleMapOpenChanged;
            CancelPlayerStateLoad();
            _initialStateTask = null;
            _windows?.Dispose();
            _windows = null;
            _serviceHost?.Dispose();
            _serviceHost = null;
            _iconIndex?.Dispose();

            if (_profileRepository != null)
            {
                _profileRepository.ProfileSaved -= ProfileSaved;
                _profileRepository.ActiveProfileChanged -= ActiveProfileChanged;
            }

            _profileLoader = null;
            _profileActions = null;
            _sync = null;
            _iconIndex = null;
            _presenceLoop = null;
            _sparkClient = null;
            _presenceService = null;
            _tokens = null;
            _playerState = null;
            _sparkSettings = null;
            _notes = null;
            _profileCache = null;
            _profileRepository = null;
            _profileValidator = null;
        }
    }
}
