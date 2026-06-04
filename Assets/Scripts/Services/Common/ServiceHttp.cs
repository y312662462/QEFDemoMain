using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace MultiAgentNPC.Services
{
    /// <summary>
    /// Shared HTTP helper for all external services. Wraps a single reused
    /// <see cref="HttpClient"/> and centralizes timeout, retry, status-code to
    /// <see cref="ServiceErrorType"/> mapping and exception capture, so individual
    /// services only build requests and parse responses.
    /// </summary>
    public static class ServiceHttp
    {
        // One HttpClient reused process-wide (recommended .NET practice). Per-request
        // timeout is handled with a CancellationTokenSource, so the client's own
        // Timeout is left effectively disabled.
        private static readonly HttpClient Client = new HttpClient
        {
            Timeout = Timeout.InfiniteTimeSpan
        };

        /// <summary>Raw HTTP payload returned to a service for parsing.</summary>
        public class HttpPayload
        {
            public byte[] Body;
            public long StatusCode;
        }

        /// <summary>
        /// Joins a base URL and a path without duplicating the path when the base
        /// already ends with it. Tolerates users pasting a full endpoint (including
        /// the route) into a base-URL/endpoint field, e.g. avoids
        /// ".../v1/audio/transcriptions/audio/transcriptions".
        /// </summary>
        public static string CombinePath(string baseUrl, string path)
        {
            if (string.IsNullOrEmpty(baseUrl))
            {
                return path;
            }

            string b = baseUrl.TrimEnd('/');
            string p = "/" + (path ?? string.Empty).Trim('/');
            if (b.EndsWith(p, System.StringComparison.OrdinalIgnoreCase))
            {
                return b;
            }

            return b + p;
        }

        /// <summary>
        /// Sends a request created by <paramref name="requestFactory"/> with timeout
        /// and retry. The factory is invoked once per attempt because an
        /// <see cref="HttpRequestMessage"/> cannot be reused.
        /// Retries only transient failures (network, timeout, HTTP 408/5xx).
        /// </summary>
        public static async Task<ServiceResult<HttpPayload>> SendWithRetryAsync(
            Func<HttpRequestMessage> requestFactory,
            int timeoutSeconds,
            int retryCount,
            CancellationToken cancellationToken)
        {
            int maxAttempts = Mathf.Max(1, retryCount + 1);
            int effectiveTimeout = Mathf.Max(1, timeoutSeconds);
            ServiceResult<HttpPayload> last = null;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return ServiceResult<HttpPayload>.Fail(ServiceErrorType.Cancelled, "Request cancelled by caller.");
                }

                last = await SendOnceAsync(requestFactory, effectiveTimeout, cancellationToken);
                if (last.IsSuccess)
                {
                    return last;
                }

                bool retryable = last.ErrorType == ServiceErrorType.Network ||
                                 last.ErrorType == ServiceErrorType.Timeout;
                if (!retryable || attempt == maxAttempts)
                {
                    return last;
                }

                Debug.LogWarning(
                    $"[ServiceHttp] Attempt {attempt}/{maxAttempts} failed ({last.ErrorType}: {last.ErrorMessage}). Retrying...");
            }

            return last ?? ServiceResult<HttpPayload>.Fail(ServiceErrorType.Unknown, "No attempt was made.");
        }

        private static async Task<ServiceResult<HttpPayload>> SendOnceAsync(
            Func<HttpRequestMessage> requestFactory,
            int timeoutSeconds,
            CancellationToken cancellationToken)
        {
            using (var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

                HttpRequestMessage request = null;
                try
                {
                    request = requestFactory();
                    using (HttpResponseMessage response =
                        await Client.SendAsync(request, HttpCompletionOption.ResponseContentRead, timeoutCts.Token))
                    {
                        byte[] body = await response.Content.ReadAsByteArrayAsync();
                        long status = (long)response.StatusCode;

                        if (response.IsSuccessStatusCode)
                        {
                            return ServiceResult<HttpPayload>.Ok(new HttpPayload { Body = body, StatusCode = status });
                        }

                        ServiceErrorType type = MapStatus(response.StatusCode);
                        string snippet = SafeText(body);
                        return ServiceResult<HttpPayload>.Fail(
                            type, $"HTTP {status} {response.ReasonPhrase}. {snippet}", status);
                    }
                }
                catch (OperationCanceledException)
                {
                    // External cancellation vs our timeout.
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return ServiceResult<HttpPayload>.Fail(ServiceErrorType.Cancelled, "Request cancelled by caller.");
                    }

                    return ServiceResult<HttpPayload>.Fail(
                        ServiceErrorType.Timeout, $"Request timed out after {timeoutSeconds}s.");
                }
                catch (HttpRequestException e)
                {
                    return ServiceResult<HttpPayload>.Fail(ServiceErrorType.Network, $"Network error: {e.Message}");
                }
                catch (Exception e)
                {
                    return ServiceResult<HttpPayload>.Fail(ServiceErrorType.Unknown, $"Unexpected error: {e.Message}");
                }
                finally
                {
                    request?.Dispose();
                }
            }
        }

        private static ServiceErrorType MapStatus(HttpStatusCode code)
        {
            switch (code)
            {
                case HttpStatusCode.Unauthorized:
                case HttpStatusCode.Forbidden:
                    return ServiceErrorType.Auth;
                case HttpStatusCode.RequestTimeout: // 408
                case (HttpStatusCode)429:           // Too Many Requests
                    return ServiceErrorType.Network;
                default:
                    if ((int)code >= 500)
                    {
                        return ServiceErrorType.Network; // retryable server error
                    }

                    return ServiceErrorType.Unknown;    // other 4xx, non-retryable
            }
        }

        private static string SafeText(byte[] body)
        {
            if (body == null || body.Length == 0)
            {
                return string.Empty;
            }

            try
            {
                string text = System.Text.Encoding.UTF8.GetString(body);
                return text.Length > 500 ? text.Substring(0, 500) + "..." : text;
            }
            catch
            {
                return "<non-text response body>";
            }
        }
    }
}
