using Blish_HUD;
using rp.spark.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace rp.spark.Services
{
    public class PresenceLoop : IDisposable
    {
        private static readonly Logger Logger = Logger.GetLogger<PresenceLoop>();

        private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(15);
        private static readonly TimeSpan FirstRetryDelay = TimeSpan.FromSeconds(30);

        private readonly PresenceService _presenceService;
        private readonly SemaphoreSlim _refreshGate = new SemaphoreSlim(1, 1);

        private CancellationTokenSource _cancellation;
        private Task _worker;

        public PresenceLoop(PresenceService presenceService)
        {
            _presenceService = presenceService;
        }

        public PlayerPresence CurrentPresence { get; private set; }

        public DateTime LastRefreshedAt { get; private set; }

        public string LastStatus { get; private set; } = string.Empty;

        public bool IsRunning => _worker != null && !_worker.IsCompleted;

        public event Action<PlayerPresence> PresenceUpdated;

        public event Action<string> StatusChanged;

        public void Start()
        {
            if (IsRunning)
                return;

            Stop();
            _cancellation = new CancellationTokenSource();
            _worker = RunAsync(_cancellation.Token);
        }

        public void Stop()
        {
            var cancellation = _cancellation;
            var worker = _worker;
            _cancellation = null;
            _worker = null;

            if (cancellation == null)
                return;

            cancellation.Cancel();
            TaskCleanup.DisposeWhenComplete(worker, cancellation);
        }

        public async Task<PlayerPresence> RefreshAsync(CancellationToken cancellationToken = default)
        {
            await _refreshGate.WaitAsync(cancellationToken);

            try
            {
                var presence = await _presenceService.GetCurrentPresenceAsync(cancellationToken);

                CurrentPresence = presence;
                LastRefreshedAt = DateTime.UtcNow;
                SetStatus(presence?.CanShare == true
                    ? "Presence ready."
                    : presence?.ShareBlockReason ?? "Presence refreshed.");

                PresenceUpdated?.Invoke(presence);

                return presence;
            }
            finally
            {
                _refreshGate.Release();
            }
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var delay = RefreshInterval;

                try
                {
                    await RefreshAsync(cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "SPARK presence refresh failed.");
                    SetStatus("Presence refresh failed. Retrying later.");
                    delay = FirstRetryDelay;
                }

                try
                {
                    await Task.Delay(delay, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
            }
        }

        private void SetStatus(string status)
        {
            LastStatus = status ?? string.Empty;
            StatusChanged?.Invoke(LastStatus);
        }

        private bool _isDisposed;

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            var worker = _worker;
            Stop();
            TaskCleanup.DisposeWhenComplete(worker, _refreshGate);
        }
    }
}
