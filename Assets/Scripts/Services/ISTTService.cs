using System.Threading;
using System.Threading.Tasks;

namespace MultiAgentNPC.Services
{
    /// <summary>
    /// Abstraction over a speech-to-text provider. Takes raw audio bytes and returns
    /// recognized text. Microphone capture is out of scope for this sprint; callers
    /// supply the audio bytes (e.g. from a test WAV file).
    /// </summary>
    public interface ISTTService
    {
        /// <summary>
        /// Transcribes the given audio. Never throws: failures are returned as a
        /// failed <see cref="ServiceResult{T}"/>.
        /// </summary>
        Task<ServiceResult<string>> TranscribeAsync(STTRequest request, CancellationToken cancellationToken = default);
    }
}
