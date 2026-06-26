using Blish_HUD;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using rp.spark.Models;
using rp.spark.Models.Api;
using System;
using System.Collections.Generic;
using System.IO;
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
        private const string ServerInvalidRequestMessage = "SPARK could not prepare the request.";
        private const string SubtokenHeader = "X-GW2-Subtoken";
        private const int MaxResponseBodySize = 1024 * 1024;
        private const int ResponseReadBufferSize = 8192;
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

        private static readonly HttpClient SharedHttpClient = new HttpClient
        {
            Timeout = System.Threading.Timeout.InfiniteTimeSpan
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

        public Task<ApiResult<PresenceListResponse>> ListPresenceResultAsync(
            ProfileRegion region,
            bool includeMature,
            string gw2Subtoken,
            CancellationToken cancellationToken = default)
        {
            var path = $"presence/?region={Uri.EscapeDataString(region.ToString())}"
                     + $"&includeMature={includeMature.ToString().ToLowerInvariant()}";

            return GetAsync<PresenceListResponse>(
                path,
                cancellationToken,
                gw2Subtoken);
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
            bool includeMature,
            string gw2Subtoken,
            CancellationToken cancellationToken = default)
        {
            var path = "profiles/"
                     + $"?account={Uri.EscapeDataString(accountName ?? string.Empty)}"
                     + $"&character={Uri.EscapeDataString(officialCharacterName ?? string.Empty)}"
                     + $"&profileId={Uri.EscapeDataString(profileId ?? string.Empty)}"
                     + $"&includeMature={includeMature.ToString().ToLowerInvariant()}";

            return GetAsync<ProfileDownload>(path, cancellationToken, gw2Subtoken);
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
            string gw2Subtoken)
            where T : class
        {
            if (!IsConfigured)
                return ApiResult<T>.Failure(
                    "SPARK webserver is misconfigured or down.",
                    failureKind: ApiFailure.NotConfigured);

            return await ExecuteRequestAsync<T>(
                HttpMethod.Get,
                relativePath,
                cancellationToken,
                async requestToken =>
                {
                    using (var request = new HttpRequestMessage(HttpMethod.Get, GetUri(relativePath)))
                    {
                        AddSubtokenHeader(request, gw2Subtoken);

                        using (var response = await SharedHttpClient.SendAsync(
                            request,
                            HttpCompletionOption.ResponseHeadersRead,
                            requestToken))
                        {
                            return await ReadJsonResponseAsync<T>(
                                HttpMethod.Get,
                                relativePath,
                                response,
                                requestToken);
                        }
                    }
                });
        }

        private async Task<ApiResult<bool>> PostAsync<T>(
            string relativePath,
            T payload,
            CancellationToken cancellationToken,
            string gw2Subtoken)
        {
            if (!IsConfigured)
                return ApiResult<bool>.Failure(
                    "SPARK webserver is not configured.",
                    failureKind: ApiFailure.NotConfigured);

            if (payload == null)
                return ApiResult<bool>.Failure(
                    "SPARK request is empty.",
                    failureKind: ApiFailure.InvalidRequest);

            return await ExecuteRequestAsync<bool>(
                HttpMethod.Post,
                relativePath,
                cancellationToken,
                async requestToken =>
                {
                    var json = JsonConvert.SerializeObject(payload, Formatting.None, JsonSettings);

                    using (var request = new HttpRequestMessage(HttpMethod.Post, GetUri(relativePath)))
                    using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
                    {
                        request.Content = content;

                        AddSubtokenHeader(request, gw2Subtoken);

                        using (var response = await SharedHttpClient.SendAsync(
                            request,
                            HttpCompletionOption.ResponseHeadersRead,
                            requestToken))
                        {
                            return await ReadStatusOnlyResponseAsync(
                                HttpMethod.Post,
                                relativePath,
                                response,
                                requestToken);
                        }
                    }
                });
        }

        private async Task<ApiResult<TResponse>> PostAsync<TPayload, TResponse>(
            string relativePath,
            TPayload payload,
            CancellationToken cancellationToken,
            string gw2Subtoken)
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

            return await ExecuteRequestAsync<TResponse>(
                HttpMethod.Post,
                relativePath,
                cancellationToken,
                async requestToken =>
                {
                    var json = JsonConvert.SerializeObject(payload, Formatting.None, JsonSettings);

                    using (var request = new HttpRequestMessage(HttpMethod.Post, GetUri(relativePath)))
                    using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
                    {
                        request.Content = content;

                        AddSubtokenHeader(request, gw2Subtoken);

                        using (var response = await SharedHttpClient.SendAsync(
                            request,
                            HttpCompletionOption.ResponseHeadersRead,
                            requestToken))
                        {
                            return await ReadJsonResponseAsync<TResponse>(
                                HttpMethod.Post,
                                relativePath,
                                response,
                                requestToken);
                        }
                    }
                });
        }

        private async Task<ApiResult<bool>> PutAsync<T>(
            string relativePath,
            T payload,
            CancellationToken cancellationToken,
            string gw2Subtoken)
        {
            if (!IsConfigured)
                return ApiResult<bool>.Failure(
                    "SPARK webserver is not configured.",
                    failureKind: ApiFailure.NotConfigured);

            if (payload == null)
                return ApiResult<bool>.Failure(
                    "SPARK request is empty.",
                    failureKind: ApiFailure.InvalidRequest);

            return await ExecuteRequestAsync<bool>(
                HttpMethod.Put,
                relativePath,
                cancellationToken,
                async requestToken =>
                {
                    var json = JsonConvert.SerializeObject(payload, Formatting.None, JsonSettings);

                    using (var request = new HttpRequestMessage(HttpMethod.Put, GetUri(relativePath)))
                    using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
                    {
                        request.Content = content;

                        AddSubtokenHeader(request, gw2Subtoken);

                        using (var response = await SharedHttpClient.SendAsync(
                            request,
                            HttpCompletionOption.ResponseHeadersRead,
                            requestToken))
                        {
                            return await ReadStatusOnlyResponseAsync(
                                HttpMethod.Put,
                                relativePath,
                                response,
                                requestToken);
                        }
                    }
                });
        }

        private async Task<ApiResult<bool>> DeleteAsync(
            string relativePath,
            CancellationToken cancellationToken,
            string gw2Subtoken)
        {
            if (!IsConfigured)
                return ApiResult<bool>.Failure(
                    "SPARK webserver is not configured.",
                    failureKind: ApiFailure.NotConfigured);

            return await ExecuteRequestAsync<bool>(
                HttpMethod.Delete,
                relativePath,
                cancellationToken,
                async requestToken =>
                {
                    using (var request = new HttpRequestMessage(HttpMethod.Delete, GetUri(relativePath)))
                    {
                        AddSubtokenHeader(request, gw2Subtoken);

                        using (var response = await SharedHttpClient.SendAsync(
                            request,
                            HttpCompletionOption.ResponseHeadersRead,
                            requestToken))
                        {
                            return await ReadStatusOnlyResponseAsync(
                                HttpMethod.Delete,
                                relativePath,
                                response,
                                requestToken);
                        }
                    }
                });
        }

        private static async Task<ApiResult<T>> ExecuteRequestAsync<T>(
            HttpMethod method,
            string relativePath,
            CancellationToken cancellationToken,
            Func<CancellationToken, Task<ApiResult<T>>> requestAsync)
        {
            var logPath = GetEndpointPathForLog(relativePath);

            using (var requestTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                requestTimeout.CancelAfter(RequestTimeout);

                try
                {
                    return await requestAsync(requestTimeout.Token);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (ResponseBodyTooLargeException ex)
                {
                    Logger.Warn(
                        "SPARK {method} {path} exceeded the {limit} byte response limit.",
                        method.Method,
                        logPath,
                        MaxResponseBodySize);

                    return ApiResult<T>.Failure(
                        ServerInvalidResponseMessage,
                        ex.StatusCode,
                        ApiFailure.InvalidResponse);
                }
                catch (InvalidApiResponseException ex)
                {
                    Logger.Warn(
                        "SPARK {method} {path} returned invalid JSON ({errorType}).",
                        method.Method,
                        logPath,
                        (ex.InnerException ?? ex).GetType().Name);

                    return ApiResult<T>.Failure(
                        ServerInvalidResponseMessage,
                        ex.StatusCode,
                        ApiFailure.InvalidResponse);
                }
                catch (JsonException ex)
                {
                    Logger.Warn(
                        "SPARK {method} {path} could not serialize the request ({errorType}).",
                        method.Method,
                        logPath,
                        ex.GetType().Name);

                    return ApiResult<T>.Failure(
                        ServerInvalidRequestMessage,
                        failureKind: ApiFailure.InvalidRequest);
                }
                catch (OperationCanceledException ex)
                {
                    Logger.Warn(
                        "SPARK {method} {path} timed out ({errorType}).",
                        method.Method,
                        logPath,
                        ex.GetType().Name);

                    return ApiResult<T>.Failure(
                        ServerTimeoutMessage,
                        failureKind: ApiFailure.Timeout);
                }
                catch (Exception ex)
                {
                    BlishWarnings.HttpBlocked(ex, ServerAction);

                    Logger.Warn(
                        "SPARK {method} {path} failed ({errorType}).",
                        method.Method,
                        logPath,
                        ex.GetType().Name);

                    return ApiResult<T>.Failure(
                        ServerUnavailableMessage,
                        failureKind: BlishWarnings.IsHttpBlocked(ex)
                            ? ApiFailure.BlockedByWindows
                            : ApiFailure.Network);
                }
            }
        }

        private static async Task<ApiResult<T>> ReadJsonResponseAsync<T>(
            HttpMethod method,
            string relativePath,
            HttpResponseMessage response,
            CancellationToken cancellationToken)
            where T : class
        {
            var responseBody = await ReadResponseBodyAsync(response, cancellationToken);

            if (!response.IsSuccessStatusCode)
                return CreateStatusFailure<T>(
                    method,
                    relativePath,
                    response,
                    responseBody);

            if (string.IsNullOrWhiteSpace(responseBody))
                return ApiResult<T>.Failure(
                    ServerEmptyResponseMessage,
                    response.StatusCode);

            try
            {
                var value = JsonConvert.DeserializeObject<T>(responseBody, JsonSettings);

                return value == null
                    ? ApiResult<T>.Failure(
                        ServerInvalidResponseMessage,
                        response.StatusCode,
                        ApiFailure.InvalidResponse)
                    : ApiResult<T>.Success(value, response.StatusCode);
            }
            catch (JsonException ex)
            {
                throw new InvalidApiResponseException(response.StatusCode, ex);
            }
        }

        private static async Task<ApiResult<bool>> ReadStatusOnlyResponseAsync(
            HttpMethod method,
            string relativePath,
            HttpResponseMessage response,
            CancellationToken cancellationToken)
        {
            if (response.IsSuccessStatusCode)
                return ApiResult<bool>.Success(true, response.StatusCode);

            var responseBody = await ReadResponseBodyAsync(response, cancellationToken);

            return CreateStatusFailure<bool>(
                method,
                relativePath,
                response,
                responseBody);
        }

        private static ApiResult<T> CreateStatusFailure<T>(
            HttpMethod method,
            string relativePath,
            HttpResponseMessage response,
            string responseBody)
        {
            Logger.Warn(
                "SPARK {method} {path} failed with status {status}.",
                method.Method,
                GetEndpointPathForLog(relativePath),
                response.StatusCode);

            return ApiResult<T>.Failure(
                GetServerStatusMessage(response.StatusCode, responseBody),
                response.StatusCode);
        }

        private static async Task<string> ReadResponseBodyAsync(
            HttpResponseMessage response,
            CancellationToken cancellationToken)
        {
            if (response?.Content == null)
                return string.Empty;

            var contentLength = response.Content.Headers.ContentLength;

            if (contentLength.HasValue && contentLength.Value > MaxResponseBodySize)
                throw new ResponseBodyTooLargeException(response.StatusCode);

            using (var stream = await response.Content.ReadAsStreamAsync())
            using (var memory = new MemoryStream())
            {
                var buffer = new byte[ResponseReadBufferSize];
                int bytesRead;

                while ((bytesRead = await stream.ReadAsync(
                           buffer,
                           0,
                           buffer.Length,
                           cancellationToken)) > 0)
                {
                    if (memory.Length + bytesRead > MaxResponseBodySize)
                        throw new ResponseBodyTooLargeException(response.StatusCode);

                    memory.Write(buffer, 0, bytesRead);
                }

                return Encoding.UTF8.GetString(memory.ToArray());
            }
        }

        private static readonly char[] LogPathSeparators = { '?', '#' };
        private static string GetEndpointPathForLog(string relativePath)
        {
            var path = relativePath?.Trim() ?? string.Empty;
            var index = path.IndexOfAny(LogPathSeparators);

            return index < 0 ? path : path.Substring(0, index);
        }

        private sealed class InvalidApiResponseException : Exception
        {
            public InvalidApiResponseException(
                HttpStatusCode statusCode,
                Exception innerException)
                : base("The API response contained invalid JSON.", innerException)
            {
                StatusCode = statusCode;
            }

            public HttpStatusCode StatusCode { get; }
        }

        private sealed class ResponseBodyTooLargeException : Exception
        {
            public ResponseBodyTooLargeException(HttpStatusCode statusCode)
            {
                StatusCode = statusCode;
            }

            public HttpStatusCode StatusCode { get; }
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
