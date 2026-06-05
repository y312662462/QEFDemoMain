using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using MultiAgentNPC.Config;
using MultiAgentNPC.DebugTools;
using MultiAgentNPC.NPC;
using MultiAgentNPC.Prompts;
using MultiAgentNPC.Quest;
using MultiAgentNPC.Services;
using MultiAgentNPC.UI;
using MultiAgentNPC.Utils;

namespace MultiAgentNPC.Dialogue
{
    /// <summary>
    /// Scene host for the Sprint 6 dialogue pipeline. Wires the real LLM/TTS services,
    /// listens for debug text input and active-NPC changes, runs one
    /// <see cref="DialoguePipeline"/> turn at a time, and mirrors progress into
    /// <see cref="DebugStateStore"/>. Implements <see cref="IDialoguePresenter"/> so the
    /// pipeline can drive subtitles and play audio on the main thread.
    ///
    /// Owns the Idle/InRange terminal states (the pipeline only reports Thinking and
    /// Speaking). Enforces the single-turn rule: debug input is ignored unless the state
    /// is <see cref="DialogueState.InRange"/>. Any failure or cancellation returns to
    /// InRange (NPC still active) or Idle (no active NPC) - never stuck Processing.
    /// </summary>
    [AddComponentMenu("MultiAgentNPC/Dialogue Manager")]
    public class DialogueManager : MonoBehaviour, IDialoguePresenter
    {
        [Header("Service / Scene References")]
        [Tooltip("Source of AI service settings + secrets. Auto-found if left empty.")]
        [SerializeField] private AIServiceConfig aiServiceConfig;

        [Tooltip("Live quest manager owner. Optional; used for prompt selection by quest state.")]
        [SerializeField] private QuestRuntimeHost questHost;

        [Tooltip("Subtitle surface for player/NPC/error lines.")]
        [SerializeField] private SubtitleUI subtitleUI;

        [Tooltip("Audio source that plays the NPC's TTS audio.")]
        [SerializeField] private AudioSource npcAudioSource;

        [Header("Prompt Tuning")]
        [SerializeField] private string targetLanguage = "English";
        [SerializeField] private int maxSentenceCount = 3;
        [SerializeField] private int maxSentenceLength = 40;

        [Header("Behaviour")]
        [Tooltip("Most-recent committed turns sent to the LLM per request.")]
        [SerializeField] private int historyTurnsSent = ConversationHistoryManager.DefaultMaxTurns;

        [Tooltip("Milliseconds a subtitle stays on screen when its TTS fails.")]
        [SerializeField] private int subtitleHoldOnTtsFailureMs = 1200;

        [Tooltip("Log pipeline lifecycle to the Console.")]
        [SerializeField] private bool logEvents = true;

        private ILLMService _llm;
        private ITTSService _tts;
        private PromptManager _promptManager;
        private readonly ConversationHistoryManager _history = new ConversationHistoryManager();

        private DialogueState _state = DialogueState.Idle;
        private DialogueSession _currentSession;
        private int _sessionCounter;
        private AudioClip _currentClip;
        private bool _subscribed;

        private void Start()
        {
            BuildServices();
            Subscribe();
            SyncInitialNpcState();
        }

        private void OnDestroy()
        {
            Unsubscribe();
            CancelActiveTurn();
        }

        private void BuildServices()
        {
            if (aiServiceConfig == null)
            {
                aiServiceConfig = FindFirstObjectByType<AIServiceConfig>();
            }

            if (aiServiceConfig == null)
            {
                Debug.LogError("[DialogueManager] No AIServiceConfig found; LLM/TTS will be unavailable.");
            }
            else
            {
                AIServiceSettings settings = aiServiceConfig.Settings;
                _llm = ServiceFactory.CreateLLMService(settings);
                _tts = ServiceFactory.CreateTTSService(settings);
            }

            _promptManager = new PromptManager();

            if (subtitleUI == null)
            {
                subtitleUI = FindFirstObjectByType<SubtitleUI>();
            }

            if (questHost == null)
            {
                questHost = FindFirstObjectByType<QuestRuntimeHost>();
            }

            if (npcAudioSource == null)
            {
                Debug.LogWarning("[DialogueManager] No AudioSource assigned; NPC audio will not play (subtitles only).");
            }
        }

