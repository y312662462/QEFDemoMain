using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MultiAgentNPC.Services
{
    /// <summary>
    /// Base implementation for OpenAI-compatible chat-completion APIs
    /// (OpenAI and DeepSeek share the same request/response shape).
    /// Subclasses only differ in their default base URL and model.
    /// </summary>
    public abstract class OpenAICompatibleLLMService : ILLMService
    {
        protected readonly LLMSettings Settings;
        protected readonly int TimeoutSeconds;
        protected readonly int RetryCount;

        protected OpenAICompatibleLLMService(LLMSettings settings, int timeoutSeconds, int retryCount)
        {
            Settings = settings;
            TimeoutSeconds = timeoutSeconds;
            RetryCount = retryCount;
        }

        public async Task<ServiceResult<string>> CompleteAsync(
            LLMRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null || request.Messages == null || request.Messages.Count == 0)
            {
                return ServiceResult<string>.Fail(ServiceErrorType.Unknown, "LLM request has no messages.");
            }

            if (string.IsNullOrWhiteSpace(Settings.ApiKey))
            {
                return ServiceResult<string>.Fail(ServiceErrorType.Auth, "LLM API key is not configured.");
            }

            string url = ServiceHttp.CombinePath(Settings.ResolveBaseUrl(), "chat/completions");
            string bodyJson = BuildRequestJson(request);

            ServiceResult<ServiceHttp.HttpPayload> http = await ServiceHttp.SendWithRetryAsync(
                () =>
                {
                    var message = new HttpRequestMessage(HttpMethod.Post, url)
                    {
                        Content = new StringContent(bodyJson, Encoding.UTF8, "application/json")
                    };
                    message.Headers.TryAddWithoutValidation("Authorization", $"Bearer {Settings.ApiKey}");
                    return message;
                },
                TimeoutSeconds, RetryCount, cancellationToken);

            if (!http.IsSuccess)
            {
                return ServiceResult<string>.Fail(http.ErrorType, http.ErrorMessage, http.StatusCode);
            }

            return ParseResponse(http.Value.Body);
        }

        private string BuildRequestJson(LLMRequest request)
        {
            var messages = new List<object>(request.Messages.Count);
            foreach (LLMMessage m in request.Messages)
            {
                messages.Add(new { role = m.Role, content = m.Content });
            }

            var payload = new Dictionary<string, object>
            {
                ["model"] = Settings.ResolveModel(),
                ["messages"] = messages,
                ["temperature"] = request.TemperatureOverride ?? Settings.Temperature,
                ["max_tokens"] = request.MaxTokensOverride ?? Settings.MaxTokens
            };

            if (request.ForceJson)
            {
                // Ask the model to emit a JSON object only (no Markdown/prose).
                payload["response_format"] = new { type = "json_object" };
            }

            return JsonConvert.SerializeObject(payload);
        }

        private ServiceResult<string> ParseResponse(byte[] body)
        {
            string raw = Encoding.UTF8.GetString(body ?? new byte[0]);
            try
            {
                JObject root = JObject.Parse(raw);
                string content = (string)root.SelectToken("choices[0].message.content");
                if (string.IsNullOrEmpty(content))
                {
                    return ServiceResult<string>.Fail(
                        ServiceErrorType.Parse, "LLM response missing choices[0].message.content.");
                }

                return ServiceResult<string>.Ok(content);
            }
            catch (JsonException e)
            {
                return ServiceResult<string>.Fail(
                    ServiceErrorType.Parse, $"Failed to parse LLM response JSON: {e.Message}");
            }
        }
    }
}
