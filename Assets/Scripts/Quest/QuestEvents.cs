namespace MultiAgentNPC.Quest
{
    /// <summary>
    /// Payload broadcast by <see cref="QuestManager.QuestStateChanged"/> whenever a
    /// quest transitions between <see cref="QuestState"/> values. UI, NPC and audio
    /// modules can subscribe without coupling to the manager's internals.
    /// </summary>
    public class QuestStateChangedEventArgs
    {
        /// <summary>Quest whose state changed.</summary>
        public int QuestId { get; }

        /// <summary>Config of the quest whose state changed.</summary>
        public QuestConfig Quest { get; }

        /// <summary>State before the transition.</summary>
        public QuestState PreviousState { get; }

        /// <summary>State after the transition.</summary>
        public QuestState NewState { get; }

        public QuestStateChangedEventArgs(QuestConfig quest, QuestState previousState, QuestState newState)
        {
            Quest = quest;
            QuestId = quest != null ? quest.QuestId : 0;
            PreviousState = previousState;
            NewState = newState;
        }

        public override string ToString()
        {
            return $"QuestStateChanged[{QuestId}] {PreviousState} -> {NewState} ({Quest?.QuestName})";
        }
    }
}
