using UnityEngine;

namespace MultiAgentNPC.Quest
{
    /// <summary>
    /// Supported quest evaluation types (requirements doc 10.4).
    /// First version ships two; the enum is intended to grow (PickupItem,
    /// EnterArea, ClickObject, UseItem, WatchAnimation, CustomRule, ...).
    /// </summary>
    public enum QuestType
    {
        /// <summary>Player must talk to a specific NPC; completion judged by an LLM evaluator.</summary>
        TargetDialogue,

        /// <summary>Parent quest completed when all of its child quests are completed (rule-based).</summary>
        Composite
    }

    public static class QuestTypeExtensions
    {
        /// <summary>
        /// Parses a CSV cell into a <see cref="QuestType"/>. Case-insensitive.
        /// Unknown values log a warning and fall back to <see cref="QuestType.TargetDialogue"/>.
        /// </summary>
        public static QuestType ParseQuestType(string raw, string context = null)
        {
            if (!string.IsNullOrWhiteSpace(raw) &&
                System.Enum.TryParse(raw.Trim(), true, out QuestType parsed))
            {
                return parsed;
            }

            string where = string.IsNullOrEmpty(context) ? string.Empty : $" ({context})";
            Debug.LogWarning(
                $"[QuestType] Unknown QuestType '{raw}'{where}. Falling back to TargetDialogue.");
            return QuestType.TargetDialogue;
        }
    }
}
