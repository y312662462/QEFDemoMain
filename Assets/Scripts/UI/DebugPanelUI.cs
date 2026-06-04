using System.Text;
using TMPro;
using UnityEngine;
using MultiAgentNPC.DebugTools;

namespace MultiAgentNPC.UI
{
    /// <summary>
    /// On-screen debug panel (Sprint 5). Toggled with F1, it renders a snapshot of the
    /// <see cref="DebugStateStore"/>: current NPC, current quest, dialogue state
    /// placeholder, recent STT/LLM raw/JSON/quest verdict, TTS queue length and the last
    /// error.
    ///
    /// Rebuild strategy: the store's <see cref="DebugStateStore.Changed"/> only flips a
    /// dirty flag; <see cref="Update"/> rebuilds the TMP text at most once per frame and
    /// only when dirty (and visible). It never rebuilds unconditionally every frame.
    /// </summary>
    [AddComponentMenu("MultiAgentNPC/UI/Debug Panel UI")]
    public class DebugPanelUI : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Root object toggled by the F1 key. Defaults to this GameObject.")]
        [SerializeField] private GameObject panelRoot;

        [Tooltip("Multiline label that displays the formatted debug snapshot.")]
        [SerializeField] private TMP_Text contentText;

        [Header("Behaviour")]
        [Tooltip("Key that shows/hides the panel.")]
        [SerializeField] private KeyCode toggleKey = KeyCode.F1;

        [Tooltip("Show the panel when the scene starts.")]
        [SerializeField] private bool startVisible;

        private DebugStateStore _store;
        private bool _dirty = true;
        private bool _visible;

        private void Awake()
        {
            // The script must keep running so F1 still works; never toggle our own
            // GameObject. Put DebugPanelUI on an always-active object (e.g. the Canvas)
            // and point panelRoot at a separate child panel.
            if (panelRoot == gameObject)
            {
                Debug.LogWarning(
                    "[DebugPanelUI] panelRoot is set to this same GameObject; toggling it would " +
                    "disable the F1 handler. Assign a separate child panel instead. Ignoring panelRoot.");
                panelRoot = null;
            }
        }

        private void OnEnable()
        {
            _store = DebugStateStore.Instance;
            _store.Changed += OnStoreChanged;
            _dirty = true;

            SetVisible(startVisible);
        }

        private void OnDisable()
        {
            if (_store != null)
            {
                _store.Changed -= OnStoreChanged;
                _store = null;
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                SetVisible(!_visible);
            }

            // Only rebuild when something changed and the panel is actually showing.
            if (_visible && _dirty)
            {
                Rebuild();
            }
        }

        private void OnStoreChanged(DebugStateStore store)
        {
            _dirty = true;
        }

        private void SetVisible(bool visible)
        {
            _visible = visible;

            if (panelRoot != null)
            {
                panelRoot.SetActive(visible);
            }
            else if (contentText != null)
            {
                contentText.gameObject.SetActive(visible);
            }

            // Force a fresh rebuild the next frame after being shown.
            if (visible)
            {
                _dirty = true;
            }
        }

        private void Rebuild()
        {
            _dirty = false;

            if (contentText == null)
            {
                return;
            }

            DebugStateStore s = _store ?? DebugStateStore.Instance;

            var sb = new StringBuilder(512);
            sb.AppendLine("<b>== Debug Panel ==</b>");
            sb.AppendLine($"Current NPC: {Describe(s.CurrentNpcName)} (id {s.CurrentNpcId})");
            sb.AppendLine($"Current Quest: {Describe(s.CurrentQuestName)} (id {s.CurrentQuestId})");
            sb.AppendLine($"Dialogue State: {Describe(s.DialogueState)}");
            sb.AppendLine($"Last STT: {Describe(s.LastSttText)}");
            sb.AppendLine($"Last LLM Raw: {Describe(s.LastLlmRaw)}");
            sb.AppendLine($"Last JSON Parse: {Describe(s.LastJsonParse)}");
            sb.AppendLine($"Last Quest Verdict: {Describe(s.LastQuestVerdict)}");
            sb.AppendLine($"TTS Queue Length: {s.TtsQueueLength}");
            sb.AppendLine($"Last Error: {Describe(s.LastError)}");
            sb.Append($"Updated (UTC): {s.LastUpdatedUtc:HH:mm:ss}");

            contentText.text = sb.ToString();
        }

        private static string Describe(string value)
        {
            return string.IsNullOrEmpty(value) ? "(none)" : value;
        }
    }
}
