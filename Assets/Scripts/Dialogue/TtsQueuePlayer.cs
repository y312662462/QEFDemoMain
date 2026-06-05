using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using MultiAgentNPC.Services;

namespace MultiAgentNPC.Dialogue
{
    /// <summary>
    /// Plays an <see cref="NPCResponse"/>'s sentences as a strictly ordered queue
    /// (Sprint 7). Uses one-ahead prefetch: while sentence <c>i</c> is playing, the TTS
    /// for sentence <c>i+1</c> is already being synthesized. Playback order always
    /// matches the JSON <c>sentences</c> order because a single index produces and a
    /// single loop consumes.
    ///
    /// All side effects are gated on the turn's <see cref="CancellationToken"/>: after
    /// every await the token is re-checked, so a late TTS result from a cancelled session
    /// is discarded before it can play or touch the UI. A failed TTS request is
    /// non-blocking - the subtitle is held on screen for a short hold instead.
    ///
    /// Pure orchestration: it never writes History or touches quest state. The remaining
    /// count is reported via <see cref="RemainingChanged"/> so the host can mirror the
    /// queue length into the debug panel.
    /// </summary>
    public class TtsQueuePlayer
    {
        private readonly ITTSService _tts;
        private readonly IDialoguePresenter _presenter;

        /// <summary>Milliseconds to hold a subtitle on screen when its TTS fails.</summary>
        public int SubtitleHoldOnTtsFailureMs { get; set; } = 1200;

        /// <summary>
        /// Reports the number of sentences not yet finished playing (queue length).
        /// Fires with the total at start, then total-1, ... down to 0.
        /// </summary>
        public event Action<int> RemainingChanged;

        /// <summary>Reports a non-fatal TTS failure (subtitle shown without audio).</summary>
        public event Action<string> ErrorOccurred;

        public TtsQueuePlayer(ITTSService tts, IDialoguePresenter presenter)
        {
            _tts = tts;
            _presenter = presenter;
        }

        /// <summary>
        /// Synthesizes and plays every sentence in order. Throws
        /// <see cref="OperationCanceledException"/> if the session is cancelled mid-flight
        /// (so the caller skips the commit). Never commits anything itself.
        /// </summary>
        public async Task PlayAsync(int npcId, string voiceId, NPCResponse response, CancellationToken cancellationToken)
        {
            int total = response.Sentences.Count;
            RaiseRemaining(total);

            // Kick off synthesis for the first sentence; the loop prefetches the next.
            Task<ServiceResult<byte[]>> pending = StartSynth(0, voiceId, response, cancellationToken);

            try
            {
                for (int i = 0; i < total; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    NPCSentence sentence = response.Sentences[i];

                    // Await the synth started for THIS sentence (may be null when skipped).
                    ServiceResult<byte[]> tts = pending != null ? await pending : null;
                    cancellationToken.ThrowIfCancellationRequested();

                    // Prefetch the NEXT sentence so its TTS overlaps this one's playback.
                    pending = (i + 1 < total)
                        ? StartSynth(i + 1, voiceId, response, cancellationToken)
                        : null;

                    if (sentence == null || string.IsNullOrWhiteSpace(sentence.Text))
                    {
                        RaiseRemaining(total - (i + 1));
                        continue;
                    }

                    _presenter.ShowNpcSentence(sentence.Text);

                    if (tts != null && tts.IsSuccess && tts.Value != null && tts.Value.Length > 0)
                    {
                        await _presenter.PlaySentenceAsync(tts.Value, $"npc{npcId}_s{i}", cancellationToken);
                        cancellationToken.ThrowIfCancellationRequested();
                    }
                    else
                    {
                        string reason = tts != null
                            ? $"{tts.ErrorType} - {tts.ErrorMessage}"
                            : "TTS service unavailable";
                        RaiseError($"TTS failed (subtitle only): {reason}");
                        await DelaySafe(SubtitleHoldOnTtsFailureMs, cancellationToken);
                    }

                    RaiseRemaining(total - (i + 1));
                }
            }
            finally
            {
                // A prefetched-but-unconsumed synth (e.g. on cancellation) must be observed
                // so a late OperationCanceledException does not surface as unobserved.
                ObserveAndDiscard(pending);
            }
        }

        private Task<ServiceResult<byte[]>> StartSynth(
            int index, string voiceId, NPCResponse response, CancellationToken cancellationToken)
        {
            if (_tts == null || index < 0 || index >= response.Sentences.Count)
            {
                return null;
            }

            NPCSentence sentence = response.Sentences[index];
            if (sentence == null || string.IsNullOrWhiteSpace(sentence.Text))
            {
                return null;
            }

            return _tts.SynthesizeAsync(new TTSRequest(sentence.Text, voiceId), cancellationToken);
        }

        private static async Task DelaySafe(int milliseconds, CancellationToken cancellationToken)
        {
            if (milliseconds <= 0)
            {
                return;
            }

            await Task.Delay(milliseconds, cancellationToken);
        }

        private static void ObserveAndDiscard(Task task)
        {
            if (task == null)
            {
                return;
            }

            task.ContinueWith(
                t => { _ = t.Exception; },
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        private void RaiseRemaining(int remaining)
        {
            int safe = remaining < 0 ? 0 : remaining;
            try
            {
                RemainingChanged?.Invoke(safe);
            }
            catch (Exception e)
            {
                Debug.LogError($"[TtsQueuePlayer] A RemainingChanged subscriber threw: {e}");
            }
        }

        private void RaiseError(string message)
        {
            Debug.LogWarning($"[TtsQueuePlayer] {message}");
            try
            {
                ErrorOccurred?.Invoke(message);
            }
            catch (Exception e)
            {
                Debug.LogError($"[TtsQueuePlayer] An ErrorOccurred subscriber threw: {e}");
            }
        }
    }
}
