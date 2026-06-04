using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace MultiAgentNPC.Services
{
    /// <summary>
    /// Speech-to-text via the OpenAI Whisper API
    /// (multipart/form-data POST to /audio/transcriptions).
    /// </summary>
    public class OpenAIWhisperSTTService : ISTTService
    {
        private readonly STTSettings _settings;
        private readonly int _timeoutSeconds;
        private readonly int _retryCount;

        public OpenAIWhisperSTTService(STTSettings settings, int timeoutSeconds, int retryCount)
        {
            _settings = settings;
            _timeoutSeconds = timeoutSeconds;
            _retryCount = retryCount;
        }

        public async Task<ServiceResult<string>> TranscribeAsync(
            STTRequest request, CancellationToken cancellationToken = default)
        {
            if (request?.AudioBytes == null || request.AudioBytes.Length == 0)
            {
                return ServiceResult<string>.Fail(ServiceErrorType.Unknown, "STT request has no audio bytes.");
            }

            if (string.IsNullOrWhiteSpace(_settings.ApiKey))
            {
                return ServiceResult<string>.Fail(ServiceErrorType.Auth, "Whisper STT API key is not configured.");
            }

            string baseUrl = string.IsNullOrWhiteSpace(_settings.Endpoint)
                ? ServiceDefaults.OpenAIBaseUrl
                : _settings.Endpoint.TrimEnd('/');
            string url = ServiceHttp.CombinePath(baseUrl, "audio/transcriptions");

            string format = string.IsNullOrWhiteSpace(request.Format) ? "wav" : request.Format;
            string language = string.IsNullOrWhiteSpace(request.Language) ? _settings.ResolveLanguage() : request.Language;

            ServiceResult<ServiceHttp.HttpPayload> http = await ServiceHttp.SendWithRetryAsync(
                () =>
                {
                    // MultipartFormDataContent must be rebuilt per attempt.
                    var form = new MultipartFormDataContent();

                    var fileContent = new ByteArrayContent(request.AudioBytes);
                    fileContent.Headers.ContentType = new MediaTypeHeaderValue("audio/" + format);
                    form.Add(fileContent, "file", "audio." + format);

                    form.Add(new StringContent(_settings.ResolveModel()), "model");
                    if (!string.IsNullOrWhiteSpace(language))
                    {
                        // Whisper expects an ISO-639-1 code; take the part before any '-'.
                        form.Add(new StringContent(language.Split('-')[0]), "language");
                    }

                    var message = new HttpRequestMessage(HttpMethod.Post, url) { Content = form };
                    message.Headers.TryAddWithoutValidation("Authorization", $"Bearer {_settings.ApiKey}");
                    return message;
                },
                _timeoutSeconds, _retryCount, cancellationToken);

            if (!http.IsSuccess)
            {
                return ServiceResult<string>.Fail(
                    http.ErrorType, $"{http.ErrorMessage} [POST {url}]", http.StatusCode);
            }

            return Parse(http.Value.Body);
        }

        private ServiceResult<string> Parse(byte[] body)
        {
            string raw = Encoding.UTF8.GetString(body ?? new byte[0]);
            try
            {
                JObject root = JObject.Parse(raw);
                string text = (string)root["text"];
                if (text == null)
                {
                    return ServiceResult<string>.Fail(ServiceErrorType.Parse, "Whisper response missing 'text'.");
                }

                return ServiceResult<string>.Ok(text.Trim());
            }
            catch (Newtonsoft.Json.JsonException e)
            {
                return ServiceResult<string>.Fail(ServiceErrorType.Parse, $"Failed to parse Whisper response: {e.Message}");
            }
        }
    }
}
