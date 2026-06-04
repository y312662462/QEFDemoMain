using System.Net.Http;
using System.Net.Http.Headers;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MultiAgentNPC.Services
{
    /// <summary>
    /// Text-to-speech via the Azure Speech REST endpoint. Sends SSML and returns the
    /// synthesized audio bytes (WAV/PCM by default) for the caller to decode/play.
    /// </summary>
    public class AzureTTSService : ITTSService
    {
        private readonly TTSSettings _settings;
        private readonly int _timeoutSeconds;
        private readonly int _retryCount;

        public AzureTTSService(TTSSettings settings, int timeoutSeconds, int retryCount)
        {
            _settings = settings;
            _timeoutSeconds = timeoutSeconds;
            _retryCount = retryCount;
        }

        public async Task<ServiceResult<byte[]>> SynthesizeAsync(
            TTSRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Text))
            {
                return ServiceResult<byte[]>.Fail(ServiceErrorType.Unknown, "TTS request text is empty.");
            }

            if (string.IsNullOrWhiteSpace(_settings.ApiKey))
            {
                return ServiceResult<byte[]>.Fail(ServiceErrorType.Auth, "Azure TTS API key is not configured.");
            }

            if (string.IsNullOrWhiteSpace(_settings.Region) && string.IsNullOrWhiteSpace(_settings.Endpoint))
            {
                return ServiceResult<byte[]>.Fail(
                    ServiceErrorType.Unknown, "Azure TTS requires a Region or explicit Endpoint.");
            }

            string baseUrl = string.IsNullOrWhiteSpace(_settings.Endpoint)
                ? $"https://{_settings.Region}.tts.speech.microsoft.com"
                : _settings.Endpoint.TrimEnd('/');
            string url = ServiceHttp.CombinePath(baseUrl, "cognitiveservices/v1");

            string voice = string.IsNullOrWhiteSpace(request.VoiceOverride)
                ? _settings.ResolveVoice()
                : request.VoiceOverride;
            string ssml = BuildSsml(request.Text, voice);
            string outputFormat = _settings.ResolveOutputFormat();

            ServiceResult<ServiceHttp.HttpPayload> http = await ServiceHttp.SendWithRetryAsync(
                () =>
                {
                    var content = new StringContent(ssml, Encoding.UTF8);
                    content.Headers.ContentType = new MediaTypeHeaderValue("application/ssml+xml");

                    var message = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
                    message.Headers.TryAddWithoutValidation("Ocp-Apim-Subscription-Key", _settings.ApiKey);
                    message.Headers.TryAddWithoutValidation("X-Microsoft-OutputFormat", outputFormat);
                    message.Headers.TryAddWithoutValidation("User-Agent", "MultiAgentNPC");
                    return message;
                },
                _timeoutSeconds, _retryCount, cancellationToken);

            if (!http.IsSuccess)
            {
                // Include the attempted URL so 404/routing issues are diagnosable
                // (e.g. wrong Region, or Endpoint set to the portal keys URL instead
                // of the "{region}.tts.speech.microsoft.com" host).
                return ServiceResult<byte[]>.Fail(
                    http.ErrorType, $"{http.ErrorMessage} [POST {url}]", http.StatusCode);
            }

            byte[] audio = http.Value.Body;
            if (audio == null || audio.Length == 0)
            {
                return ServiceResult<byte[]>.Fail(ServiceErrorType.Parse, "Azure TTS returned empty audio.");
            }

            return ServiceResult<byte[]>.Ok(audio);
        }

        private static string BuildSsml(string text, string voice)
        {
            // Derive locale from the voice name (e.g. "en-US-JennyNeural" -> "en-US").
            string locale = "en-US";
            string[] parts = voice.Split('-');
            if (parts.Length >= 2)
            {
                locale = parts[0] + "-" + parts[1];
            }

            string safeText = SecurityElement.Escape(text);
            return $"<speak version='1.0' xml:lang='{locale}'>" +
                   $"<voice xml:lang='{locale}' name='{voice}'>{safeText}</voice></speak>";
        }
    }
}
