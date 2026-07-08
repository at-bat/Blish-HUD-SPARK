using Blish_HUD;
using Blish_HUD.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace rp.spark.UI.Views
{
    internal sealed class SparkSettingsButtons : IDisposable
    {
        private static readonly TimeSpan StatePollInterval = TimeSpan.FromSeconds(1);

        private readonly Action _openProfileManager;
        private readonly Action _openProfileViewer;
        private readonly Action _openOnlineList;
        private readonly Action _openSavedProfiles;
        private readonly Action _openAbout;
        private readonly Action _openNearby;
        private readonly Func<Task<string>> _waitForPlayerStateMessageAsync;
        private readonly Func<string> _getPlayerStateMessage;
        private readonly Action _reloadPlayerState;
        private readonly Func<string> _getMatureProfilesButtonText;
        private readonly Action _toggleMatureProfiles;

        private bool _isDisposed;
        private int _refreshId;
        private string _lastMessage;
        private CancellationTokenSource _statePollCancel;
        private Task _statePollTask;
        private StandardButton _openButton;
        private StandardButton _viewButton;
        private StandardButton _onlineListButton;
        private StandardButton _savedProfilesButton;
        private StandardButton _nearbyButton;
        private StandardButton _matureProfilesButton;
        private readonly Func<bool> _shouldHideGameplayWindows;

        public SparkSettingsButtons(
            Action openProfileManager,
            Action openProfileViewer,
            Action openOnlineList,
            Action openNearby,
            Action openSavedProfiles,
            Action openAbout,
            Func<string> getMatureProfilesButtonText,
            Action toggleMatureProfiles,
            Func<Task<string>> waitForPlayerStateMessageAsync,
            Func<string> getPlayerStateMessage,
            Action reloadPlayerState,
            Func<bool> shouldHideGameplayWindows)
        {
            _openProfileManager = openProfileManager;
            _openProfileViewer = openProfileViewer;
            _openOnlineList = openOnlineList;
            _openSavedProfiles = openSavedProfiles;
            _openAbout = openAbout;
            _openNearby = openNearby;
            _waitForPlayerStateMessageAsync = waitForPlayerStateMessageAsync;
            _getPlayerStateMessage = getPlayerStateMessage;
            _reloadPlayerState = reloadPlayerState;
            _shouldHideGameplayWindows = shouldHideGameplayWindows;
            _getMatureProfilesButtonText = getMatureProfilesButtonText;
            _toggleMatureProfiles = toggleMatureProfiles;
        }

        // Rebuilding to be a single view
        public void Build(Container buildPanel)
        {
            _isDisposed = false;

            var buttonStack = SparkFormLayout.AddAutoStack(buildPanel, 660, 6);

            var firstRow = SparkFormLayout.AddRow(buttonStack, 660, 30, 8);

            _openButton = SparkFormLayout.AddButton(firstRow, "Profile Editor", 122, 30, false);
            _openButton.Click += (s, e) => _openProfileManager();

            _viewButton = SparkFormLayout.AddButton(firstRow, "My Profile", 110, 30, false);
            _viewButton.Click += (s, e) => _openProfileViewer();

            _onlineListButton = SparkFormLayout.AddButton(firstRow, "Online List", 110, 30);
            _onlineListButton.Click += (s, e) => _openOnlineList();

            _nearbyButton = SparkFormLayout.AddButton(firstRow, "Nearby Players", 126, 30);
            _nearbyButton.Click += (s, e) => _openNearby();

            var secondRow = SparkFormLayout.AddRow(buttonStack, 660, 30, 8);

            _savedProfilesButton = SparkFormLayout.AddButton(secondRow, "Saved Profiles", 126, 30);
            _savedProfilesButton.Click += (s, e) => _openSavedProfiles();

            var aboutButton = SparkFormLayout.AddButton(secondRow, "About", 80, 30);
            aboutButton.Click += (s, e) => _openAbout();

            _matureProfilesButton = SparkFormLayout.AddButton(secondRow, MatureProfilesButtonText(), 190, 30);
            _matureProfilesButton.Click += (s, e) => _toggleMatureProfiles?.Invoke();

            WatchGameState();
            StartStatePolling();
            RefreshButtonText();
            RefreshButtonState(false);
        }

        private async Task RefreshButtonStateAsync(int refreshVersion)
        {
            string resultText;

            try
            {
                resultText = await _waitForPlayerStateMessageAsync();
            }
            catch
            {
                resultText = "Unavailable";
            }

            if (_isDisposed)
                return;

            SparkUiThread.Queue(() =>
            {
                if (_isDisposed || refreshVersion != _refreshId)
                    return;

                ApplyButtonState(resultText);
                RefreshButtonText();
            });
        }

        private void WatchGameState()
        {
            GameService.Gw2Mumble.IsAvailableChanged += OnGameStateChanged;
            GameService.Gw2Mumble.FinishedLoading += OnGameStateChanged;
            GameService.Gw2Mumble.PlayerCharacter.NameChanged += OnGameStateChanged;
            GameService.Gw2Mumble.CurrentMap.MapChanged += OnGameStateChanged;
            GameService.Gw2Mumble.UI.IsMapOpenChanged += OnGameStateChanged;
            GameService.GameIntegration.Gw2Instance.Gw2Started += OnGameStateChanged;
            GameService.GameIntegration.Gw2Instance.Gw2Closed += OnGameStateChanged;
            GameService.GameIntegration.Gw2Instance.IsInGameChanged += OnGameStateChanged;
        }

        private void UnwatchGameState()
        {
            GameService.Gw2Mumble.IsAvailableChanged -= OnGameStateChanged;
            GameService.Gw2Mumble.FinishedLoading -= OnGameStateChanged;
            GameService.Gw2Mumble.PlayerCharacter.NameChanged -= OnGameStateChanged;
            GameService.Gw2Mumble.CurrentMap.MapChanged -= OnGameStateChanged;
            GameService.Gw2Mumble.UI.IsMapOpenChanged -= OnGameStateChanged;
            GameService.GameIntegration.Gw2Instance.Gw2Started -= OnGameStateChanged;
            GameService.GameIntegration.Gw2Instance.Gw2Closed -= OnGameStateChanged;
            GameService.GameIntegration.Gw2Instance.IsInGameChanged -= OnGameStateChanged;
        }

        private void OnGameStateChanged(object sender, EventArgs e)
        {
            RefreshButtonState(true);
        }

        private string RefreshButtonText()
        {
            if (_isDisposed || _openButton == null || _viewButton == null)
                return _lastMessage;

            try
            {
                var message = _getPlayerStateMessage?.Invoke() ?? string.Empty;
                ApplyButtonState(message);
                return message;
            }
            catch
            {
                const string unavailableMessage = "Unavailable";
                ApplyButtonState(unavailableMessage);
                return unavailableMessage;
            }
        }

        public void RefreshMatureButtonText()
        {
            if (_matureProfilesButton != null)
                _matureProfilesButton.Text = MatureProfilesButtonText();
        }

        private string MatureProfilesButtonText()
        {
            try
            {
                return _getMatureProfilesButtonText?.Invoke() ?? "Mature Profiles";
            }
            catch
            {
                return "Mature Profiles";
            }
        }

        // Enhancing UX here to disable buttons when you can't access things yet based on feedback
        private void ApplyButtonState(string resultText)
        {
            if (_openButton == null || _viewButton == null)
                return;

            var hideGameplayWindows = ShouldHideGameplayWindows();
            var unavailableMessage = resultText ?? string.Empty;
            var canUseProfileTools = !hideGameplayWindows && string.IsNullOrWhiteSpace(unavailableMessage);

            _lastMessage = unavailableMessage;

            _openButton.Text = "Profile Editor";
            _openButton.Enabled = canUseProfileTools;

            _viewButton.Text = "My Profile";
            _viewButton.Enabled = canUseProfileTools;

            if (_onlineListButton != null)
                _onlineListButton.Enabled = !hideGameplayWindows;

            if (_savedProfilesButton != null)
                _savedProfilesButton.Enabled = !hideGameplayWindows;

            if (_nearbyButton != null)
                _nearbyButton.Enabled = !hideGameplayWindows;
        }

        private bool ShouldHideGameplayWindows()
        {
            try
            {
                return _shouldHideGameplayWindows?.Invoke() == true;
            }
            catch
            {
                return false;
            }
        }

        public void Refresh()
        {
            if (_isDisposed)
                return;

            RefreshButtonState(false);
        }

        private void RefreshButtonState(bool reloadPlayerState)
        {
            if (_isDisposed)
                return;

            if (reloadPlayerState)
            {
                try
                {
                    _reloadPlayerState?.Invoke();
                }
                catch
                {
                    // The async warmup path below will surface the unavailable state in the buttons.
                }
            }

            var refreshVersion = Interlocked.Increment(ref _refreshId);
            SparkUiThread.Queue(() => RefreshButtonText());
            _ = RefreshButtonStateAsync(refreshVersion);
        }

        private void StartStatePolling()
        {
            StopStatePolling();
            _statePollCancel = new CancellationTokenSource();
            _statePollTask = PollButtonStateAsync(_statePollCancel.Token);
        }

        private async Task PollButtonStateAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(StatePollInterval, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                if (_isDisposed)
                    return;

                SparkUiThread.Queue(() =>
                {
                    if (_isDisposed)
                        return;

                    var previousMessage = _lastMessage;
                    var currentMessage = RefreshButtonText();

                    if (!string.Equals(previousMessage, currentMessage, StringComparison.Ordinal)
                        && string.IsNullOrWhiteSpace(currentMessage))
                    {
                        RefreshButtonState(true);
                    }
                });
            }
        }

        private void StopStatePolling()
        {
            var cancellation = _statePollCancel;
            _statePollCancel = null;

            if (cancellation == null)
                return;

            try
            {
                cancellation.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Already disposed by an earlier unload path.
            }
            finally
            {
                var watcherTask = _statePollTask;
                _statePollTask = null;

                TaskCleanup.DisposeWhenComplete(watcherTask, cancellation);
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            StopStatePolling();
            UnwatchGameState();
        }
    }
}
