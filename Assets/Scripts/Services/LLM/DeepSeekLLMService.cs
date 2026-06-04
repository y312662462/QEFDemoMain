namespace MultiAgentNPC.Services
{
    /// <summary>DeepSeek chat-completion LLM service (OpenAI-compatible API).</summary>
    public class DeepSeekLLMService : OpenAICompatibleLLMService
    {
        public DeepSeekLLMService(LLMSettings settings, int timeoutSeconds, int retryCount)
            : base(settings, timeoutSeconds, retryCount)
        {
        }
    }
}
