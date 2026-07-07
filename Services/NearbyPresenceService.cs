using Blish_HUD;
using rp.spark.Models;
using rp.spark.Models.Api;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace rp.spark.Services
{
    public class NearbyPresenceService : IDisposable
    {
        private static readonly TimeSpan PublishInterval = TimeSpan.FromSeconds(10);

        private readonly SparkClient _apiClient;
        private readonly SparkSettings _settings;
        private readonly PresenceLoop _presenceLoop;
        private readonly GW2TokenVerification _tokens;
        private readonly SemaphoreSlim _publishGate = new SemaphoreSlim(1, 1);

        private CancellationTokenSource _loopCancellation;
        private Task _loopTask;
        private bool _published;

        public string LastStatus { get; private set; } = string.Empty;
        public int CurrentMapId { get; private set; }
        public uint CurrentShardId { get; private set; }
        public string CurrentServerAddress { get; private set; } = string.Empty;

        public NearbyPresenceService(
            SparkClient apiClient,
            SparkSettings settings,
            PresenceLoop presenceLoop,
            GW2TokenVerification tokens)
        {
            _apiClient = apiClient;
            _settings = settings;
            _presenceLoop = presenceLoop;
            _tokens = tokens;
        }

        public void Start()
        {
            if (_loopTask != null && !_loopTask.IsCompleted)
                return;

            _loopCancellation = new CancellationTokenSource();
            _loopTask = RunAsync(_loopCancellation.Token);
        }

        public void Stop()
        {
            _loopCancellation?.Cancel();
            _loopCancellation?.Dispose();
            _loopCancellation = null;
            _loopTask = null;
        }

        public async Task<IReadOnlyList<NearbyPresence>> SearchAsync(CancellationToken cancellationToken = default)
        {
            var query = BuildSearchRequest();
            if (query == null)
                return Array.Empty<NearbyPresence>();

            var token = await GetTokenAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(token))
            {
                LastStatus = "Add a valid GW2 API key before checking nearby RPers.";
                return Array.Empty<NearbyPresence>();
            }

            var result = await _apiClient.SearchNearbyPresenceResultAsync(query, token, cancellationToken).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                LastStatus = result.ErrorMessage ?? "Unable to refresh nearby RPers.";
                return Array.Empty<NearbyPresence>();
            }

            LastStatus = "Nearby RPers updated.";
            return (result.Value?.Entries ?? new List<NearbyPresence>())
                .Where(entry => entry?.Presence != null)
                .ToList();
        }

        public async Task PublishNowAsync(CancellationToken cancellationToken = default)
        {
            await _publishGate.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                var nearby = await BuildNearbyPresenceAsync(cancellationToken).ConfigureAwait(false);
                if (nearby == null)
                {
                    if (_published)
                        await RemoveAsync(cancellationToken).ConfigureAwait(false);

                    return;
                }

                var token = await GetTokenAsync(cancellationToken).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(token))
                {
                    LastStatus = "Add a valid GW2 API key before sharing nearby presence.";
                    return;
                }

                var result = await _apiClient.PublishNearbyPresenceResultAsync(nearby, token, cancellationToken).ConfigureAwait(false);
                _published = result.Succeeded;
                LastStatus = result.Succeeded
                    ? "Nearby presence shared."
                    : result.ErrorMessage ?? "Unable to share nearby presence.";
            }
            finally
            {
                _publishGate.Release();
            }
        }

        public async Task RemoveAsync(CancellationToken cancellationToken = default)
        {
            var token = await GetTokenAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(token))
                return;

            var result = await _apiClient.RemoveNearbyPresenceResultAsync(token, cancellationToken).ConfigureAwait(false);
            if (result.Succeeded)
                _published = false;

            LastStatus = result.Succeeded
                ? "Nearby presence hidden."
                : result.ErrorMessage ?? "Unable to hide nearby presence.";
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (_settings.ShowNearbyPresence.Value)
                        await PublishNowAsync(cancellationToken).ConfigureAwait(false);
                    else if (_published)
                        await RemoveAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    LastStatus = ex.Message;
                }

                try
                {
                    await Task.Delay(PublishInterval, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private async Task<NearbyPresence> BuildNearbyPresenceAsync(CancellationToken cancellationToken)
        {
            var presence = await GetPresenceAsync(cancellationToken).ConfigureAwait(false);
            if (presence == null || !presence.CanShare || presence.IsLocationHidden || presence.Status == RPStatus.Invisible)
            {
                LastStatus = "Nearby presence is not shared while your profile/location is hidden.";
                return null;
            }

            if (!TryGetLocation(out var mapId, out var shardId, out var serverAddress, out var x, out var y, out var z))
                return null;

            return new NearbyPresence
            {
                Presence = presence,
                MapId = mapId,
                ShardId = shardId,
                ServerAddress = serverAddress,
                HasPosition = true,
                X = x,
                Y = y,
                Z = z,
                LastSeen = DateTime.UtcNow
            };
        }

        private NearbyPresenceSearchRequest BuildSearchRequest()
        {
            if (!TryGetLocation(out var mapId, out var shardId, out var serverAddress, out var x, out var y, out var z))
                return null;

            return new NearbyPresenceSearchRequest
            {
                Region = _settings.RegionFilter.Value,
                IncludeMature = _settings.ShowMatureProfiles.Value,
                MapId = mapId,
                ShardId = shardId,
                ServerAddress = serverAddress,
                HasPosition = true,
                X = x,
                Y = y,
                Z = z,
                MaxDistanceMeters = 600
            };
        }

        private bool TryGetLocation(
            out int mapId,
            out uint shardId,
            out string serverAddress,
            out double x,
            out double y,
            out double z)
        {
            mapId = 0;
            shardId = 0;
            serverAddress = string.Empty;
            x = y = z = 0;

            CurrentMapId = 0;
            CurrentShardId = 0;
            CurrentServerAddress = string.Empty;

            if (_settings.HideLocation.Value)
            {
                LastStatus = "Nearby RPers are unavailable while location is hidden.";
                return false;
            }

            if (!GameService.Gw2Mumble.IsAvailable)
            {
                LastStatus = "GW2 location data is not available yet.";
                return false;
            }

            mapId = GameService.Gw2Mumble.CurrentMap.Id;
            shardId = GameService.Gw2Mumble.Info.ShardId;
            serverAddress = Convert.ToString(GameService.Gw2Mumble.Info.ServerAddress)?.Trim() ?? string.Empty;

            var position = GameService.Gw2Mumble.PlayerCharacter.Position;
            x = position.X;
            y = position.Y;
            z = position.Z;

            if (mapId <= 0 || shardId == 0)
            {
                LastStatus = "Enter a map before checking nearby RPers.";
                return false;
            }

            CurrentMapId = mapId;
            CurrentShardId = shardId;
            CurrentServerAddress = serverAddress;

            return true;
        }

        private async Task<PlayerPresence> GetPresenceAsync(CancellationToken cancellationToken)
        {
            if (_presenceLoop?.CurrentPresence != null)
                return _presenceLoop.CurrentPresence;

            return _presenceLoop == null
                ? null
                : await _presenceLoop.RefreshAsync(cancellationToken).ConfigureAwait(false);
        }

        public bool IsCurrentMapIp(NearbyPresence nearby)
        {
            if (nearby == null)
                return false;

            if (!string.IsNullOrWhiteSpace(CurrentServerAddress)
                && !string.IsNullOrWhiteSpace(nearby.ServerAddress))
            {
                return string.Equals(
                    nearby.ServerAddress.Trim(),
                    CurrentServerAddress.Trim(),
                    StringComparison.OrdinalIgnoreCase);
            }

            return CurrentShardId != 0 && nearby.ShardId == CurrentShardId;
        }

        private Task<string> GetTokenAsync(CancellationToken cancellationToken)
        {
            return _tokens.GetTokenAsync(cancellationToken);
        }

        public void Dispose()
        {
            Stop();
            _publishGate.Dispose();
        }
    }
}