        private void Subscribe()
        {
            if (_subscribed)
            {
                return;
            }

            DebugEvents.DebugTextSubmitted += OnDebugTextSubmitted;

            NPCManager manager = NPCManager.Instance;
            if (manager != null)
            {
                manager.ActiveNPCChanged += OnActiveNpcChanged;
                manager.ActiveNPCCleared += OnActiveNpcCleared;
            }
            else
            {
                Debug.LogWarning("[DialogueManager] NPCManager.Instance is null at Start; active-NPC events not wired.");
            }

            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            DebugEvents.DebugTextSubmitted -= OnDebugTextSubmitted;

            NPCManager manager = NPCManager.Instance;
            if (manager != null)
            {
                manager.ActiveNPCChanged -= OnActiveNpcChanged;
                manager.ActiveNPCCleared -= OnActiveNpcCleared;
            }

            _subscribed = false;
        }

        private void SyncInitialNpcState()
        {
            NPCController active = NPCManager.Instance != null ? NPCManager.Instance.ActiveNpc : null;
            if (active != null)
            {
                DebugStateStore.Instance.SetCurrentNpc(active.NpcId, active.NpcName);
                SetState(DialogueState.InRange);
            }
            else
            {
                SetState(DialogueState.Idle);
            }
        }

        private void OnActiveNpcChanged(ActiveNPCChangedEventArgs args)
        {
            DebugStateStore.Instance.SetCurrentNpc(args.NpcId, args.NpcName);

            // Only promote to InRange when not mid-turn; a turn in flight keeps its state.
            if (!IsBusy)
            {
                SetState(DialogueState.InRange);
            }
        }

        private void OnActiveNpcCleared(NPCController previous)
        {
            // The active NPC left (or was switched out): cancel any running turn, tear down
            // presentation (audio/subtitles/queue) and drop back to Idle. No History is
            // written and no task result is applied - the commit gate sits past the
            // cancellation check in the pipeline.
            CancelActiveTurn();
            DebugStateStore.Instance.SetCurrentNpc(0, string.Empty);
            SetState(DialogueState.Idle);
        }

        private void OnDebugTextSubmitted(string text)
        {
            SubmitPlayerText(text);
        }

        /// <summary>
        /// True only when a new turn may start: an NPC is active, the state is
        /// <see cref="DialogueState.InRange"/>, and no dialogue turn is currently running.
        /// Shared gate for both Debug text and voice (Sprint 8) input.
        /// </summary>
        public bool CanStartTalking =>
            _state == DialogueState.InRange
            && _currentSession == null
            && NPCManager.Instance != null
            && NPCManager.Instance.ActiveNpc != null;

        /// <summary>
        /// Unified entry point for player text (Debug typing in Sprint 5/6 and STT in
        /// Sprint 8). Applies the single-turn gate and starts a turn. Returns true when a
        /// turn was started. Never throws.
        /// </summary>
        public bool SubmitPlayerText(string text)
        {
            string playerText = text != null ? text.Trim() : string.Empty;
            if (string.IsNullOrEmpty(playerText))
            {
                return false;
            }

            if (!CanStartTalking)
            {
                if (logEvents)
                {
                    Debug.Log($"[DialogueManager] Input ignored: state is {_state} (NPC busy or no active NPC).");
                }
                return false;
            }

            NPCController active = NPCManager.Instance != null ? NPCManager.Instance.ActiveNpc : null;
            if (active == null)
            {
                Debug.Log("[DialogueManager] Input ignored: no active NPC.");
                return false;
            }

            int npcId = active.NpcId;
            if (NPCManager.Instance == null || !NPCManager.Instance.TryGetNpcConfig(npcId, out NPCConfig config) || config == null)
            {
                Debug.LogError($"[DialogueManager] No NPCConfig for active NPC {npcId}; cannot start a turn.");
                return false;
            }

            DebugStateStore.Instance.SetLastSttText(playerText);
            BeginTurn(npcId, config, playerText);
            return true;
        }

