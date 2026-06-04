namespace MultiAgentNPC.Config
{
    /// <summary>
    /// One task-to-prompt binding for an NPC (see requirements doc 6.3 / 12.3).
    /// Each binding maps a quest to the three task-state prompt file names the
    /// NPC should use while that quest is Inactive / Active / Completed.
    ///
    /// NPCs hold a list of these so the design scales past the two columns the
    /// first-version CSV ships with.
    /// </summary>
    [System.Serializable]
    public class NPCPromptBinding
    {
        /// <summary>Bound quest id. A value of 0 means "no binding" and is filtered out at load time.</summary>
        public int TaskId;

        /// <summary>Prompt file name used while the quest is Inactive.</summary>
        public string InactivePrompt;

        /// <summary>Prompt file name used while the quest is Active.</summary>
        public string ActivePrompt;

        /// <summary>Prompt file name used while the quest is Completed.</summary>
        public string CompletedPrompt;

        public NPCPromptBinding()
        {
        }

        public NPCPromptBinding(int taskId, string inactivePrompt, string activePrompt, string completedPrompt)
        {
            TaskId = taskId;
            InactivePrompt = inactivePrompt;
            ActivePrompt = activePrompt;
            CompletedPrompt = completedPrompt;
        }

        public override string ToString()
        {
            return $"Task {TaskId} (Inactive='{InactivePrompt}', Active='{ActivePrompt}', Completed='{CompletedPrompt}')";
        }
    }
}
