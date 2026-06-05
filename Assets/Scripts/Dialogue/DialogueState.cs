namespace MultiAgentNPC.Dialogue
{
    /// <summary>
    /// High-level state of the dialogue pipeline (Sprint 6). Drives input gating and
    /// exception recovery: a new turn may only start while in <see cref="InRange"/>.
    /// Any failure or cancellation must return to <see cref="InRange"/> (NPC still
    /// active) or <see cref="Idle"/> (no active NPC) - the pipeline must never stay
    /// stuck in <see cref="Thinking"/> or <see cref="Speaking"/>.
    /// </summary>
    public enum DialogueState
    {
        /// <summary>No active NPC; debug input is ignored.</summary>
        Idle,

        /// <summary>An NPC is active and the pipeline is ready to accept player text.</summary>
        InRange,

        /// <summary>Player text submitted; calling the LLM and parsing its response.</summary>
        Thinking,

        /// <summary>Synthesizing and playing the NPC's sentences (TTS + subtitles).</summary>
        Speaking
    }
}