        private async void BeginTurn(int npcId, NPCConfig config, string playerText)
        {
            var session = new DialogueSession(++_sessionCounter);
            _currentSession = session;

            DebugStateStore.Instance.SetSession(session.Id, false);

            DialoguePipeline pipeline = BuildPipeline();
            pipeline.IsSessionCurrent = IsCurrentSession;
            Action unwireDiagnostics = WireDiagnostics(pipeline, session);

            if (logEvents)
            {
                Debug.Log($"[DialogueManager] Turn start for NPC {npcId} ({session}): \"{playerText}\".");
            }

            try
            {
                await pipeline.RunTurnAsync(npcId, config, playerText, session);
                if (logEvents)
                {
                    Debug.Log($"[DialogueManager] Turn finished for NPC {npcId} ({session}).");
                }
            }
            catch (OperationCanceledException)
            {
                if (logEvents)
                {
                    Debug.Log($"[DialogueManager] Turn cancelled ({session}: NPC left range or shutdown).");
                }
            }
            catch (Exception e)
            {
                // Only surface errors for the session that is still current; a late failure
                // from an expired session must not overwrite a newer turn's UI/state.
                if (IsCurrentSession(session))
                {
                    Debug.LogError($"[DialogueManager] Unexpected error during turn: {e}");
                    DebugStateStore.Instance.SetLastError(e.Message);
                    subtitleUI?.ShowError("Something went wrong. Please try again.");
                }
                else
                {
                    Debug.LogWarning($"[DialogueManager] Suppressed error from stale {session}: {e.Message}");
                }
            }
            finally
            {
                unwireDiagnostics();

                // A finishing OLD session must never overwrite a NEWER session's state.
                if (IsCurrentSession(session))
                {
                    DebugStateStore.Instance.SetTtsQueueLength(0);
                    _currentSession = null;
                    RestoreTerminalState();
                }

                session.Dispose();
            }
        }

        /// <summary>True while <paramref name="session"/> is still the host's current turn.</summary>
        private bool IsCurrentSession(DialogueSession session)
        {
            return session != null && _currentSession != null && _currentSession.Id == session.Id;
        }

        private DialoguePipeline BuildPipeline()
        {
            QuestManager quests = questHost != null ? questHost.QuestManager : null;
            var resolver = new NPCPromptResolver(_promptManager, quests)
            {
                TargetLanguage = targetLanguage,
                MaxSentenceCount = maxSentenceCount,
                MaxSentenceLength = maxSentenceLength
            };

            return new DialoguePipeline(_llm, _tts, resolver, _history, this)
            {
                SubtitleHoldOnTtsFailureMs = subtitleHoldOnTtsFailureMs
            };
        }

