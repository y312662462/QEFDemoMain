using System;
using System.Collections.Generic;

namespace MultiAgentNPC.Dialogue
{
    /// <summary>
    /// One committed conversation turn for a single NPC: what the player said and the
    /// NPC's reply. Committed to <see cref="ConversationHistoryManager"/> only after the
    /// reply finished playing (Sprint 6 requirement 8). A cancelled or failed turn is
    /// never committed.
    /// </summary>
    public class DialogueTurn
    {
        /// <summary>The player's text for this turn (debug-typed in Sprint 6).</summary>
        public string PlayerText { get; }

        /// <summary>The NPC reply text (all sentences joined).</summary>
        public string NpcText { get; }

        /// <summary>The structured sentences that were played.</summary>
        public IReadOnlyList<NPCSentence> Sentences { get; }

        /// <summary>UTC creation time, used for ordering/diagnostics.</summary>
        public DateTime TimestampUtc { get; }

        public DialogueTurn(string playerText, string npcText, IReadOnlyList<NPCSentence> sentences)
        {
            PlayerText = playerText ?? string.Empty;
            NpcText = npcText ?? string.Empty;
            Sentences = sentences ?? Array.Empty<NPCSentence>();
            TimestampUtc = DateTime.UtcNow;
        }
    }
}
