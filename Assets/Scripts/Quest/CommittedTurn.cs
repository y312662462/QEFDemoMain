using System;

namespace MultiAgentNPC.Quest
{
    /// <summary>
    /// Immutable snapshot of a dialogue turn at the moment it was committed to History
    /// (Sprint 9). Raised by <c>DialoguePipeline.TurnCommitted</c> after the turn's TTS
    /// playback finished AND the commit gates passed, so it always represents a turn the
    /// player actually heard and that was written to History.
    ///
    /// Carries everything the host needs to launch a post-commit quest evaluation without
    /// holding a reference to the (possibly already disposed) <c>DialogueSession</c>.
    /// </summary>
    public sealed class CommittedTurn
    {
        /// <summary>Id of the dialogue session that produced (and committed) this turn.</summary>
        public int SessionId { get; }

        /// <summary>NPC the turn happened with.</summary>
        public int NpcId { get; }

        /// <summary>NPC display name.</summary>
        public string NpcName { get; }

        /// <summary>The player's utterance for this turn.</summary>
        public string PlayerText { get; }

        /// <summary>The NPC's spoken reply text for this turn.</summary>
        public string NpcResponseText { get; }

        /// <summary>Conversation history as it was BEFORE this turn was committed.</summary>
        public string PriorHistory { get; }

        /// <summary>UTC time the turn was committed.</summary>
        public DateTime TimestampUtc { get; }

        public CommittedTurn(
            int sessionId,
            int npcId,
            string npcName,
            string playerText,
            string npcResponseText,
            string priorHistory,
            DateTime timestampUtc)
        {
            SessionId = sessionId;
            NpcId = npcId;
            NpcName = npcName ?? string.Empty;
            PlayerText = playerText ?? string.Empty;
            NpcResponseText = npcResponseText ?? string.Empty;
            PriorHistory = priorHistory ?? string.Empty;
            TimestampUtc = timestampUtc;
        }

        public override string ToString()
        {
            return $"CommittedTurn(session #{SessionId}, npc {NpcId} '{NpcName}', player='{PlayerText}')";
        }
    }
}