        /// <summary>
        /// Subscribes diagnostics for one turn. Every handler is gated on the turn's
        /// <paramref name="session"/> still being current, so a late event from an expired
        /// session cannot update the UI, subtitles, queue length or debug store. Returns an
        /// action that unsubscribes the exact handlers it added.
        /// </summary>
        private Action WireDiagnostics(DialoguePipeline pipeline, DialogueSession session)
        {
            Action<DialogueState> onState = state => { if (IsCurrentSession(session)) SetState(state); };
            Action<string> onLlmRaw = raw => { if (IsCurrentSession(session)) DebugStateStore.Instance.SetLastLlmRaw(raw); };
            Action<string> onJson = summary => { if (IsCurrentSession(session)) DebugStateStore.Instance.SetLastJsonParse(summary); };
            Action<int> onQueue = length => { if (IsCurrentSession(session)) DebugStateStore.Instance.SetTtsQueueLength(length); };
            Action<string> onError = message =>
            {
                if (!IsCurrentSession(session))
                {
                    return;
                }

                DebugStateStore.Instance.SetLastError(message);
                subtitleUI?.ShowError(message);
            };

            pipeline.StateChanged += onState;
            pipeline.LlmRawReceived += onLlmRaw;
            pipeline.JsonParsed += onJson;
            pipeline.TtsQueueChanged += onQueue;
            pipeline.ErrorOccurred += onError;

            return () =>
            {
                pipeline.StateChanged -= onState;
                pipeline.LlmRawReceived -= onLlmRaw;
                pipeline.JsonParsed -= onJson;
                pipeline.TtsQueueChanged -= onQueue;
                pipeline.ErrorOccurred -= onError;
            };
        }

        private void RestoreTerminalState()
        {
            bool hasActive = NPCManager.Instance != null && NPCManager.Instance.ActiveNpc != null;
            SetState(hasActive ? DialogueState.InRange : DialogueState.Idle);
        }

        private void CancelActiveTurn()
        {
            DialogueSession session = _currentSession;
            if (session != null)
            {
                session.Cancel();
                DebugStateStore.Instance.SetSession(session.Id, true);
            }

            // Rollback presentation immediately so the player sees/hears the turn stop even
            // though the awaiting pipeline may take a frame to unwind. Presentation-only:
            // no History is written and no task result is applied here.
            StopAudioAndClear();
            DebugStateStore.Instance.SetTtsQueueLength(0);
        }

        private bool IsBusy => _state == DialogueState.Thinking || _state == DialogueState.Speaking;

        private void SetState(DialogueState state)
        {
            _state = state;
            DebugStateStore.Instance.SetDialogueState(state.ToString());
        }

        // ----- IDialoguePresenter (main-thread UI + audio) -----

        public void ShowPlayerText(string text) => subtitleUI?.ShowPlayerText(text);

        public void ShowNpcSentence(string text) => subtitleUI?.ShowNpcSentence(text);

        public void ShowError(string text) => subtitleUI?.ShowError(text);

        public void ClearPlayerText() => subtitleUI?.ShowPlayerText(string.Empty);

        /// <summary>
        /// Presentation-only rollback cleanup (Sprint 7). Stops audio, releases the current
        /// clip, clears subtitles and resets local playback state. It never commits History
        /// or touches quest/session state - cancellation/commit gating lives in the host
        /// and pipeline.
        /// </summary>
        public void StopAudioAndClear()
        {
            if (npcAudioSource != null)
            {
                npcAudioSource.Stop();
            }

            CleanupClip(_currentClip);
            _currentClip = null;

            subtitleUI?.Clear();
        }

        public async Task PlaySentenceAsync(byte[] wavBytes, string clipName, CancellationToken cancellationToken)
        {
            if (npcAudioSource == null)
            {
                return;
            }

            AudioClip clip = WavUtility.ToAudioClip(wavBytes, clipName);
            if (clip == null)
            {
                return;
            }

            npcAudioSource.Stop();
            npcAudioSource.clip = clip;
            _currentClip = clip;
            npcAudioSource.Play();

            try
            {
                while (npcAudioSource != null && npcAudioSource.isPlaying)
                {
                    await Task.Delay(50, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                if (npcAudioSource != null)
                {
                    npcAudioSource.Stop();
                }

                CleanupClip(clip);
                throw;
            }

            CleanupClip(clip);
        }

        private void CleanupClip(AudioClip clip)
        {
            if (clip == null)
            {
                return;
            }

            if (npcAudioSource != null && npcAudioSource.clip == clip)
            {
                npcAudioSource.clip = null;
            }

            if (_currentClip == clip)
            {
                _currentClip = null;
            }

            Destroy(clip);
        }
    }
}
