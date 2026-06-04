using UnityEngine;

namespace MultiAgentNPC.Services
{
    /// <summary>
    /// Builds concrete service instances from <see cref="AIServiceSettings"/> based on
    /// the selected providers. The business layer asks the factory for an interface and
    /// stays unaware of which vendor implementation it receives (requirements doc 18.3).
    /// Returns null and logs an error for unknown providers / missing settings.
    /// </summary>
    public static class ServiceFactory
    {
        public static ILLMService CreateLLMService(AIServiceSettings settings)
        {
            if (settings?.Llm == null)
            {
                Debug.LogError("[ServiceFactory] AIServiceSettings.Llm is null.");
                return null;
            }

            switch (settings.Llm.Provider)
            {
                case LLMProvider.OpenAI:
                    return new OpenAILLMService(settings.Llm, settings.TimeoutSeconds, settings.RetryCount);
                case LLMProvider.DeepSeek:
                    return new DeepSeekLLMService(settings.Llm, settings.TimeoutSeconds, settings.RetryCount);
                default:
                    Debug.LogError($"[ServiceFactory] Unknown LLM provider: {settings.Llm.Provider}.");
                    return null;
            }
        }

        public static ISTTService CreateSTTService(AIServiceSettings settings)
        {
            if (settings?.Stt == null)
            {
                Debug.LogError("[ServiceFactory] AIServiceSettings.Stt is null.");
                return null;
            }

            switch (settings.Stt.Provider)
            {
                case STTProvider.OpenAIWhisper:
                    return new OpenAIWhisperSTTService(settings.Stt, settings.TimeoutSeconds, settings.RetryCount);
                case STTProvider.AzureSTT:
                    return new AzureSTTService(settings.Stt, settings.TimeoutSeconds, settings.RetryCount);
                default:
                    Debug.LogError($"[ServiceFactory] Unknown STT provider: {settings.Stt.Provider}.");
                    return null;
            }
        }

        public static ITTSService CreateTTSService(AIServiceSettings settings)
        {
            if (settings?.Tts == null)
            {
                Debug.LogError("[ServiceFactory] AIServiceSettings.Tts is null.");
                return null;
            }

            switch (settings.Tts.Provider)
            {
                case TTSProvider.AzureTTS:
                    return new AzureTTSService(settings.Tts, settings.TimeoutSeconds, settings.RetryCount);
                default:
                    Debug.LogError($"[ServiceFactory] Unknown TTS provider: {settings.Tts.Provider}.");
                    return null;
            }
        }
    }
}
