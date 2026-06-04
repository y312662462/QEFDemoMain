using System.Collections.Generic;

namespace MultiAgentNPC.Config
{
    /// <summary>
    /// Runtime representation of one row in NPCConfig.csv (requirements doc 12.3).
    /// Plain data object with no Unity dependency so it can later be produced by a
    /// ScriptableObject or remote config source behind the same shape.
    /// </summary>
    [System.Serializable]
    public class NPCConfig
    {
        /// <summary>Unique NPC id, matched against the NPCID on the scene NPC script.</summary>
        public int NpcId;

        /// <summary>Display name shown in UI / overhead label.</summary>
        public string NpcName;

        /// <summary>Azure TTS voice id for this NPC.</summary>
        public string TtsVoiceId;

        /// <summary>Default prompt file name (no path), used when no quest binding applies.</summary>
        public string DefaultPrompt;

        /// <summary>Proximity hint text shown when the player is in range.</summary>
        public string ProximityPromptText;

        /// <summary>Proximity hint audio resource name (not an LLM/TTS output).</summary>
        public string ProximityPromptAudio;

        /// <summary>Interaction radius in meters. Optional; defaults to 0 when unset.</summary>
        public float InteractionRadius;

        /// <summary>Quest-to-prompt bindings, in CSV order. Empty when the NPC has no quest binding.</summary>
        public List<NPCPromptBinding> TaskBindings = new List<NPCPromptBinding>();

        public override string ToString()
        {
            return $"NPC[{NpcId}] {NpcName} (voice='{TtsVoiceId}', default='{DefaultPrompt}', bindings={TaskBindings.Count})";
        }
    }
}
