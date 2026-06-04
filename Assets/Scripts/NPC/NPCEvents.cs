namespace MultiAgentNPC.NPC
{
    /// <summary>
    /// Payload broadcast by <see cref="ActiveNPCService.ActiveNPCChanged"/> whenever a
    /// new NPC becomes the single global ActiveNPC. UI, facing and audio modules can
    /// subscribe without coupling to the NPC or manager internals.
    /// </summary>
    public class ActiveNPCChangedEventArgs
    {
        /// <summary>Default proximity hint shown when the NPC config has no override text.</summary>
        public const string DefaultHintText = "按住空格说话";

        /// <summary>The NPC that just became active.</summary>
        public NPCController Npc { get; }

        /// <summary>Active NPC id (0 when <see cref="Npc"/> is null).</summary>
        public int NpcId { get; }

        /// <summary>Display name resolved from NPCConfig.</summary>
        public string NpcName { get; }

        /// <summary>Proximity hint text from NPCConfig (may be empty).</summary>
        public string ProximityPromptText { get; }

        /// <summary>
        /// Hint to surface to the player: the config proximity text when present,
        /// otherwise <see cref="DefaultHintText"/>.
        /// </summary>
        public string HintText { get; }

        public ActiveNPCChangedEventArgs(NPCController npc, string npcName, string proximityPromptText)
        {
            Npc = npc;
            NpcId = npc != null ? npc.NpcId : 0;
            NpcName = npcName;
            ProximityPromptText = proximityPromptText;
            HintText = string.IsNullOrWhiteSpace(proximityPromptText) ? DefaultHintText : proximityPromptText;
        }

        public override string ToString()
        {
            return $"ActiveNPCChanged[{NpcId}] {NpcName} (hint='{HintText}')";
        }
    }
}
