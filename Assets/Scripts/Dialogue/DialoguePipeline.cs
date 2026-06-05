using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using MultiAgentNPC.Config;
using MultiAgentNPC.Quest;
using MultiAgentNPC.Services;

namespace MultiAgentNPC.Dialogue
{
    /// <summary>
    /// Orchestrates one dialogue turn end to end: render NPC prompt -> call the real LLM
    /// (forced JSON) -> parse (with fallback) -> play sentences through an ordered
    /// <see cref="TtsQueuePlayer"/> (Sprint 7) -> commit the turn at a single gated point
    /// after playback finishes.
    ///
    /// Plain C# (no MonoBehaviour). All UI/audio side effects go through
    /// <see cref="IDialoguePresenter"/>; diagnostics are surfaced via events so the host
    /// can mirror them into the debug panel. The host owns the Idle/InRange terminal
    /// states; this class only reports <see cref="DialogueState.Thinking"/> and
    /// <see cref="DialogueState.Speaking"/> while a turn runs.
    ///
    /// Sprint 7: every turn runs under a <see cref="DialogueSession"/>. The single commit
    /// point (<see cref="CommitTurn"/>) is gated on BOTH the session's cancellation token
    /// and on the session still being the host's current session, so a late LLM/TTS
    /// return from an expired session never writes History (or applies future task
    /// results).
    /// </summary>
    public class DialoguePipeline
    {
        private readonly ILLMService _llm;
        private readonly ITTSService _tts;
        private readonly NPCPromptResolver _resolver;
        private readonly ConversationHistoryManager _history;
        private readonly IDialoguePresenter _presenter;

        /// <summary>Milliseconds to hold a subtitle on screen when its TTS fails.</summary>
        public int SubtitleHoldOnTtsFailureMs { get; set; } = 1200;

        /// <summary>
        /// Optional identity gate set by the host. Returns true while the given session is
        /// still the host's current session. When it returns false the commit is skipped
        /// (a stale/expired session must not write History or apply task results).
        /// </summary>
        public Func<DialogueSession, bool> IsSessionCurrent { get; set; }

        /// <summary>Reports the pipeline's internal state transitions (Thinking, Speaking).</summary>
        public event Action<DialogueState> StateChanged;

        /// <summary>Raised with the raw LLM text before parsing.</summary>
        public event Action<string> LlmRawReceived;

        /// <summary>Raised with a short summary of the JSON parse result.</summary>
        public event Action<string> JsonParsed;

        /// <summary>Raised whenever the remaining TTS queue length changes.</summary>
        public event Action<int> TtsQueueChanged;

        /// <summary>Raised with a non-fatal or fatal error message for this turn.</summary>
        public event Action<string> ErrorOccurred;

        /// <summary>
        /// Raised exactly once per turn AFTER the turn has been committed to History
        /// (playback finished and both commit gates passed). Carries an immutable
        /// <see cref="CommittedTurn"/> snapshot. The host uses this to launch post-commit
        /// quest evaluation; the pipeline itself never references the quest system.
        /// </summary>
        public event Action<CommittedTurn> TurnCommitted;

        public DialoguePipeline(
            ILLMService llm,
            ITTSService tts,
            NPCPromptResolver resolver,
            ConversationHistoryManager history,
            IDialoguePresenter presenter)
        {
            _llm = llm;
            _tts = tts;
            _resolver = resolver;
            _history = history;
            _presenter = presenter;
        }

