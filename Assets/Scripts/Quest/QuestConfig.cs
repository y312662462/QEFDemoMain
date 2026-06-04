using System.Collections.Generic;

namespace MultiAgentNPC.Quest
{
    /// <summary>
    /// Runtime representation of one row in QuestConfig.csv (requirements doc 12.4).
    /// Plain data object; quest runtime/activation logic is intentionally out of
    /// scope for this sprint.
    /// </summary>
    [System.Serializable]
    public class QuestConfig
    {
        /// <summary>Unique quest id.</summary>
        public int QuestId;

        /// <summary>Quest activated after this one completes; 0 means there is no next quest.</summary>
        public int NextQuestId;

        /// <summary>Quest name for UI.</summary>
        public string QuestName;

        /// <summary>Quest description for UI.</summary>
        public string QuestDescription;

        /// <summary>Evaluation type.</summary>
        public QuestType QuestType;

        /// <summary>Target NPC id; only meaningful for <see cref="QuestType.TargetDialogue"/>.</summary>
        public int TargetNpcId;

        /// <summary>
        /// Raw parameter, interpreted by quest type:
        /// TargetDialogue -> evaluation prompt file name;
        /// Composite -> child quest id list, e.g. "10003|10004|10005".
        /// </summary>
        public string QuestParam;

        /// <summary>Whether this quest shows in the quest UI.</summary>
        public bool ShowInUI;

        /// <summary>Parent quest id; 0 when this quest has no parent.</summary>
        public int ParentQuestId;

        /// <summary>UI sort order.</summary>
        public int SortOrder;

        /// <summary>
        /// For <see cref="QuestType.TargetDialogue"/>, the evaluation prompt file name
        /// stored in <see cref="QuestParam"/>. Returns null for other types.
        /// </summary>
        public string GetEvalPromptFileName()
        {
            return QuestType == QuestType.TargetDialogue ? QuestParam : null;
        }

        /// <summary>
        /// For <see cref="QuestType.Composite"/>, the parsed child quest ids from
        /// <see cref="QuestParam"/> (pipe-separated). Returns an empty list otherwise.
        /// </summary>
        public List<int> GetChildQuestIds()
        {
            var result = new List<int>();
            if (QuestType != QuestType.Composite || string.IsNullOrWhiteSpace(QuestParam))
            {
                return result;
            }

            string[] parts = QuestParam.Split('|');
            foreach (string part in parts)
            {
                string trimmed = part.Trim();
                if (trimmed.Length == 0)
                {
                    continue;
                }

                if (int.TryParse(trimmed, out int id))
                {
                    result.Add(id);
                }
            }

            return result;
        }

        public override string ToString()
        {
            return $"Quest[{QuestId}] {QuestName} ({QuestType}, next={NextQuestId}, target={TargetNpcId})";
        }
    }
}
