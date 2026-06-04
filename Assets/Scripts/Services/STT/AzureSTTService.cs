using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace MultiAgentNPC.Services
{
    /// <summary>
    /// Speech-to-text via the Azure Speech "short audio" REST endpoint.
    /// Sends a single WAV (PCM 16kHz mono recommended) and reads back DisplayText.
    /// </summary>
    public class AzureSTTService : ISTTService
    {
        private readonly STTSettings _settings;
        private readonly int _timeoutSeconds;
        private readonly int _retryCount;

        public AzureSTTService(STTSettings settings, int timeoutSeconds, int retryCount)
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
                return ServiceResult<string>.Fail(ServiceErrorType.Auth, "Azure STT API key is not configured.");
            }

            if (string.IsNullOrWhiteSpace(_settings.Region) && string.IsNullOrWhiteSpace(_settings.Endpoint))
            {
                return ServiceResult<string>.Fail(
                    ServiceErrorType.Unknown, "Azure STT requires a Region or explicit Endpoint.");
            }

            string baseUrl = string.IsNullOrWhiteSpace(_settings.Endpoint)
                ? $"https://{_settings.Region}.stt.speech.microsoft.com"
                : _settings.Endpoint.TrimEnd('/');
            string language = string.IsNullOrWhiteSpace(request.Language) ? _settings.ResolveLanguage() : request.Language;
            string endpoint = ServiceHttp.CombinePath(baseUrl, "speech/recognition/conversation/cognitiveservices/v1");
            string url = $"{endpoint}?language={language}&format=detailed";

            ServiceResult<ServiceHttp.HttpPayload> http = await ServiceHttp.SendWithRetryAsync(
                () =>
                {
                    var content = new ByteArrayContent(request.AudioBytes);
                    // For WAV (RIFF), Azure reads the sample rate from the header, so we
                    // don't pin samplerate here (avoids mismatches with e.g. 24kHz TTS output).
                    content.Headers.ContentType =
                        MediaTypeHeaderValue.Parse("audio/wav; codecs=audio/pcm");

                    var message = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
                    message.Headers.TryAddWithoutValidation("Ocp-Apim-Subscription-Key", _settings.ApiKey);
                    message.Headers.TryAddWithoutValidation("Accept", "application/json");
                    return message;
                },
                _timeoutSeconds, _retryCount, cancellationToken);

            if (!http.IsSuccess)
            {
                // Include the attempted URL so routing/region issues are diagnosable.
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
                string status = (string)root["RecognitionStatus"];
                if (!string.IsNullOrEmpty(status) && status != "Success")
                {
                    return ServiceResult<string>.Fail(
                        ServiceErrorType.Parse, $"Azure STT recognition status: {status}.");
                }

                string text = (string)root["DisplayText"];
                if (string.IsNullOrEmpty(text))
                {
                    // 'detailed' format also exposes NBest[0].Display.
                    text = (string)root.SelectToken("NBest[0].Display");
                }

                if (string.IsNullOrEmpty(text))
                {
                    return ServiceResult<string>.Fail(ServiceErrorType.Parse, "Azure STT response missing DisplayText.");
                }

                return ServiceResult<string>.Ok(text.Trim());
            }
            catch (Newtonsoft.Json.JsonException e)
            {
                return ServiceResult<string>.Fail(ServiceErrorType.Parse, $"Failed to parse Azure STT response: {e.Message}");
            }
        }
    }
}
