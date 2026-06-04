namespace MultiAgentNPC.Quest
{
    /// <summary>
    /// Mutable runtime wrapper around a <see cref="QuestConfig"/> row. The
    /// <see cref="QuestManager"/> owns one of these per known quest and drives its
    /// <see cref="State"/>. The config itself is immutable data loaded from CSV.
    /// </summary>
    public class QuestRuntimeState
    {
        /// <summary>The immutable config this runtime state tracks.</summary>
        public QuestConfig Config { get; }

        /// <summary>Current lifecycle state. Starts <see cref="QuestState.Inactive"/>.</summary>
        public QuestState State { get; internal set; }

        /// <summary>Reason text from the last LLM evaluation, when any.</summary>
        public string LastReason { get; internal set; }

        /// <summary>Confidence from the last LLM evaluation, when any.</summary>
        public float LastConfidence { get; internal set; }

        public QuestRuntimeState(QuestConfig config)
        {
            Config = config;
            State = QuestState.Inactive;
        }

        public int QuestId => Config != null ? Config.QuestId : 0;

        public override string ToString()
        {
            return $"QuestRuntime[{QuestId}] {Config?.QuestName} = {State}";
        }
    }
}
