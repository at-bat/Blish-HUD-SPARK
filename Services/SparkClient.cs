using Blish_HUD;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using rp.spark.Models;
using rp.spark.Models.Api;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace rp.spark.Services
{
    // SPARK treats the GW2 API as the authority for account/character name ownership.
    // The client (users) can send public profile/presence JSON, but account/character ownership is verified server-side before authenticated changes are accepted.
    // For authenticated requests, the client sends a temporary GW2 subtoken, then the server verifies that against the actual GW2 API to derive the account name + character.
    // The client does not store a permanent key. The server keeps a short-lived GW2 auth cache to avoid repeatedly hitting the GW2 API.
    //
    // Endpoint summary:
    // - GET  /health
    // - GET  /presence/?region=NA|EU
    // - POST /presence/
    // - POST /profiles/
    // - GET  /profiles/?account={account}&character={character}&profileId={profileId}
    // - POST /blocks/
    // - GET  /blocks/
    // - PUT  /blocks/
    // - DELETE /blocks/?account={account}
    // - POST /reports/

    public class SparkClient
    {
        private static readonly Logger Logger = Logger.GetLogger<SparkClient>();
        private const string ServerAction = "connect to the SPARK webserver";
        private const string ServerUnavailableMessage = "Cannot connect to the SPARK webserver.";
        private const string ServerInvalidResponseMessage = "The SPARK webserver returned a response SPARK could not read.";
        private const string ServerEmptyResponseMessage = "The SPARK webserver returned an empty response.";
        private const string ServerTimeoutMessage = "The SPARK webserver did not respond in time.";
        private const string SubtokenHeader = "X-GW2-Subtoken";

        private static readonly HttpClient SharedHttpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            Converters = new List<JsonConverter> { new StringEnumConverter() }
        };

        private Uri _baseUri;

        public SparkClient(string baseUrl)
        {
            SetBaseUrl(baseUrl);
        }

        public bool IsConfigured => _baseUri != null;

        public void SetBaseUrl(string baseUrl)
        {
            _baseUri = !string.IsNullOrWhiteSpace(baseUrl)
                && Uri.TryCreate(baseUrl.Trim().TrimEnd('/') + "/", UriKind.Absolute, out var uri)
                    ? uri
                    : null;
        }

        public async Task<bool> PublishPresenceAsync(PlayerPresence presence, CancellationToken cancellationToken = default)
        {
            var result = await PublishPresenceResultAsync(presence, cancellationToken);
            return result.Succeeded;
        }

        public async Task<IReadOnlyList<PlayerPresence>> ListPresenceAsync(ProfileRegion region, CancellationToken cancellationToken = default)
        {
            var result = await ListPresenceResultAsync(region, cancellationToken);

            return result.Succeeded
                ? result.Value?.Entries ?? new List<PlayerPresence>()
                : new List<PlayerPresence>();
        }

        public async Task<bool> UploadProfileAsync(CharacterProfile profile, CancellationToken cancellationToken = default)
        {
            var result = await UploadProfileResultAsync(profile, null, cancellationToken);
            return result.Succeeded;
        }

        public async Task<bool> UploadProfileAsync(
            CharacterProfile profile,
            PlayerPresence presence,
            CancellationToken cancellationToken = default)
        {
            var result = await UploadProfileResultAsync(profile, presence, cancellationToken);
            return result.Succeeded;
        }

        public async Task<ProfileDownload> DownloadProfileAsync(
            string accountName,
            string officialCharacterName,
            string profileId, 
            CancellationToken cancellationToken = default)
        {
            var result = await DownloadProfileResultAsync(accountName, officialCharacterName, profileId, cancellationToken);
            return result.Value;
        }

        public async Task<bool> BlockAccountAsync(string accountName, CancellationToken cancellationToken = default)
        {
            var result = await BlockAccountResultAsync(accountName, cancellationToken);
            return result.Succeeded;
        }

        public async Task<bool> UnblockAccountAsync(string accountName, CancellationToken cancellationToken = default)
        {
            var result = await UnblockAccountResultAsync(accountName, cancellationToken);
            return result.Succeeded;
        }

        public async Task<bool> ReplaceBlocklistAsync(
            IEnumerable<string> accountNames,
            CancellationToken cancellationToken = default)
        {
            var result = await ReplaceBlocklistResultAsync(accountNames, cancellationToken);
            return result.Succeeded;
        }

        public Task<ApiResult<bool>> PublishPresenceResultAsync(PlayerPresence presence, CancellationToken cancellationToken = default)
        {
            return PublishPresenceResultAsync(presence, string.Empty, cancellationToken);
        }

        public Task<ApiResult<bool>> PublishPresenceResultAsync(
            PlayerPresence presence,
            string gw2Subtoken,
            CancellationToken cancellationToken = default)
        {
            return PostAsync(
                "presence/",
                new PresencePublishRequest { Presence = presence },
                cancellationToken,
                gw2Subtoken);
        }

        public Task<ApiResult<PresenceListResponse>> ListPresenceResultAsync(ProfileRegion region, CancellationToken cancellationToken = default)
        {
            return ListPresenceResultAsync(region, string.Empty, cancellationToken);
        }

        public Task<ApiResult<PresenceListResponse>> ListPresenceResultAsync(
            ProfileRegion region,
            string gw2Subtoken,
            CancellationToken cancellationToken = default)
        {
            return GetAsync<PresenceListResponse>(
                $"presence/?region={Uri.EscapeDataString(region.ToString())}",
                cancellationToken,
                gw2Subtoken);
        }

        public Task<ApiResult<bool>> UploadProfileResultAsync(CharacterProfile profile, CancellationToken cancellationToken = default)
        {
            return UploadProfileResultAsync(profile, null, cancellationToken);
        }

        public Task<ApiResult<bool>> UploadProfileResultAsync(
            CharacterProfile profile,
            PlayerPresence presence,
            CancellationToken cancellationToken = default)
        {
            return UploadProfileResultAsync(profile, presence, string.Empty, cancellationToken);
        }

        public Task<ApiResult<bool>> UploadProfileResultAsync(
            CharacterProfile profile,
            PlayerPresence presence,
            string gw2Subtoken,
            CancellationToken cancellationToken = default)
        {
            return PostAsync(
                "profiles/",
                ProfileUploadRequest.FromProfile(profile, presence),
                cancellationToken,
                gw2Subtoken);
        }

        public Task<ApiResult<ProfileDownload>> DownloadProfileResultAsync(
            string accountName,
            string officialCharacterName,
            string profileId,
            CancellationToken cancellationToken = default)
        {
            return DownloadProfileResultAsync(
                accountName,
                officialCharacterName,
                profileId,
                string.Empty,
                cancellationToken);
        }

        public Task<ApiResult<ProfileDownload>> DownloadProfileResultAsync(
            string accountName,
            string officialCharacterName,
            string profileId,
            string gw2Subtoken,
            CancellationToken cancellationToken = default)
        {
            var path = "profiles/"
                     + $"?account={Uri.EscapeDataString(accountName ?? string.Empty)}"
                     + $"&character={Uri.EscapeDataString(officialCharacterName ?? string.Empty)}"
                     + $"&profileId={Uri.EscapeDataString(profileId ?? string.Empty)}";

            return GetAsync<ProfileDownload>(path, cancellationToken, gw2Subtoken);
        }

        public Task<ApiResult<bool>> BlockAccountResultAsync(string accountName, CancellationToken cancellationToken = default)
        {
            return BlockAccountResultAsync(accountName, string.Empty, cancellationToken);
        }

        public Task<ApiResult<bool>> BlockAccountResultAsync(
            string accountName,
            string gw2Subtoken,
            CancellationToken cancellationToken = default)
        {
            return PostAsync(
                "blocks/",
                new AccountBlockRequest { AccountName = accountName ?? string.Empty },
                cancellationToken,
                gw2Subtoken);
        }

        public Task<ApiResult<bool>> UnblockAccountResultAsync(string accountName, CancellationToken cancellationToken = default)
        {
            return UnblockAccountResultAsync(accountName, string.Empty, cancellationToken);
        }

        public Task<ApiResult<bool>> UnblockAccountResultAsync(
            string accountName,
            string gw2Subtoken,
            CancellationToken cancellationToken = default)
        {
            var path = "blocks/"
                     + $"?account={Uri.EscapeDataString(accountName ?? string.Empty)}";

            return DeleteAsync(path, cancellationToken, gw2Subtoken);
        }

        public Task<ApiResult<bool>> ReplaceBlocklistResultAsync(
            IEnumerable<string> accountNames,
            CancellationToken cancellationToken = default)
        {
            return ReplaceBlocklistResultAsync(accountNames, string.Empty, cancellationToken);
        }

        public Task<ApiResult<bool>> ReplaceBlocklistResultAsync(
            IEnumerable<string> accountNames,
            string gw2Subtoken,
            CancellationToken cancellationToken = default)
        {
            return PutAsync(
                "blocks/",
                new BlocklistRequest
                {
                    AccountNames = accountNames == null
                        ? new List<string>()
                        : new List<string>(accountNames)
                },
                cancellationToken,
                gw2Subtoken);
        }

        public Task<ApiResult<ProfileReportResponse>> ReportProfileResultAsync(
            ProfileReportRequest report,
            string gw2Subtoken,
            CancellationToken cancellationToken = default)
        {
            return PostAsync<ProfileReportRequest, ProfileReportResponse>(
                "reports/",
                report,
                cancellationToken,
                gw2Subtoken);
        }

        private async Task<ApiResult<T>> GetAsync<T>(
            string relativePath,
            CancellationToken cancellationToken,
            string gw2Subtoken = "")
            where T : class
        {
            if (!IsConfigured)
                return ApiResult<T>.Failure(
                    "SPARK webserver is misconfigured or down.",
                    failureKind: ApiFailure.NotConfigured);

            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Get, GetUri(relativePath)))
                {
                    AddSubtokenHeader(request, gw2Subtoken);

                    using (var response = await SharedHttpClient.SendAsync(request, cancellationToken))
                    {
                        var responseBody = await response.Content.ReadAsStringAsync();

                        if (!response.IsSuccessStatusCode)
                        {
                            Logger.Warn("SPARK GET {path} failed with status {status}.", relativePath, response.StatusCode);
                            return ApiResult<T>.Failure(
                                GetServerStatusMessage(response.StatusCode, responseBody),
                                response.StatusCode);
                        }

                        if (string.IsNullOrWhiteSpace(responseBody))
                            return ApiResult<T>.Failure(ServerEmptyResponseMessage, response.StatusCode);

                        var value = JsonConvert.DeserializeObject<T>(responseBody, JsonSettings);

                        return value == null
                            ? ApiResult<T>.Failure(ServerInvalidResponseMessage, response.StatusCode)
                            : ApiResult<T>.Success(value, response.StatusCode);
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (TaskCanceledException ex)
            {
                Logger.Warn(ex, "SPARK GET {path} timed out.", relativePath);
                return ApiResult<T>.Failure(
                    ServerTimeoutMessage,
                    failureKind: ApiFailure.Timeout);
            }
            catch (JsonException ex)
            {
                Logger.Warn(ex, "SPARK GET {path} returned invalid JSON.", relativePath);
                return ApiResult<T>.Failure(
                    ServerInvalidResponseMessage,
                    failureKind: ApiFailure.InvalidResponse);
            }
            catch (Exception ex)
            {
                BlishWarnings.HttpBlocked(ex, ServerAction);
                Logger.Warn(ex, "SPARK GET {path} failed.", relativePath);
                return ApiResult<T>.Failure(
                    ServerUnavailableMessage,
                    failureKind: BlishWarnings.IsHttpBlocked(ex)
                        ? ApiFailure.BlockedByWindows
                        : ApiFailure.Network);
            }
        }

        private async Task<ApiResult<bool>> PostAsync<T>(
            string relativePath,
            T payload,
            CancellationToken cancellationToken,
            string gw2Subtoken = "")
        {
            if (!IsConfigured)
                return ApiResult<bool>.Failure(
                    "SPARK webserver is not configured.",
                    failureKind: ApiFailure.NotConfigured);

            if (payload == null)
                return ApiResult<bool>.Failure(
                    "SPARK response is empty.",
                    failureKind: ApiFailure.InvalidRequest);

            try
            {
                var json = JsonConvert.SerializeObject(payload, Formatting.None, JsonSettings);

                using (var request = new HttpRequestMessage(HttpMethod.Post, GetUri(relativePath)))
                using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
                {
                    request.Content = content;

                    AddSubtokenHeader(request, gw2Subtoken);

                    using (var response = await SharedHttpClient.SendAsync(request, cancellationToken))
                    {
                        var responseBody = await response.Content.ReadAsStringAsync();

                        if (response.IsSuccessStatusCode)
                            return ApiResult<bool>.Success(true, response.StatusCode);

                        Logger.Warn("SPARK POST {path} failed with status {status}.", relativePath, response.StatusCode);
                        return ApiResult<bool>.Failure(
                            GetServerStatusMessage(response.StatusCode, responseBody),
                            response.StatusCode);
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (TaskCanceledException ex)
            {
                Logger.Warn(ex, "SPARK POST {path} timed out.", relativePath);
                return ApiResult<bool>.Failure(
                    ServerTimeoutMessage,
                    failureKind: ApiFailure.Timeout);
            }
            catch (Exception ex)
            {
                BlishWarnings.HttpBlocked(ex, ServerAction);
                Logger.Warn(ex, "SPARK POST {path} failed.", relativePath);
                return ApiResult<bool>.Failure(
                    ServerUnavailableMessage,
                    failureKind: BlishWarnings.IsHttpBlocked(ex)
                        ? ApiFailure.BlockedByWindows
                : ApiFailure.Network);
            }
        }

        private async Task<ApiResult<TResponse>> PostAsync<TPayload, TResponse>(
            string relativePath,
            TPayload payload,
            CancellationToken cancellationToken,
            string gw2Subtoken = "")
            where TResponse : class
        {
            if (!IsConfigured)
                return ApiResult<TResponse>.Failure(
                    "SPARK webserver is not configured.",
                    failureKind: ApiFailure.NotConfigured);

            if (payload == null)
                return ApiResult<TResponse>.Failure(
                    "SPARK request is empty.",
                    failureKind: ApiFailure.InvalidRequest);

            try
            {
                var json = JsonConvert.SerializeObject(payload, Formatting.None, JsonSettings);

                using (var request = new HttpRequestMessage(HttpMethod.Post, GetUri(relativePath)))
                using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
                {
                    request.Content = content;

                    AddSubtokenHeader(request, gw2Subtoken);

                    using (var response = await SharedHttpClient.SendAsync(request, cancellationToken))
                    {
                        var responseBody = await response.Content.ReadAsStringAsync();

                        if (!response.IsSuccessStatusCode)
                        {
                            Logger.Warn("SPARK POST {path} failed with status {status}.", relativePath, response.StatusCode);
                            return ApiResult<TResponse>.Failure(
                                GetServerStatusMessage(response.StatusCode, responseBody),
                                response.StatusCode);
                        }

                        if (string.IsNullOrWhiteSpace(responseBody))
                            return ApiResult<TResponse>.Failure(ServerEmptyResponseMessage, response.StatusCode);

                        var value = JsonConvert.DeserializeObject<TResponse>(responseBody, JsonSettings);

                        return value == null
                            ? ApiResult<TResponse>.Failure(ServerInvalidResponseMessage, response.StatusCode)
                            : ApiResult<TResponse>.Success(value, response.StatusCode);
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (TaskCanceledException ex)
            {
                Logger.Warn(ex, "SPARK POST {path} timed out.", relativePath);
                return ApiResult<TResponse>.Failure(
                    ServerTimeoutMessage,
                    failureKind: ApiFailure.Timeout);
            }
            catch (JsonException ex)
            {
                Logger.Warn(ex, "SPARK POST {path} returned invalid JSON.", relativePath);
                return ApiResult<TResponse>.Failure(
                    ServerInvalidResponseMessage,
                    failureKind: ApiFailure.InvalidResponse);
            }
            catch (Exception ex)
            {
                BlishWarnings.HttpBlocked(ex, ServerAction);
                Logger.Warn(ex, "SPARK POST {path} failed.", relativePath);
                return ApiResult<TResponse>.Failure(
                    ServerUnavailableMessage,
                    failureKind: BlishWarnings.IsHttpBlocked(ex)
                        ? ApiFailure.BlockedByWindows
                        : ApiFailure.Network);
            }
        }

        private async Task<ApiResult<bool>> PutAsync<T>(
            string relativePath,
            T payload,
            CancellationToken cancellationToken,
            string gw2Subtoken = "")
        {
            if (!IsConfigured)
                return ApiResult<bool>.Failure(
                    "SPARK webserver is not configured.",
                    failureKind: ApiFailure.NotConfigured);

            if (payload == null)
                return ApiResult<bool>.Failure(
                    "SPARK response is empty.",
                    failureKind: ApiFailure.InvalidRequest);

            try
            {
                var json = JsonConvert.SerializeObject(payload, Formatting.None, JsonSettings);

                using (var request = new HttpRequestMessage(HttpMethod.Put, GetUri(relativePath)))
                using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
                {
                    request.Content = content;

                    AddSubtokenHeader(request, gw2Subtoken);

                    using (var response = await SharedHttpClient.SendAsync(request, cancellationToken))
                    {
                        var responseBody = await response.Content.ReadAsStringAsync();

                        if (response.IsSuccessStatusCode)
                            return ApiResult<bool>.Success(true, response.StatusCode);

                        Logger.Warn("SPARK PUT {path} failed with status {status}.", relativePath, response.StatusCode);
                        return ApiResult<bool>.Failure(
                            GetServerStatusMessage(response.StatusCode, responseBody),
                            response.StatusCode);
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (TaskCanceledException ex)
            {
                Logger.Warn(ex, "SPARK PUT {path} timed out.", relativePath);
                return ApiResult<bool>.Failure(
                    ServerTimeoutMessage,
                    failureKind: ApiFailure.Timeout);
            }
            catch (Exception ex)
            {
                BlishWarnings.HttpBlocked(ex, ServerAction);
                Logger.Warn(ex, "SPARK PUT {path} failed.", relativePath);
                return ApiResult<bool>.Failure(
                    ServerUnavailableMessage,
                    failureKind: BlishWarnings.IsHttpBlocked(ex)
                        ? ApiFailure.BlockedByWindows
                        : ApiFailure.Network);
            }
        }

        private async Task<ApiResult<bool>> DeleteAsync(
            string relativePath,
            CancellationToken cancellationToken,
            string gw2Subtoken = "")
        {
            if (!IsConfigured)
                return ApiResult<bool>.Failure(
                    "SPARK webserver is not configured.",
                    failureKind: ApiFailure.NotConfigured);

            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Delete, GetUri(relativePath)))
                {
                    AddSubtokenHeader(request, gw2Subtoken);

                    using (var response = await SharedHttpClient.SendAsync(request, cancellationToken))
                    {
                        var responseBody = await response.Content.ReadAsStringAsync();

                        if (response.IsSuccessStatusCode)
                            return ApiResult<bool>.Success(true, response.StatusCode);

                        Logger.Warn("SPARK DELETE {path} failed with status {status}.", relativePath, response.StatusCode);
                        return ApiResult<bool>.Failure(
                            GetServerStatusMessage(response.StatusCode, responseBody),
                            response.StatusCode);
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (TaskCanceledException ex)
            {
                Logger.Warn(ex, "SPARK DELETE {path} timed out.", relativePath);
                return ApiResult<bool>.Failure(
                    ServerTimeoutMessage,
                    failureKind: ApiFailure.Timeout);
            }
            catch (Exception ex)
            {
                BlishWarnings.HttpBlocked(ex, ServerAction);
                Logger.Warn(ex, "SPARK DELETE {path} failed.", relativePath);
                return ApiResult<bool>.Failure(
                    ServerUnavailableMessage,
                    failureKind: BlishWarnings.IsHttpBlocked(ex)
                        ? ApiFailure.BlockedByWindows
                        : ApiFailure.Network);
            }
        }

        private static void AddSubtokenHeader(HttpRequestMessage request, string gw2Subtoken)
        {
            if (request == null || string.IsNullOrWhiteSpace(gw2Subtoken))
                return;

            request.Headers.TryAddWithoutValidation(SubtokenHeader, gw2Subtoken.Trim());
        }

        private Uri GetUri(string relativePath)
        {
            return new Uri(_baseUri, relativePath ?? string.Empty);
        }

        private static string GetServerStatusMessage(HttpStatusCode statusCode, string responseBody = "")
        {
            var serverDetail = GetServerDetail(responseBody);

            if (!string.IsNullOrWhiteSpace(serverDetail))
                return serverDetail;

            return $"SPARK status: {(int)statusCode}.";
        }

        private static string GetServerDetail(string responseBody)
        {
            if (string.IsNullOrWhiteSpace(responseBody))
                return string.Empty;

            try
            {
                var payload = JsonConvert.DeserializeObject<Dictionary<string, object>>(responseBody, JsonSettings);

                if (payload == null || !payload.TryGetValue("detail", out var detail))
                    return string.Empty;

                var detailText = detail?.ToString()?.Trim() ?? string.Empty;

                return detailText.Length <= 160 ? detailText : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
    }

    public class ApiResult<T>
    {
        private ApiResult(
            bool succeeded,
            T value,
            HttpStatusCode? statusCode,
            string errorMessage,
            ApiFailure failureKind)
        {
            Succeeded = succeeded;
            Value = value;
            StatusCode = statusCode;
            ErrorMessage = errorMessage ?? string.Empty;
            FailureKind = failureKind;
        }

        public bool Succeeded { get; }

        public T Value { get; }

        public HttpStatusCode? StatusCode { get; }

        public string ErrorMessage { get; }

        public ApiFailure FailureKind { get; }

        public static ApiResult<T> Success(T value, HttpStatusCode? statusCode = null)
        {
            return new ApiResult<T>(true, value, statusCode, string.Empty, ApiFailure.None);
        }

        public static ApiResult<T> Failure(
            string errorMessage,
            HttpStatusCode? statusCode = null,
            ApiFailure failureKind = ApiFailure.None)
        {
            return new ApiResult<T>(false, default, statusCode, errorMessage, failureKind);
        }
    }

    public enum ApiFailure
    {
        None,
        NotConfigured,
        InvalidRequest,
        Timeout,
        BlockedByWindows,
        InvalidResponse,
        Network
    }
}
