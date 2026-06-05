using System.Collections.Generic;
using System.Text;

namespace MultiAgentNPC.Dialogue
{
    /// <summary>
    /// Per-NPC conversation history. Each NPC keeps its own independent list of
    /// committed <see cref="DialogueTurn"/>s; a turn is committed only after its reply
    /// finished playing. When rendering the next prompt, only the most recent
    /// <see cref="DefaultMaxTurns"/> turns are sent to the LLM (Sprint 6 requirement).
    ///
    /// Plain C# (no Unity dependency) so it can be unit-tested and owned by either a
    /// MonoBehaviour host or the pipeline.
    /// </summary>
    public class ConversationHistoryManager
    {
        /// <summary>Number of most-recent committed turns sent to the LLM per request.</summary>
        public const int DefaultMaxTurns = 6;

        private const string EmptyHistoryText = "(no previous turns)";

        private readonly Dictionary<int, List<DialogueTurn>> _historyByNpc =
            new Dictionary<int, List<DialogueTurn>>();

        /// <summary>Appends a committed turn to the given NPC's history.</summary>
        public void Commit(int npcId, DialogueTurn turn)
        {
            if (turn == null)
            {
                return;
            }

            if (!_historyByNpc.TryGetValue(npcId, out List<DialogueTurn> turns))
            {
                turns = new List<DialogueTurn>();
                _historyByNpc[npcId] = turns;
            }

            turns.Add(turn);
        }

        /// <summary>Total committed turns recorded for an NPC.</summary>
        public int GetTurnCount(int npcId)
        {
            return _historyByNpc.TryGetValue(npcId, out List<DialogueTurn> turns) ? turns.Count : 0;
        }

        /// <summary>
        /// Formats the most recent <paramref name="maxTurns"/> turns for an NPC as a
        /// prompt-ready transcript. Returns <c>"(no previous turns)"</c> when empty so
        /// templates render cleanly.
        /// </summary>
        public string GetRecentFormatted(int npcId, int maxTurns = DefaultMaxTurns)
        {
            if (!_historyByNpc.TryGetValue(npcId, out List<DialogueTurn> turns) || turns.Count == 0)
            {
                return EmptyHistoryText;
            }

            if (maxTurns < 1)
            {
                maxTurns = 1;
            }

            int startIndex = turns.Count > maxTurns ? turns.Count - maxTurns : 0;

            var sb = new StringBuilder();
            for (int i = startIndex; i < turns.Count; i++)
            {
                DialogueTurn turn = turns[i];
                if (turn == null)
                {
                    continue;
                }

                sb.Append("Player: ").AppendLine(turn.PlayerText);
                sb.Append("NPC: ").AppendLine(turn.NpcText);
            }

            return sb.ToString().TrimEnd();
        }

        /// <summary>Clears history for a single NPC.</summary>
        public void Clear(int npcId)
        {
            _historyByNpc.Remove(npcId);
        }

        /// <summary>Clears history for every NPC.</summary>
        public void ClearAll()
        {
            _historyByNpc.Clear();
        }
    }
}
