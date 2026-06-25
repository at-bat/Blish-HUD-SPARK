using Blish_HUD;
using Blish_HUD.Modules.Managers;
using Gw2Sharp.WebApi.V2.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace rp.spark.Services
{
    // For authentication reasons, we use a short-lived GW2 subtoken (if there's a better way to handle this, please let me know)
    // Only need account + character permissions
    // These expire quickly and are cached in memory and cleared when states change
    // More explanation can be found in SparkClient.cs
    public class GW2TokenVerification : IDisposable
    {
        private static readonly Logger Logger = Logger.GetLogger<GW2TokenVerification>();
        private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan RefreshSkew = TimeSpan.FromMinutes(1);

        private readonly Gw2ApiManager _gw2ApiManager;
        private readonly SemaphoreSlim _tokenGate = new SemaphoreSlim(1, 1);

        private string _cachedToken = string.Empty;
        private DateTimeOffset _expiresAt = DateTimeOffset.MinValue;

        // Fixing small race condition with the cache token so a late one already in flight can't write after clear
        private readonly object _cacheLock = new object();
        private int _cacheVersion;

        public GW2TokenVerification(Gw2ApiManager gw2ApiManager)
        {
            _gw2ApiManager = gw2ApiManager;
        }

        public void Clear()
        {
            lock (_cacheLock)
            {
                _cacheVersion++;
                _cachedToken = string.Empty;
                _expiresAt = DateTimeOffset.MinValue;
            }
        }

        public bool HasValidApiKey()
        {
            return HasRequiredPermissions();
        }

        // Create a short-lived token when we need to verify account ownership with SPARK for things like publishing profiles
        public async Task<string> GetTokenAsync(CancellationToken cancellationToken = default)
        {
            if (!HasRequiredPermissions())
                return string.Empty;

            if (TryGetFreshToken(out var cachedToken))
                return cachedToken;

            await _tokenGate.WaitAsync(cancellationToken);

            try
            {
                if (!HasRequiredPermissions())
                {
                    Clear();
                    return string.Empty;
                }

                if (TryGetFreshToken(out cachedToken))
                    return cachedToken;

                var cacheVersion = GetCacheVersion();
                var expiresAt = DateTimeOffset.UtcNow.Add(TokenLifetime);
                var subtoken = await _gw2ApiManager.Gw2ApiClient.V2.CreateSubtoken
                    .Expires(expiresAt)
                    .WithPermissions(new[] { TokenPermission.Account, TokenPermission.Characters })
                    .GetAsync(cancellationToken);

                var token = subtoken?.Subtoken?.Trim() ?? string.Empty;
                lock (_cacheLock)
                {
                    if (cacheVersion != _cacheVersion)
                        return string.Empty;

                    _cachedToken = token;
                    _expiresAt = string.IsNullOrWhiteSpace(_cachedToken)
                        ? DateTimeOffset.MinValue
                        : expiresAt;

                    return _cachedToken;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Clear();
                BlishWarnings.HttpBlocked(ex, "create a temporary GW2 API verification token");
                Logger.Warn(ex, "Failed to create a GW2 API verification subtoken for SPARK.");
                return string.Empty;
            }
            finally
            {
                _tokenGate.Release();
            }
        }

        private bool HasRequiredPermissions()
        {
            return _gw2ApiManager != null
                && _gw2ApiManager.HasSubtoken
                && _gw2ApiManager.HasPermissions(new[] { TokenPermission.Account, TokenPermission.Characters });
        }

        private int GetCacheVersion()
        {
            lock (_cacheLock)
            {
                return _cacheVersion;
            }
        }

        private bool TryGetFreshToken(out string token)
        {
            lock (_cacheLock)
            {
                if (!string.IsNullOrWhiteSpace(_cachedToken)
                    && DateTimeOffset.UtcNow < _expiresAt.Subtract(RefreshSkew))
                {
                    token = _cachedToken;
                    return true;
                }
            }

            token = string.Empty;
            return false;
        }

        public void Dispose()
        {
            Clear();
        }
    }
}
