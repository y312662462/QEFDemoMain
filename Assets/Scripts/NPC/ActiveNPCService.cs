using System;
using UnityEngine;

namespace MultiAgentNPC.NPC
{
    /// <summary>
    /// Owns the single global ActiveNPC invariant. Plain C# class (no Unity
    /// dependency beyond logging) so it can be unit-driven outside MonoBehaviours.
    ///
    /// Exactly one NPC may be active at a time. <see cref="SetActive"/> and
    /// <see cref="Clear"/> raise events that UI, facing and (later) dialogue rollback
    /// can subscribe to without coupling to the manager internals.
    /// </summary>
    public class ActiveNPCService
    {
        /// <summary>Fired when a new NPC becomes active (after any previous one is cleared).</summary>
        public event Action<ActiveNPCChangedEventArgs> ActiveNPCChanged;

        /// <summary>
        /// Fired when the current ActiveNPC is cleared. The argument is the NPC that
        /// was active. Reserved as the hook for later Dialogue rollback.
        /// </summary>
        public event Action<NPCController> ActiveNPCCleared;

        /// <summary>The current ActiveNPC, or null when none is active.</summary>
        public NPCController Current { get; private set; }

        /// <summary>True while an NPC is active.</summary>
        public bool HasActive => Current != null;

        /// <summary>
        /// Makes <paramref name="npc"/> the ActiveNPC. If a different NPC is already
        /// active it is cleared first so the single-active invariant always holds.
        /// Re-activating the already-active NPC is a no-op.
        /// </summary>
        public void SetActive(NPCController npc)
        {
            if (npc == null)
            {
                Debug.LogWarning("[ActiveNPCService] SetActive called with null NPC; ignoring.");
                return;
            }

            if (Current == npc)
            {
                return;
            }

            if (Current != null)
            {
                Clear();
            }

            Current = npc;

            var args = new ActiveNPCChangedEventArgs(npc, npc.NpcName, npc.ProximityPromptText);
            try
            {
                ActiveNPCChanged?.Invoke(args);
            }
            catch (Exception e)
            {
                Debug.LogError($"[ActiveNPCService] An ActiveNPCChanged subscriber threw: {e}");
            }
        }

        /// <summary>Clears the current ActiveNPC (if any) and notifies subscribers.</summary>
        public void Clear()
        {
            if (Current == null)
            {
                return;
            }

            NPCController previous = Current;
            Current = null;

            try
            {
                ActiveNPCCleared?.Invoke(previous);
            }
            catch (Exception e)
            {
                Debug.LogError($"[ActiveNPCService] An ActiveNPCCleared subscriber threw: {e}");
            }
        }
    }
}
