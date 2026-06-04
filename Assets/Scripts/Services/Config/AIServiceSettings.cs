using System;
using UnityEngine;

namespace MultiAgentNPC.Services
{
    public enum LLMProvider
    {
        OpenAI,
        DeepSeek
    }

    public enum STTProvider
    {
        OpenAIWhisper,
        AzureSTT
    }

    public enum TTSProvider
    {
        AzureTTS
    }

    /// <summary>
    /// Provider default endpoints / models, kept in one place so the settings UI
    /// can fall back to sensible values when optional fields are left blank.
    /// </summary>
    public static class ServiceDefaults
    {
        public const string OpenAIBaseUrl = "https://api.openai.com/v1";
        public const string OpenAIModel = "gpt-4o";

        public const string DeepSeekBaseUrl = "https://api.deepseek.com/v1";
        public const string DeepSeekModel = "deepseek-chat";

        public const string WhisperModel = "whisper-1";
        public const string DefaultSttLanguage = "en-US";

        public const string DefaultTtsVoice = "en-US-JennyNeural";
        public const string DefaultTtsOutputFormat = "riff-24khz-16bit-mono-pcm";
    }

    /// <summary>LLM service configuration filled in the Inspector.</summary>
    [Serializable]
    public class LLMSettings
    {
        public LLMProvider Provider = LLMProvider.OpenAI;

        [Tooltip("LLM API key. Local only - never commit a real key.")]
        public string ApiKey = string.Empty;

        [Tooltip("Optional. Leave blank to use the provider default base URL.")]
        public string BaseUrl = string.Empty;

        [Tooltip("Optional. Leave blank to use the provider default model.")]
        public string Model = string.Empty;

        [Range(0f, 2f)]
        public float Temperature = 0.7f;

        public int MaxTokens = 512;

        public string ResolveBaseUrl()
        {
            if (!string.IsNullOrWhiteSpace(BaseUrl))
            {
                return BaseUrl.TrimEnd('/');
            }

            return (Provider == LLMProvider.DeepSeek
                ? ServiceDefaults.DeepSeekBaseUrl
                : ServiceDefaults.OpenAIBaseUrl).TrimEnd('/');
        }

        public string ResolveModel()
        {
            if (!string.IsNullOrWhiteSpace(Model))
            {
                return Model;
            }

            return Provider == LLMProvider.DeepSeek
                ? ServiceDefaults.DeepSeekModel
                : ServiceDefaults.OpenAIModel;
        }
    }

    /// <summary>STT service configuration filled in the Inspector.</summary>
    [Serializable]
    public class STTSettings
    {
        public STTProvider Provider = STTProvider.OpenAIWhisper;

        [Tooltip("STT API key. Local only - never commit a real key.")]
        public string ApiKey = string.Empty;

        [Tooltip("Azure region, e.g. 'eastus'. Required for Azure STT.")]
        public string Region = string.Empty;

        [Tooltip("Optional explicit endpoint. Overrides Region-derived URL when set.")]
        public string Endpoint = string.Empty;

        [Tooltip("Recognition language, e.g. 'en-US'.")]
        public string Language = ServiceDefaults.DefaultSttLanguage;

        [Tooltip("Whisper model name (OpenAI Whisper only).")]
        public string Model = ServiceDefaults.WhisperModel;

        public string ResolveLanguage() =>
            string.IsNullOrWhiteSpace(Language) ? ServiceDefaults.DefaultSttLanguage : Language;

        public string ResolveModel() =>
            string.IsNullOrWhiteSpace(Model) ? ServiceDefaults.WhisperModel : Model;
    }

    /// <summary>TTS service configuration filled in the Inspector.</summary>
    [Serializable]
    public class TTSSettings
    {
        public TTSProvider Provider = TTSProvider.AzureTTS;

        [Tooltip("Azure TTS key. Local only - never commit a real key.")]
        public string ApiKey = string.Empty;

        [Tooltip("Azure region, e.g. 'eastus'. Required for Azure TTS.")]
        public string Region = string.Empty;

        [Tooltip("Optional explicit endpoint. Overrides Region-derived URL when set.")]
        public string Endpoint = string.Empty;

        [Tooltip("Azure voice name, e.g. 'en-US-JennyNeural'.")]
        public string Voice = ServiceDefaults.DefaultTtsVoice;

        [Tooltip("Azure output format header value (WAV/PCM recommended).")]
        public string OutputFormat = ServiceDefaults.DefaultTtsOutputFormat;

        public string ResolveVoice() =>
            string.IsNullOrWhiteSpace(Voice) ? ServiceDefaults.DefaultTtsVoice : Voice;

        public string ResolveOutputFormat() =>
            string.IsNullOrWhiteSpace(OutputFormat) ? ServiceDefaults.DefaultTtsOutputFormat : OutputFormat;
    }

    /// <summary>
    /// Aggregate of all AI service settings, plus shared network options.
    /// Plain serializable object so it can be embedded in a MonoBehaviour (this
    /// sprint) or later in a ScriptableObject without code changes.
    /// </summary>
    [Serializable]
    public class AIServiceSettings
    {
        [Header("Shared Network")]
        [Tooltip("Per-request timeout in seconds.")]
        public int TimeoutSeconds = 30;

        [Tooltip("Number of retries on transient failures (network/timeout/5xx).")]
        public int RetryCount = 1;

        [Header("LLM")]
        public LLMSettings Llm = new LLMSettings();

        [Header("STT")]
        public STTSettings Stt = new STTSettings();

        [Header("TTS")]
        public TTSSettings Tts = new TTSSettings();
    }
}
