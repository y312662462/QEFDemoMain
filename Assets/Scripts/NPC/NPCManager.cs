using System;
using System.Collections.Generic;
using UnityEngine;
using MultiAgentNPC.Config;

namespace MultiAgentNPC.NPC
{
    /// <summary>
    /// Scene singleton that owns the shared <see cref="ConfigManager"/> and enforces
    /// the single global ActiveNPC rule (delegated to <see cref="ActiveNPCService"/>).
    ///
    /// NPCs register themselves on <c>Start</c> and report range enter/exit. The
    /// manager keeps an ordered, duplicate-free candidate list of NPCs the player is
    /// currently inside. Conflict rule: first-come keeps focus. When a new NPC enters
    /// while one is active the new NPC is only queued; when the active NPC's range is
    /// left, the next remaining candidate is promoted.
    /// </summary>
    [AddComponentMenu("MultiAgentNPC/NPC Manager")]
    public class NPCManager : MonoBehaviour
    {
        /// <summary>Single scene instance. Set in <c>Awake</c>.</summary>
        public static NPCManager Instance { get; private set; }

        [Header("Config")]
        [Tooltip("Optional override for the config folder. Empty = StreamingAssets/Config.")]
        [SerializeField] private string configFolderOverride = string.Empty;

        [Header("Debug")]
        [Tooltip("Log NPC registration and ActiveNPC changes to the Console.")]
        [SerializeField] private bool logEvents = true;

        private ConfigManager _configManager;
        private readonly ActiveNPCService _activeService = new ActiveNPCService();

        private readonly List<NPCController> _registered = new List<NPCController>();
        private readonly List<NPCController> _candidates = new List<NPCController>();

        /// <summary>Single-active state and its change/clear events.</summary>
        public ActiveNPCService ActiveService => _activeService;

        /// <summary>Convenience re-export of <see cref="ActiveNPCService.ActiveNPCChanged"/>.</summary>
        public event Action<ActiveNPCChangedEventArgs> ActiveNPCChanged
        {
            add => _activeService.ActiveNPCChanged += value;
            remove => _activeService.ActiveNPCChanged -= value;
        }

        /// <summary>Convenience re-export of <see cref="ActiveNPCService.ActiveNPCCleared"/>.</summary>
        public event Action<NPCController> ActiveNPCCleared
        {
            add => _activeService.ActiveNPCCleared += value;
            remove => _activeService.ActiveNPCCleared -= value;
        }

        /// <summary>The current ActiveNPC, or null.</summary>
        public NPCController ActiveNpc => _activeService.Current;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning(
                    $"[NPCManager] A second NPCManager on '{name}' was found; destroying it. Keep one per scene.");
                Destroy(this);
                return;
            }

            Instance = this;
            LoadConfig();

            if (logEvents)
            {
                _activeService.ActiveNPCChanged += args => Debug.Log($"[NPCManager] {args}");
                _activeService.ActiveNPCCleared += npc =>
                    Debug.Log($"[NPCManager] ActiveNPC cleared (was {(npc != null ? npc.NpcId : 0)}).");
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void LoadConfig()
        {
            _configManager = new ConfigManager(
                string.IsNullOrWhiteSpace(configFolderOverride) ? null : configFolderOverride);

            Debug.Log($"[NPCManager] Loading NPC config from: {_configManager.ConfigFolderPath}");
            if (!_configManager.LoadNpcConfigs())
            {
                Debug.LogError("[NPCManager] NPC configs failed to load; NPCs will report missing config.");
            }
        }

        /// <summary>Resolves an NPC config row by id. Returns false when not loaded/found.</summary>
        public bool TryGetNpcConfig(int npcId, out NPCConfig config)
        {
            config = null;
            return _configManager != null && _configManager.TryGetNpc(npcId, out config);
        }

        /// <summary>Registers an NPC so the manager can track and (later) query it.</summary>
        public void RegisterNpc(NPCController npc)
        {
            if (npc == null || _registered.Contains(npc))
            {
                return;
            }

            _registered.Add(npc);
            if (logEvents)
            {
                Debug.Log($"[NPCManager] Registered NPC {npc.NpcId} ('{npc.NpcName}'). Total={_registered.Count}.");
            }
        }

        /// <summary>Removes an NPC from tracking and clears it from candidate/active state.</summary>
        public void UnregisterNpc(NPCController npc)
        {
            if (npc == null)
            {
                return;
            }

            _registered.Remove(npc);
            NotifyPlayerExited(npc);
        }

        /// <summary>
        /// Called when the player enters an NPC's range. Adds it to the candidate list
        /// (no duplicates) and, when nothing is active, promotes it. If something is
        /// already active the new NPC is only queued.
        /// </summary>
        public void NotifyPlayerEntered(NPCController npc)
        {
            if (npc == null)
            {
                return;
            }

            if (!_candidates.Contains(npc))
            {
                _candidates.Add(npc);
            }

            if (!_activeService.HasActive)
            {
                Promote(npc);
            }
        }

        /// <summary>
        /// Called when the player leaves an NPC's range. Removes it from the candidate
        /// list; if it was the ActiveNPC, clears it and promotes the next candidate.
        /// </summary>
        public void NotifyPlayerExited(NPCController npc)
        {
            if (npc == null)
            {
                return;
            }

            _candidates.Remove(npc);

            if (_activeService.Current != npc)
            {
                return;
            }

            _activeService.Clear();

            NPCController next = NextCandidate();
            if (next != null)
            {
                Promote(next);
            }
        }

        private void Promote(NPCController npc)
        {
            _activeService.SetActive(npc);
        }

        private NPCController NextCandidate()
        {
            for (int i = 0; i < _candidates.Count; i++)
            {
                if (_candidates[i] != null)
                {
                    return _candidates[i];
                }
            }

            return null;
        }
    }
}