        /// <summary>
        /// Runs a full turn under <paramref name="session"/>. Never throws to the caller
        /// except <see cref="OperationCanceledException"/> (surfaced so the host knows the
        /// turn was cancelled and must not commit). The host is responsible for restoring
        /// the terminal state (InRange/Idle) in its own finally block.
        /// </summary>
        public async Task RunTurnAsync(int npcId, NPCConfig npc, string playerText, DialogueSession session)
        {
            if (session == null)
            {
                ReportError("No dialogue session for this turn.");
                return;
            }

            CancellationToken cancellationToken = session.Token;

            if (npc == null)
            {
                ReportError("No NPC config for this turn.");
                return;
            }

            if (_llm == null)
            {
                ReportError("LLM service is not available.");
                return;
            }

            var pending = new PendingDialogueTurn(npcId, npc.NpcName, playerText);

            RaiseState(DialogueState.Thinking);
            _presenter.ShowPlayerText(playerText);

            // 1. Resolve + render the prompt (never hard-coded; from PromptManager).
            // 'history' here is the conversation BEFORE this turn is committed; it is also
            // forwarded to the post-commit quest evaluation as PriorHistory.
            string history = _history != null ? _history.GetRecentFormatted(npcId) : "(no previous turns)";
            ResolvedPrompt resolved = _resolver != null ? _resolver.Resolve(npc, playerText, history) : null;
            if (resolved == null || !resolved.IsValid)
            {
                ReportError($"Prompt could not be resolved for NPC {npcId}.");
                return;
            }

            pending.RenderedPrompt = resolved.RenderedPrompt;
            cancellationToken.ThrowIfCancellationRequested();

            // 2. Call the real LLM, forcing JSON.
            var messages = new List<LLMMessage>
            {
                LLMMessage.System(resolved.RenderedPrompt),
                LLMMessage.User("Reply with the JSON now.")
            };
            var request = new LLMRequest(messages, forceJson: true);

            ServiceResult<string> llmResult = await _llm.CompleteAsync(request, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            if (!llmResult.IsSuccess)
            {
                ReportError($"LLM call failed: {llmResult.ErrorType} - {llmResult.ErrorMessage}");
                return;
            }

            pending.RawLlm = llmResult.Value;
            RaiseLlmRaw(llmResult.Value);

            // 3. Parse JSON (clean code fences; fall back to a safe reply on failure).
            NPCResponse response = NPCResponseJsonParser.Parse(llmResult.Value, out string parseSummary);
            pending.Parsed = response;
            RaiseJsonParsed(response.IsFallback ? $"{parseSummary} [FALLBACK]" : parseSummary);

            if (!response.HasContent)
            {
                ReportError("Parsed response had no playable sentences.");
                return;
            }

            // 4. Speak each sentence through the ordered TTS queue (one-ahead prefetch).
            RaiseState(DialogueState.Speaking);
            await SpeakAsync(npcId, npc, response, cancellationToken);

            // 5. Single transaction commit point (gated on cancellation + session identity).
            CommitTurn(session, npcId, npc.NpcName, playerText, response, history);
        }

        private async Task SpeakAsync(int npcId, NPCConfig npc, NPCResponse response, CancellationToken cancellationToken)
        {
            var player = new TtsQueuePlayer(_tts, _presenter)
            {
                SubtitleHoldOnTtsFailureMs = SubtitleHoldOnTtsFailureMs
            };

            player.RemainingChanged += RaiseTtsQueue;
            player.ErrorOccurred += ReportError;

            try
            {
                await player.PlayAsync(npcId, npc.TtsVoiceId, response, cancellationToken);
            }
            finally
            {
                player.RemainingChanged -= RaiseTtsQueue;
                player.ErrorOccurred -= ReportError;
            }
        }

        /// <summary>
        /// THE single transaction commit point for a turn. Reached only after playback
        /// completed. Gated on both the session's cancellation token and the host's
        /// session-identity check so a cancelled or stale session never writes History.
        /// </summary>
        private void CommitTurn(
            DialogueSession session,
            int npcId,
            string npcName,
            string playerText,
            NPCResponse response,
            string priorHistory)
        {
            // Cancelled during/after playback => do not commit (requirements 5/6/8).
            session.Token.ThrowIfCancellationRequested();

            // Identity gate: a late return from an expired session must not commit even
            // if its token somehow was not tripped.
            if (IsSessionCurrent != null && !IsSessionCurrent(session))
            {
                Debug.Log($"[DialoguePipeline] Commit skipped: {session} is no longer the current session.");
                return;
            }

            string npcText = response.JoinedText();
            _history?.Commit(npcId, new DialogueTurn(playerText, npcText, response.Sentences));

            // Sprint 9 COMMIT HOOK: the turn is now durable in History (playback finished
            // and both gates passed). Notify the host so it can launch post-commit quest
            // evaluation on its OWN lifecycle. The pipeline never touches the quest system.
            var committed = new CommittedTurn(
                session.Id, npcId, npcName, playerText, npcText, priorHistory, DateTime.UtcNow);
            RaiseTurnCommitted(committed);
        }

        private void ReportError(string message)
        {
            Debug.LogWarning($"[DialoguePipeline] {message}");
            try
            {
                ErrorOccurred?.Invoke(message);
            }
            catch (Exception e)
            {
                Debug.LogError($"[DialoguePipeline] An ErrorOccurred subscriber threw: {e}");
            }
        }

        private void RaiseState(DialogueState state) => SafeInvoke(StateChanged, state, nameof(StateChanged));
        private void RaiseLlmRaw(string raw) => SafeInvoke(LlmRawReceived, raw, nameof(LlmRawReceived));
        private void RaiseJsonParsed(string summary) => SafeInvoke(JsonParsed, summary, nameof(JsonParsed));
        private void RaiseTtsQueue(int length) => SafeInvoke(TtsQueueChanged, length, nameof(TtsQueueChanged));
        private void RaiseTurnCommitted(CommittedTurn turn) => SafeInvoke(TurnCommitted, turn, nameof(TurnCommitted));

        private static void SafeInvoke<T>(Action<T> handler, T value, string name)
        {
            if (handler == null)
            {
                return;
            }

            try
            {
                handler(value);
            }
            catch (Exception e)
            {
                Debug.LogError($"[DialoguePipeline] A {name} subscriber threw: {e}");
            }
        }
    }
}
