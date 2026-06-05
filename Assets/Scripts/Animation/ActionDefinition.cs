using System;

namespace MultiAgentNPC.Animation
{
    /// <summary>
    /// How an <see cref="ActionDefinition"/> drives the Animator.
    /// </summary>
    public enum ActionDriveMode
    {
        /// <summary>Cross-fade/play a named Animator state (no parameter required).</summary>
        StateName = 0,

        /// <summary>Fire an Animator trigger parameter.</summary>
        Trigger = 1
    }

    /// <summary>
    /// One row of the action table (Sprint 10): maps an <c>actionId</c> coming from the LLM
    /// JSON to a concrete Animator action. Serializable so it can be authored in the
    /// Inspector via <see cref="ActionMappingTable"/> or built into the code default table.
    ///
    /// Deliberately decoupled from animation assets: the mapping only references Animator
    /// state names / trigger parameters, never FBX clips directly (requirements doc 9.1.1).
    /// </summary>
    [Serializable]
    public class ActionDefinition
    {
        /// <summary>Action id from the LLM JSON (<c>sentences[].actionId</c>).</summary>
        public int actionId;

        /// <summary>Whether this action plays a state by name or fires a trigger.</summary>
        public ActionDriveMode mode = ActionDriveMode.StateName;

        /// <summary>
        /// Animator state name used when <see cref="mode"/> is
        /// <see cref="ActionDriveMode.StateName"/>. For a state inside a sub-state machine
        /// use the full path (e.g. <c>Emoji.Emoji_Hi</c>).
        /// </summary>
        public string stateName;

        /// <summary>Trigger parameter name used when <see cref="mode"/> is Trigger.</summary>
        public string triggerName;

        /// <summary>Animator layer index the state/trigger targets.</summary>
        public int layer;

        /// <summary>Cross-fade duration (seconds) for StateName mode.</summary>
        public float crossFadeSeconds = 0.1f;

        public ActionDefinition()
        {
        }

        /// <summary>Convenience constructor for a StateName-driven action.</summary>
        public ActionDefinition(int actionId, string stateName, float crossFadeSeconds = 0.1f, int layer = 0)
        {
            this.actionId = actionId;
            mode = ActionDriveMode.StateName;
            this.stateName = stateName;
            this.crossFadeSeconds = crossFadeSeconds;
            this.layer = layer;
        }

        public override string ToString()
        {
            return mode == ActionDriveMode.Trigger
                ? $"Action[{actionId}] Trigger '{triggerName}' (layer {layer})"
                : $"Action[{actionId}] State '{stateName}' (layer {layer}, fade {crossFadeSeconds}s)";
        }
    }
}
