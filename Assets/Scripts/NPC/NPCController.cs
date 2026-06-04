using System;
using UnityEngine;
using MultiAgentNPC.Config;

namespace MultiAgentNPC.NPC
{
    /// <summary>
    /// Scene-side identity and activation state for one NPC. Resolves its
    /// <see cref="NPCConfig"/> from <see cref="NPCManager"/> on <c>Start</c>, registers
    /// itself, and forwards player range enter/exit (raised by an
    /// <see cref="NPCInteractionTrigger"/>) to the manager.
    ///
    /// Sibling components (facing, nameplate) react via <see cref="ActivationChanged"/>
    /// and read the exposed read-only properties; they never talk to the manager directly.
    /// </summary>
    [AddComponentMenu("MultiAgentNPC/NPC Controller")]
    public class NPCController : MonoBehaviour
    {
        [Header("Identity")]
        [Tooltip("NPCID matched against a row in NPCConfig.csv. Must exist.")]
        [SerializeField] private int npcId;

        [Header("Interaction")]
        [Tooltip("Override the interaction radius from config. 0 = use NPCConfig.InteractionRadius.")]
        [SerializeField] private float interactionRadiusOverride;

        [Tooltip("Log activation transitions for this NPC.")]
        [SerializeField] private bool logActivation;

        private NPCConfig _config;
        private NPCManager _manager;

        /// <summary>Raised when this NPC becomes active (true) or inactive (false).</summary>
        public event Action<bool> ActivationChanged;

        /// <summary>The NPCID configured in the Inspector.</summary>
        public int NpcId => npcId;

        /// <summary>True while this NPC is the global ActiveNPC.</summary>
        public bool IsActive { get; private set; }

        /// <summary>Display name resolved from config (falls back to the GameObject name).</summary>
        public string NpcName =>
            _config != null && !string.IsNullOrWhiteSpace(_config.NpcName) ? _config.NpcName : name;

        /// <summary>Proximity hint text from config (may be empty).</summary>
        public string ProximityPromptText => _config != null ? _config.ProximityPromptText : string.Empty;

        /// <summary>
        /// Hint to show the player: config proximity text when present, otherwise the
        /// default "按住空格说话".
        /// </summary>
        public string HintText =>
            string.IsNullOrWhiteSpace(ProximityPromptText)
                ? ActiveNPCChangedEventArgs.DefaultHintText
                : ProximityPromptText;

        /// <summary>
        /// Effective interaction radius: the Inspector override when &gt; 0, otherwise the
        /// config value.
        /// </summary>
        public float InteractionRadius =>
            interactionRadiusOverride > 0f
                ? interactionRadiusOverride
                : (_config != null ? _config.InteractionRadius : 0f);

        /// <summary>The player transform currently inside this NPC's range, or null.</summary>
        public Transform CurrentPlayerTransform { get; private set; }

        private void Start()
        {
            _manager = NPCManager.Instance;
            if (_manager == null)
            {
                Debug.LogError(
                    $"[NPCController] NPC '{name}' (id {npcId}) found no NPCManager in the scene. " +
                    "Add an NPCManager component to one GameObject.");
                return;
            }

            if (!_manager.TryGetNpcConfig(npcId, out _config))
            {
                Debug.LogError(
                    $"[NPCController] NPCID {npcId} on '{name}' was not found in NPCConfig.csv. " +
                    "Check the NPCID field and the config table.");
            }

            _manager.RegisterNpc(this);
            _manager.ActiveNPCChanged += OnActiveChanged;
            _manager.ActiveNPCCleared += OnActiveCleared;
            SetActive(_manager.ActiveNpc == this);
        }

        private void OnDestroy()
        {
            if (_manager != null)
            {
                _manager.ActiveNPCChanged -= OnActiveChanged;
                _manager.ActiveNPCCleared -= OnActiveCleared;
                _manager.UnregisterNpc(this);
            }
        }

        /// <summary>Called by <see cref="NPCInteractionTrigger"/> when the player enters range.</summary>
        public void OnPlayerEnteredRange(Transform player)
        {
            CurrentPlayerTransform = player;

            if (_manager == null)
            {
                _manager = NPCManager.Instance;
            }

            if (_manager == null)
            {
                Debug.LogError($"[NPCController] NPC '{name}' cannot report range entry: no NPCManager.");
                return;
            }

            _manager.NotifyPlayerEntered(this);
        }

        /// <summary>Called by <see cref="NPCInteractionTrigger"/> when the player exits range.</summary>
        public void OnPlayerExitedRange(Transform player)
        {
            if (CurrentPlayerTransform == player)
            {
                CurrentPlayerTransform = null;
            }

            if (_manager != null)
            {
                _manager.NotifyPlayerExited(this);
            }
        }

        private void OnActiveChanged(ActiveNPCChangedEventArgs args)
        {
            SetActive(args.Npc == this);
        }

        private void OnActiveCleared(NPCController previous)
        {
            if (previous == this)
            {
                SetActive(false);
            }
        }

        private void SetActive(bool active)
        {
            if (IsActive == active)
            {
                return;
            }

            IsActive = active;

            if (logActivation)
            {
                Debug.Log($"[NPCController] NPC {npcId} ('{NpcName}') active={active}.");
            }

            try
            {
                ActivationChanged?.Invoke(active);
            }
            catch (Exception e)
            {
                Debug.LogError($"[NPCController] An ActivationChanged subscriber threw: {e}");
            }
        }
    }
}
