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
        private readonly Func<Task<string>> _waitForPlayerStateMessageAsync;
        private readonly Func<string> _getPlayerStateMessage;
        private readonly Action _reloadPlayerState;

        private bool _isDisposed;
        private int _refreshId;
        private string _lastMessage;
        private CancellationTokenSource _statePollCancel;
        private Task _statePollTask;
        private StandardButton _openButton;
        private StandardButton _viewButton;
        private StandardButton _onlineListButton;
        private StandardButton _savedProfilesButton;
        private readonly Func<bool> _shouldHideGameplayWindows;

        public SparkSettingsButtons(
            Action openProfileManager,
            Action openProfileViewer,
            Action openOnlineList,
            Action openSavedProfiles,
            Action openAbout,
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
            _waitForPlayerStateMessageAsync = waitForPlayerStateMessageAsync;
            _getPlayerStateMessage = getPlayerStateMessage;
            _reloadPlayerState = reloadPlayerState;
            _shouldHideGameplayWindows = shouldHideGameplayWindows;
        }

        public void Build(Container buildPanel)
        {
            SparkViewUI.AddLabel(buildPanel, "SPARK profile tools", 0, 0, 300, 30);

            _openButton = SparkViewUI.AddButton(buildPanel, "Loading...", 0, 40, 200, enabled: false);
            _openButton.Click += (s, e) => _openProfileManager();

            _viewButton = SparkViewUI.AddButton(buildPanel, "Loading...", 0, 85, 200, enabled: false);
            _viewButton.Click += (s, e) => _openProfileViewer();

            _onlineListButton = SparkViewUI.AddButton(buildPanel, "Open Online List", 220, 40, 200);
            _onlineListButton.Click += (s, e) => _openOnlineList();

            _savedProfilesButton = SparkViewUI.AddButton(buildPanel, "Open Saved Profiles", 220, 85, 200);
            _savedProfilesButton.Click += (s, e) => _openSavedProfiles();

            var aboutButton = SparkViewUI.AddButton(buildPanel, "About", 440, 40, 200);
            aboutButton.Click += (s, e) => _openAbout();

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
                resultText = "Profile tools unavailable";
            }

            if (_isDisposed)
                return;

            GameService.Overlay.QueueMainThreadUpdate(gameTime =>
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
                const string unavailableMessage = "Profile tools unavailable";
                ApplyButtonState(unavailableMessage);
                return unavailableMessage;
            }
        }

        // Enhancing UX here to disable buttons when you can't access things yet based on feedback
        private void ApplyButtonState(string resultText)
        {
            if (_openButton == null || _viewButton == null)
                return;

            var hideGameplayWindows = ShouldHideGameplayWindows();
            var unavailableMessage = resultText ?? string.Empty;
            var buttonMessage = hideGameplayWindows && string.IsNullOrWhiteSpace(unavailableMessage)
                ? "Profile tools unavailable"
                : unavailableMessage;
            var canUseProfileTools = !hideGameplayWindows && string.IsNullOrWhiteSpace(buttonMessage);

            _lastMessage = unavailableMessage;
            _openButton.Text = canUseProfileTools
                ? "Open Profile Editor"
                : buttonMessage;
            _openButton.Enabled = canUseProfileTools;

            _viewButton.Text = "View Your Profile";
            _viewButton.Enabled = canUseProfileTools;

            if (_onlineListButton != null)
                _onlineListButton.Enabled = !hideGameplayWindows;

            if (_savedProfilesButton != null)
                _savedProfilesButton.Enabled = !hideGameplayWindows;
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
            GameService.Overlay.QueueMainThreadUpdate(gameTime => RefreshButtonText());
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

                GameService.Overlay.QueueMainThreadUpdate(gameTime =>
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
