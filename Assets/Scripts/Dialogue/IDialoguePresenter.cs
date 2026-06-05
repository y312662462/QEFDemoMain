using System.Threading;
using System.Threading.Tasks;

namespace MultiAgentNPC.Dialogue
{
    /// <summary>
    /// Output surface for the dialogue pipeline. Keeps <see cref="DialoguePipeline"/>
    /// free of Unity/UI types: the host (a MonoBehaviour) implements this to render
    /// subtitles and play audio on the main thread.
    /// </summary>
    public interface IDialoguePresenter
    {
        /// <summary>Shows the player's submitted text.</summary>
        void ShowPlayerText(string text);

        /// <summary>Shows the NPC's current sentence.</summary>
        void ShowNpcSentence(string text);

        /// <summary>
        /// Drives per-sentence NPC visuals (Sprint 10): play the Animator action for
        /// <paramref name="actionId"/> and apply the reserved <paramref name="expressionId"/>.
        /// Called as each sentence starts. Implementations must run on the Unity main thread
        /// and must never throw; an unknown action falls back to Idle and a missing
        /// expression resource is ignored.
        /// </summary>
        void PlaySentenceVisuals(int actionId, int expressionId);

        /// <summary>Shows an error message to the player.</summary>
        void ShowError(string text);

        /// <summary>Clears the player line (e.g. when a new turn starts).</summary>
        void ClearPlayerText();

        /// <summary>
        /// Presentation-only cleanup used on rollback (Sprint 7): stop the NPC audio
        /// source, release the current clip, clear subtitles and reset local playback
        /// state. Must NOT commit History, modify quest state or run rollback logic - it
        /// only tears down what is currently on screen / playing. Safe to call on the main
        /// thread at any time.
        /// </summary>
        void StopAudioAndClear();

        /// <summary>
        /// Decodes the WAV bytes and plays them on the NPC audio source, completing when
        /// playback finishes or <paramref name="cancellationToken"/> is cancelled. Must
        /// run on the Unity main thread. Implementations should not throw; a decode/play
        /// failure should simply complete (the caller already showed the subtitle).
        /// </summary>
        Task PlaySentenceAsync(byte[] wavBytes, string clipName, CancellationToken cancellationToken);
    }
}
