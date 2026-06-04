namespace MultiAgentNPC.Services
{
    /// <summary>OpenAI chat-completion LLM service (GPT-4o by default).</summary>
    public class OpenAILLMService : OpenAICompatibleLLMService
    {
        public OpenAILLMService(LLMSettings settings, int timeoutSeconds, int retryCount)
            : base(settings, timeoutSeconds, retryCount)
        {
        }
    }
}
