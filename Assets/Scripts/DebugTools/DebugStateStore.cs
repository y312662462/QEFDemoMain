using System;

namespace MultiAgentNPC.DebugTools
{
    /// <summary>
    /// Central, lightweight record of the current system state for on-screen
    /// observability (Sprint 5). Business modules push the latest values here via the
    /// typed setters; <c>DebugPanelUI</c> is the single consumer and rebuilds its text
    /// from the <see cref="Changed"/> event.
    ///
    /// Deliberately a plain C# singleton: no MonoBehaviour, no UnityEngine.Object
    /// references, no DontDestroyOnLoad. It only holds typed properties, typed setters
    /// and a change notification, so later modules can update it without touching any
    /// Text/TMP component.
    /// </summary>
    public sealed class DebugStateStore
    {
        private static readonly DebugStateStore _instance = new DebugStateStore();

        /// <summary>Process-wide shared instance.</summary>
        public static DebugStateStore Instance => _instance;

        private DebugStateStore()
        {
        }

        /// <summary>Raised after any setter changes a value. Argument is this store.</summary>
        public event Action<DebugStateStore> Changed;

        /// <summary>Active NPC id, or 0 when none.</summary>
        public int CurrentNpcId { get; private set; }

        /// <summary>Active NPC display name, or empty.</summary>
        public string CurrentNpcName { get; private set; } = string.Empty;

        /// <summary>Current quest id, or 0 when none.</summary>
        public int CurrentQuestId { get; private set; }

        /// <summary>Current quest display name, or empty.</summary>
        public string CurrentQuestName { get; private set; } = string.Empty;

        /// <summary>Dialogue state placeholder (real state machine arrives in a later sprint).</summary>
        public string DialogueState { get; private set; } = "(none)";

        /// <summary>Most recent speech-to-text transcript.</summary>
        public string LastSttText { get; private set; } = string.Empty;

        /// <summary>True while the microphone is actively recording.</summary>
        public bool Recording { get; private set; }

        /// <summary>Elapsed seconds of the current/last recording.</summary>
        public float RecordingSeconds { get; private set; }

        /// <summary>Most recent STT error message (empty when none).</summary>
        public string LastSttError { get; private set; } = string.Empty;

        /// <summary>Most recent raw LLM response (before parsing).</summary>
        public string LastLlmRaw { get; private set; } = string.Empty;

        /// <summary>Most recent JSON parse result summary.</summary>
        public string LastJsonParse { get; private set; } = string.Empty;

        /// <summary>Most recent quest evaluation verdict summary.</summary>
        public string LastQuestVerdict { get; private set; } = string.Empty;

        /// <summary>Current dialogue session id (0 when no turn has started).</summary>
        public int SessionId { get; private set; }

        /// <summary>True when the current dialogue session has been cancelled.</summary>
        public bool SessionCancelled { get; private set; }

        /// <summary>Current TTS queue length.</summary>
        public int TtsQueueLength { get; private set; }

        /// <summary>Most recent error message surfaced by any module.</summary>
        public string LastError { get; private set; } = string.Empty;

        /// <summary>UTC time of the last successful setter call.</summary>
        public DateTime LastUpdatedUtc { get; private set; }

        public void SetCurrentNpc(int npcId, string npcName)
        {
            CurrentNpcId = npcId;
            CurrentNpcName = npcName ?? string.Empty;
            RaiseChanged();
        }

        public void SetCurrentQuest(int questId, string questName)
        {
            CurrentQuestId = questId;
            CurrentQuestName = questName ?? string.Empty;
            RaiseChanged();
        }

        public void SetDialogueState(string state)
        {
            DialogueState = state ?? string.Empty;
            RaiseChanged();
        }

        public void SetLastSttText(string text)
        {
            LastSttText = text ?? string.Empty;
            RaiseChanged();
        }

        public void SetRecording(bool recording, float seconds)
        {
            Recording = recording;
            RecordingSeconds = seconds < 0f ? 0f : seconds;
            RaiseChanged();
        }

        public void SetLastSttError(string error)
        {
            LastSttError = error ?? string.Empty;
            RaiseChanged();
        }

        public void SetLastLlmRaw(string raw)
        {
            LastLlmRaw = raw ?? string.Empty;
            RaiseChanged();
        }

        public void SetLastJsonParse(string parsed)
        {
            LastJsonParse = parsed ?? string.Empty;
            RaiseChanged();
        }

        public void SetLastQuestVerdict(string verdict)
        {
            LastQuestVerdict = verdict ?? string.Empty;
            RaiseChanged();
        }

        public void SetSession(int sessionId, bool cancelled)
        {
            SessionId = sessionId;
            SessionCancelled = cancelled;
            RaiseChanged();
        }

        public void SetTtsQueueLength(int length)
        {
            TtsQueueLength = length < 0 ? 0 : length;
            RaiseChanged();
        }

        public void SetLastError(string error)
        {
            LastError = error ?? string.Empty;
            RaiseChanged();
        }

        /// <summary>Resets every field back to its initial value and notifies subscribers.</summary>
        public void Reset()
        {
            CurrentNpcId = 0;
            CurrentNpcName = string.Empty;
            CurrentQuestId = 0;
            CurrentQuestName = string.Empty;
            DialogueState = "(none)";
            LastSttText = string.Empty;
            Recording = false;
            RecordingSeconds = 0f;
            LastSttError = string.Empty;
            LastLlmRaw = string.Empty;
            LastJsonParse = string.Empty;
            LastQuestVerdict = string.Empty;
            SessionId = 0;
            SessionCancelled = false;
            TtsQueueLength = 0;
            LastError = string.Empty;
            RaiseChanged();
        }

        private void RaiseChanged()
        {
            LastUpdatedUtc = DateTime.UtcNow;

            // Subscriber exceptions must not corrupt the shared store or stop other
            // modules from reporting state; swallow and let the next update proceed.
            Action<DebugStateStore> handlers = Changed;
            if (handlers == null)
            {
                return;
            }

            foreach (Action<DebugStateStore> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(this);
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError($"[DebugStateStore] A Changed subscriber threw: {e}");
                }
            }
        }
    }
}
