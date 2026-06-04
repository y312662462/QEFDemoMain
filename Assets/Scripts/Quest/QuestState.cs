namespace MultiAgentNPC.Quest
{
    /// <summary>
    /// Lifecycle state of a quest (requirements doc 10.2).
    /// "Inactive" replaces the older "待接取" wording: the quest simply has not
    /// been activated by the main flow yet; the player does not manually accept it.
    /// </summary>
    public enum QuestState
    {
        /// <summary>Not activated by the main flow yet.</summary>
        Inactive,

        /// <summary>Currently in progress.</summary>
        Active,

        /// <summary>Objective achieved.</summary>
        Completed
    }
}
