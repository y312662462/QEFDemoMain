namespace MultiAgentNPC.Dialogue
{
    /// <summary>
    /// Mutable scratch state for a turn that is still in flight. The pipeline fills this
    /// as it progresses (raw LLM text, parsed response, error) so the value is available
    /// for diagnostics (<c>DebugStateStore</c>) and, on success, for building the final
    /// committed <see cref="DialogueTurn"/>.
    /// </summary>
    public class PendingDialogueTurn
    {
        /// <summary>NPC this turn belongs to.</summary>
        public int NpcId;

        /// <summary>NPC display name (for diagnostics).</summary>
        public string NpcName;

        /// <summary>The player's submitted text.</summary>
        public string PlayerText;

        /// <summary>The rendered prompt sent to the LLM (for diagnostics).</summary>
        public string RenderedPrompt;

        /// <summary>The raw, unparsed LLM response text.</summary>
        public string RawLlm;

        /// <summary>The parsed response (may be a fallback), or null before parsing.</summary>
        public NPCResponse Parsed;

        /// <summary>The last error encountered, or null/empty when none.</summary>
        public string Error;

        public PendingDialogueTurn(int npcId, string npcName, string playerText)
        {
            NpcId = npcId;
            NpcName = npcName ?? string.Empty;
            PlayerText = playerText ?? string.Empty;
        }

        /// <summary>True when a parsed response exists with playable content.</summary>
        public bool HasPlayableResponse => Parsed != null && Parsed.HasContent;
    }
}
