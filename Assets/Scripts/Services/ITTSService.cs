using System.Threading;
using System.Threading.Tasks;

namespace MultiAgentNPC.Services
{
    /// <summary>
    /// Abstraction over a text-to-speech provider. Returns audio as a WAV/PCM byte
    /// array so callers can decode it to an AudioClip on the main thread.
    /// </summary>
    public interface ITTSService
    {
        /// <summary>
        /// Synthesizes speech for the given text. Never throws: failures are returned
        /// as a failed <see cref="ServiceResult{T}"/>.
        /// </summary>
        Task<ServiceResult<byte[]>> SynthesizeAsync(TTSRequest request, CancellationToken cancellationToken = default);
    }
}
