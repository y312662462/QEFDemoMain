using System.Collections.Generic;
using UnityEngine;

namespace MultiAgentNPC.Animation
{
    /// <summary>
    /// Resolves an LLM <c>actionId</c> to a concrete <see cref="ActionDefinition"/>
    /// (Sprint 10). Plain C# (no scene dependency) so it is unit-testable and reusable.
    ///
    /// Source of truth, in order: an Inspector-authored <see cref="ActionMappingTable"/>
    /// when supplied, otherwise a built-in default table derived from the requirements doc
    /// (section 9.2). Unknown ids resolve to the Idle action and log a single Warning -
    /// never an error (requirements doc 9.1.3).
    /// </summary>
    public class ActionIdMapper
    {
        /// <summary>Default Idle action id (requirements doc: 1001 default stand).</summary>
        public const int DefaultIdleActionId = 1001;

        private readonly Dictionary<int, ActionDefinition> _map = new Dictionary<int, ActionDefinition>();
        private readonly HashSet<int> _warnedUnknown = new HashSet<int>();

        /// <summary>The action id used for Idle / unknown-id fallback.</summary>
        public int IdleActionId { get; }

        /// <summary>True when the built-in default table was used (no asset assigned).</summary>
        public bool UsingDefaultTable { get; }

        /// <summary>
        /// Builds a mapper. When <paramref name="table"/> is null or empty the built-in
        /// default table is used and a Warning is logged so the missing-config case is
        /// obvious (requirements doc: "default table or clear Warning").
        /// </summary>
        public ActionIdMapper(ActionMappingTable table)
        {
            IReadOnlyList<ActionDefinition> rows = table != null ? table.Actions : null;

            if (rows != null && rows.Count > 0)
            {
                IdleActionId = table.IdleActionId;
                UsingDefaultTable = false;
                foreach (ActionDefinition def in rows)
                {
                    if (def != null)
                    {
                        _map[def.actionId] = def;
                    }
                }
            }
            else
            {
                IdleActionId = DefaultIdleActionId;
                UsingDefaultTable = true;
                foreach (ActionDefinition def in BuildDefaultTable())
                {
                    _map[def.actionId] = def;
                }

                Debug.LogWarning(
                    "[ActionIdMapper] No ActionMappingTable assigned (or it was empty); " +
                    "using the built-in default action table. Assign a table via " +
                    "Create > MultiAgentNPC > Action Mapping Table to customise mappings.");
            }
        }

        /// <summary>
        /// Resolves <paramref name="actionId"/> to a definition. Unknown ids fall back to
        /// the Idle action and log one Warning per id (de-duplicated). Never returns null.
        /// </summary>
        public ActionDefinition Resolve(int actionId)
        {
            if (_map.TryGetValue(actionId, out ActionDefinition def) && def != null)
            {
                return def;
            }

            if (_warnedUnknown.Add(actionId))
            {
                Debug.LogWarning(
                    $"[ActionIdMapper] Unknown actionId {actionId}; falling back to Idle ({IdleActionId}).");
            }

            return ResolveIdle();
        }

        /// <summary>Resolves the Idle action, synthesising a safe default when unmapped.</summary>
        public ActionDefinition ResolveIdle()
        {
            if (_map.TryGetValue(IdleActionId, out ActionDefinition idle) && idle != null)
            {
                return idle;
            }

            return new ActionDefinition(IdleActionId, "Idle");
        }

        /// <summary>
        /// Built-in default mapping (requirements doc 9.2). Uses StateName mode because the
        /// shipped <c>AnimationController_Demo</c> exposes these as states (not triggers).
        /// State names match the Layer Lab Animator states (e.g. <c>Emoji_Hi</c>).
        /// </summary>
        public static IEnumerable<ActionDefinition> BuildDefaultTable()
        {
            return new List<ActionDefinition>
            {
                new ActionDefinition(1001, "Idle"),
                new ActionDefinition(1201, "Emoji_Hi"),
                new ActionDefinition(1202, "Emoji_Nice"),
                new ActionDefinition(1203, "Emoji_Smile1"),
                new ActionDefinition(1204, "Emoji_Smile2"),
                new ActionDefinition(1205, "Emoji_Cheer"),
                new ActionDefinition(1206, "Emoji_Applaud"),
                new ActionDefinition(1213, "Emoji_Putter_Around"),
                new ActionDefinition(1214, "Emoji_Showmanship"),
                new ActionDefinition(1215, "Emoji_SideToSide"),
                new ActionDefinition(1216, "Emoji_Sigh"),
                new ActionDefinition(1301, "Interaction_Item_Put"),
                new ActionDefinition(1302, "Interaction_Pickup"),
                new ActionDefinition(1501, "Dance_1"),
            };
        }
    }
}
