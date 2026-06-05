using System.Collections.Generic;
using UnityEngine;

namespace MultiAgentNPC.Animation
{
    /// <summary>
    /// Inspector-authored, shareable action table (Sprint 10). Holds the
    /// <c>actionId -&gt; Animator action</c> rows so multiple NPCs can reuse the same
    /// mapping asset and designers can edit it without touching code.
    ///
    /// Create via <c>Assets &gt; Create &gt; MultiAgentNPC &gt; Action Mapping Table</c>.
    /// When an NPC has no table assigned, <see cref="ActionIdMapper"/> falls back to a
    /// built-in default table (with a clear Warning) so the system still works.
    /// </summary>
    [CreateAssetMenu(fileName = "ActionMappingTable", menuName = "MultiAgentNPC/Action Mapping Table")]
    public class ActionMappingTable : ScriptableObject
    {
        [Tooltip("Action id from the LLM JSON to the configured Idle action. Used as the fallback for unknown ids and for ReturnToIdle.")]
        [SerializeField] private int idleActionId = 1001;

        [Tooltip("Rows mapping each actionId to an Animator state or trigger.")]
        [SerializeField] private List<ActionDefinition> actions = new List<ActionDefinition>();

        /// <summary>Action id treated as the default Idle (fallback target).</summary>
        public int IdleActionId => idleActionId;

        /// <summary>The authored mapping rows (may be empty).</summary>
        public IReadOnlyList<ActionDefinition> Actions => actions;
    }
}
