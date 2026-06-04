using System.Threading;
using System.Threading.Tasks;

namespace MultiAgentNPC.Services
{
    /// <summary>
    /// Abstraction over an LLM chat-completion provider. The business layer depends
    /// only on this interface, never on a concrete vendor SDK (requirements doc 8.2 / 18.3).
    /// </summary>
    public interface ILLMService
    {
        /// <summary>
        /// Sends a chat completion request and returns the assistant message content
        /// (a strict JSON string when <see cref="LLMRequest.ForceJson"/> is set).
        /// Never throws: failures are returned as a failed <see cref="ServiceResult{T}"/>.
        /// </summary>
        Task<ServiceResult<string>> CompleteAsync(LLMRequest request, CancellationToken cancellationToken = default);
    }
}
