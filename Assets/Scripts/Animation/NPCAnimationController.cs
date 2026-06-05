using UnityEngine;

namespace MultiAgentNPC.Animation
{
    /// <summary>
    /// Drives one NPC's <see cref="Animator"/> from action ids (Sprint 10). Lives on the
    /// NPC prefab as a sibling of <c>NPCController</c>; the dialogue host resolves it for
    /// the speaking NPC and calls <see cref="PlayAction"/> as each sentence starts and
    /// <see cref="ReturnToIdle"/> when the turn ends.
    ///
    /// The action table is decoupled from this class via <see cref="ActionIdMapper"/>:
    /// this component only knows "play the action for id X" and never hard-codes the
    /// actionId -&gt; clip mapping (requirements doc 9.1.1).
    /// </summary>
    [AddComponentMenu("MultiAgentNPC/NPC Animation Controller")]
    public class NPCAnimationController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Animator to drive. Auto-resolved from this object / children when unset.")]
        [SerializeField] private Animator animator;

        [Header("Mapping")]
        [Tooltip("Inspector-authored action table. Empty = use the built-in default table (with a Warning).")]
        [SerializeField] private ActionMappingTable mappingTable;

        [Header("Debug")]
        [Tooltip("Log each resolved action to the Console.")]
        [SerializeField] private bool logActions;

        private ActionIdMapper _mapper;

        /// <summary>The id used for Idle / unknown-id fallback (from the table or default).</summary>
        public int IdleActionId => Mapper.IdleActionId;

        private ActionIdMapper Mapper => _mapper ??= new ActionIdMapper(mappingTable);

        private void Awake()
        {
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            if (animator == null)
            {
                Debug.LogError(
                    $"[NPCAnimationController] '{name}' found no Animator; actions will be ignored.");
            }

            // Build the mapper early so a missing-table Warning surfaces at startup.
            _ = Mapper;
        }

        /// <summary>
        /// Plays the action mapped to <paramref name="actionId"/>. Unknown ids fall back to
        /// Idle (handled by the mapper) and never throw. No-op when there is no Animator.
        /// </summary>
        public void PlayAction(int actionId)
        {
            if (animator == null)
            {
                return;
            }

            ActionDefinition def = Mapper.Resolve(actionId);
            Apply(def);
        }

        /// <summary>Returns the NPC to its Idle action. Safe to call at any time.</summary>
        public void ReturnToIdle()
        {
            if (animator == null)
            {
                return;
            }

            Apply(Mapper.ResolveIdle());
        }

        private void Apply(ActionDefinition def)
        {
            if (def == null)
            {
                return;
            }

            if (logActions)
            {
                Debug.Log($"[NPCAnimationController] '{name}' -> {def}");
            }

            if (def.mode == ActionDriveMode.Trigger)
            {
                if (!string.IsNullOrEmpty(def.triggerName))
                {
                    animator.SetTrigger(def.triggerName);
                }
                return;
            }

            if (!string.IsNullOrEmpty(def.stateName))
            {
                animator.CrossFade(def.stateName, Mathf.Max(0f, def.crossFadeSeconds), def.layer);
            }
        }
    }
}
