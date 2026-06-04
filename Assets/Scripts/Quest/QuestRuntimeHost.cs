using System;
using System.Linq;
using System.Text;
using UnityEngine;
using MultiAgentNPC.Config;

namespace MultiAgentNPC.Quest
{
    /// <summary>
    /// Temporary, additive runtime owner of a single <see cref="QuestManager"/> so
    /// <c>QuestUI</c> has a live quest instance to bind to during Sprint 5. This is NOT
    /// production wiring: a later sprint will create and own the QuestManager from
    /// GameBootstrap/RuntimeContext and this host can be removed (QuestUI binds to
    /// whichever owner is provided).
    ///
    /// It loads quest configs into its own <see cref="ConfigManager"/>, builds and
    /// initializes the manager, and exposes it. The ContextMenu helpers drive the very
    /// same instance the UI observes, so manual transitions are reflected on screen.
    /// </summary>
    [AddComponentMenu("MultiAgentNPC/Quest Runtime Host (Debug)")]
    public class QuestRuntimeHost : MonoBehaviour
    {
        [Header("Startup")]
        [Tooltip("Force the start quest id. 0 = auto-detect the root quest.")]
        [SerializeField] private int startQuestIdOverride = 0;

        [Tooltip("Optional override for the config folder. Empty = StreamingAssets/Config.")]
        [SerializeField] private string configFolderOverride = string.Empty;

        [Tooltip("Build and initialize the quest manager automatically on Awake.")]
        [SerializeField] private bool initializeOnAwake = true;

        [Header("Manual Test Inputs")]
        [Tooltip("Quest id used by the 'Activate/Complete Quest For Test' context menus.")]
        [SerializeField] private int questIdField = 10001;

        private ConfigManager _configManager;
        private QuestManager _questManager;

        /// <summary>
        /// Raised whenever a new <see cref="QuestManager"/> instance is built (initial
        /// Awake or a context-menu re-init). Consumers like QuestUI rebind to the new
        /// instance. Subscribe and also read <see cref="QuestManager"/> once on bind to
        /// cover either Awake order.
        /// </summary>
        public event Action<QuestManager> QuestManagerInitialized;

        /// <summary>The live quest manager, or null until initialized.</summary>
        public QuestManager QuestManager => _questManager;

        /// <summary>True once a quest manager has been built and initialized.</summary>
        public bool IsInitialized => _questManager != null && _questManager.Initialized;

        private void Awake()
        {
            if (initializeOnAwake)
            {
                InitializeQuestRuntime();
            }
        }

        /// <summary>
        /// Loads quest configs, builds the <see cref="QuestManager"/> and activates the
        /// start quest. Safe to re-run from the context menu to reset the runtime.
        /// </summary>
        [ContextMenu("Initialize Quest Runtime")]
        public void InitializeQuestRuntime()
        {
            _configManager = new ConfigManager(
                string.IsNullOrWhiteSpace(configFolderOverride) ? null : configFolderOverride);

            Debug.Log($"[QuestRuntimeHost] Loading quest config from: {_configManager.ConfigFolderPath}");
            if (!_configManager.LoadQuestConfigs())
            {
                Debug.LogError("[QuestRuntimeHost] Quest configs failed to load; quest UI will be empty.");
                return;
            }

            _questManager = new QuestManager(_configManager, startQuestIdOverride);
            _questManager.Initialize();

            Debug.Log("[QuestRuntimeHost] Quest runtime initialized.");

            try
            {
                QuestManagerInitialized?.Invoke(_questManager);
            }
            catch (Exception e)
            {
                Debug.LogError($"[QuestRuntimeHost] A QuestManagerInitialized subscriber threw: {e}");
            }
        }

        [ContextMenu("Activate Quest For Test")]
        public void ActivateQuestForTest()
        {
            if (!EnsureInitialized())
            {
                return;
            }

            bool ok = _questManager.TryActivateQuest(questIdField);
            Debug.Log($"[QuestRuntimeHost] TryActivateQuest({questIdField}) -> {ok}");
        }

        [ContextMenu("Complete Quest For Test")]
        public void CompleteQuestForTest()
        {
            if (!EnsureInitialized())
            {
                return;
            }

            bool ok = _questManager.TryCompleteQuest(questIdField);
            Debug.Log($"[QuestRuntimeHost] TryCompleteQuest({questIdField}) -> {ok}");
        }

        [ContextMenu("Log Quest States")]
        public void LogQuestStates()
        {
            if (!EnsureInitialized())
            {
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"[QuestRuntimeHost] Quest states ({_questManager.States.Count}):");
            foreach (QuestRuntimeState state in _questManager.States.Values.OrderBy(s => s.Config.SortOrder))
            {
                sb.AppendLine($"  - {state.QuestId} [{state.State}] {state.Config.QuestName} ({state.Config.QuestType})");
            }

            Debug.Log(sb.ToString().TrimEnd());
        }

        private bool EnsureInitialized()
        {
            if (_questManager == null)
            {
                Debug.LogWarning("[QuestRuntimeHost] Not initialized; run 'Initialize Quest Runtime' first.");
                return false;
            }

            return true;
        }
    }
}
