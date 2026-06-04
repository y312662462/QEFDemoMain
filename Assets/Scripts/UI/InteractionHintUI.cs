using TMPro;
using UnityEngine;
using MultiAgentNPC.NPC;

namespace MultiAgentNPC.UI
{
    /// <summary>
    /// Screen-space proximity hint (Sprint 5). Shows the active NPC's name and the
    /// "按住空格说话" hint while an NPC is active, hides otherwise.
    ///
    /// Driven entirely by <see cref="ActiveNPCService"/> events (reached via
    /// <see cref="NPCManager.Instance"/>); it never reads NPC internals or talks to a
    /// pipeline.
    ///
    /// Robustness:
    /// - Binding is retried every frame until an NPCManager/ActiveService exists, so the
    ///   script works regardless of Awake/OnEnable ordering or a late-added manager.
    /// - Visibility toggles a separate <c>root</c> when assigned, otherwise the label
    ///   component's <c>enabled</c> flag. It never disables its own GameObject, so it can
    ///   always recover and keep receiving events. Fails safe with no NullReference.
    /// </summary>
    [AddComponentMenu("MultiAgentNPC/UI/Interaction Hint UI")]
    public class InteractionHintUI : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Label that renders '<NPC name>\\n<hint>'. Optional when logOnly is true.")]
        [SerializeField] private TMP_Text hintLabel;

        [Tooltip("Optional separate root toggled while active (e.g. a panel with a background). " +
                 "Leave empty to just show/hide the label. Do NOT point this at this same GameObject.")]
        [SerializeField] private GameObject root;

        [Tooltip("If no label is assigned, only log activation instead of warning repeatedly.")]
        [SerializeField] private bool logOnly;

        private ActiveNPCService _service;
        private bool _bound;
        private bool _missingWarned;

        private void Awake()
        {
            // Guard against the self-disable footgun: a root equal to this object would
            // hide the script itself when we hide the hint.
            if (root == gameObject)
            {
                root = null;
            }

            SetVisible(false);
        }

        private void OnEnable()
        {
            TryBind();
        }

        private void OnDisable()
        {
            if (_service != null)
            {
                _service.ActiveNPCChanged -= OnActiveNPCChanged;
                _service.ActiveNPCCleared -= OnActiveNPCCleared;
            }

            _service = null;
            _bound = false;
        }

        private void Update()
        {
            if (_bound)
            {
                return;
            }

            if (TryBind())
            {
                return;
            }

            // Warn once (not every frame) if the manager is genuinely missing.
            if (!_missingWarned && Time.timeSinceLevelLoad > 2f)
            {
                Debug.LogWarning(
                    "[InteractionHintUI] No NPCManager/ActiveService found in the scene; hint stays hidden. " +
                    "Add an NPCManager to enable proximity hints.");
                _missingWarned = true;
            }
        }

        private bool TryBind()
        {
            if (_bound)
            {
                return true;
            }

            NPCManager manager = NPCManager.Instance;
            if (manager == null)
            {
                return false;
            }

            ActiveNPCService service = manager.ActiveService;
            if (service == null)
            {
                return false;
            }

            _service = service;
            _service.ActiveNPCChanged += OnActiveNPCChanged;
            _service.ActiveNPCCleared += OnActiveNPCCleared;
            _bound = true;

            Debug.Log("[InteractionHintUI] Bound to ActiveNPCService.");

            // Reflect an NPC that is already active at bind time.
            if (_service.HasActive)
            {
                NPCController current = _service.Current;
                ShowHint(current.NpcName, current.HintText);
            }
            else
            {
                SetVisible(false);
            }

            return true;
        }

        private void OnActiveNPCChanged(ActiveNPCChangedEventArgs args)
        {
            if (args == null)
            {
                SetVisible(false);
                return;
            }

            ShowHint(args.NpcName, args.HintText);
        }

        private void OnActiveNPCCleared(NPCController previous)
        {
            SetVisible(false);
        }

        private void ShowHint(string npcName, string hintText)
        {
            string text = $"{npcName}\n{hintText}";
            if (hintLabel != null)
            {
                hintLabel.text = text;
            }
            else if (logOnly)
            {
                Debug.Log($"[InteractionHintUI] {text.Replace('\n', ' ')}");
            }
            else
            {
                Debug.LogWarning("[InteractionHintUI] No hintLabel assigned; cannot show the proximity hint.");
            }

            SetVisible(true);
        }

        private void SetVisible(bool visible)
        {
            if (root != null)
            {
                root.SetActive(visible);
                return;
            }

            // No separate root: toggle the label component only, never our own object.
            if (hintLabel != null)
            {
                hintLabel.enabled = visible;
            }
        }
    }
}
