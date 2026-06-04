using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using MultiAgentNPC.Config;
using MultiAgentNPC.Prompts;
using MultiAgentNPC.Services;

namespace MultiAgentNPC.Quest
{
    /// <summary>
    /// Minimal manual test entry for the Sprint 3 quest system. Drive transitions
    /// from the component context menu (right-click the component header) in Play
    /// Mode. This is a Debug-only harness, not part of any runtime flow.
    ///
    /// It owns its own <see cref="ConfigManager"/>, <see cref="PromptManager"/> and
    /// <see cref="QuestManager"/>, logs every <see cref="QuestManager.QuestStateChanged"/>
    /// event, and can run a real LLM evaluation via <see cref="ILLMService"/>.
    /// </summary>
    [AddComponentMenu("MultiAgentNPC/Quest System Tester")]
    public class QuestSystemTester : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Source of API keys / provider settings for the LLM evaluator. Falls back to a sibling component if unset.")]
        [SerializeField] private AIServiceConfig config;

        [Header("Start Quest")]
        [Tooltip("Force the start quest id. 0 = auto-detect the root quest.")]
        [SerializeField] private int startQuestIdOverride = 0;

        [Header("Manual Test Inputs")]
        [Tooltip("Quest id used by 'Activate Quest' / 'Complete Quest'.")]
        [SerializeField] private int questIdField = 10001;

        [Tooltip("NPC id used by 'Evaluate Dialogue Quest via LLM' (TargetNPCID filter).")]
        [SerializeField] private int npcIdField = 10001;

        [TextArea]
        [Tooltip("Player utterance sent to the LLM evaluator.")]
        [SerializeField] private string playerTextField = "I want an apple, please.";

        [TextArea]
        [Tooltip("Conversation history sent to the LLM evaluator. Empty = none.")]
        [SerializeField] private string conversationHistoryField = string.Empty;

        private ConfigManager _configManager;
        private PromptManager _promptManager;
        private QuestManager _questManager;

        private AIServiceSettings Settings
        {
            get
            {
                if (config == null)
                {
                    config = GetComponent<AIServiceConfig>();
                }

                return config != null ? config.Settings : null;
            }
        }

        private void Awake()
        {
            InitQuestSystem();
        }

        [ContextMenu("Init Quest System")]
        public void InitQuestSystem()
        {
            _configManager = new ConfigManager();
            _promptManager = new PromptManager();

            Debug.Log($"[QuestSystemTester] Loading config from: {_configManager.ConfigFolderPath}");
            if (!_configManager.LoadQuestConfigs())
            {
                Debug.LogError("[QuestSystemTester] Quest configs failed to load; aborting init.");
                return;
            }

            // NPC configs are optional for quest logic but harmless to load for context.
            _configManager.LoadNpcConfigs();

            if (_questManager != null)
            {
                _questManager.QuestStateChanged -= OnQuestStateChanged;
            }

            _questManager = new QuestManager(_configManager, startQuestIdOverride);
            _questManager.QuestStateChanged += OnQuestStateChanged;
            _questManager.Initialize();

            LogQuestStates();
        }

        [ContextMenu("Activate Quest (questIdField)")]
        public void ActivateQuest()
        {
            if (!EnsureInitialized())
            {
                return;
            }

            bool ok = _questManager.TryActivateQuest(questIdField);
            Debug.Log($"[QuestSystemTester] TryActivateQuest({questIdField}) -> {ok}");
        }

        [ContextMenu("Complete Quest (questIdField)")]
        public void CompleteQuest()
        {
            if (!EnsureInitialized())
            {
                return;
            }

            bool ok = _questManager.TryCompleteQuest(questIdField);
            Debug.Log($"[QuestSystemTester] TryCompleteQuest({questIdField}) -> {ok}");
        }

        [ContextMenu("Log Quest States")]
        public void LogQuestStates()
        {
            if (!EnsureInitialized())
            {
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"[QuestSystemTester] Quest states ({_questManager.States.Count}):");
            foreach (var state in _questManager.States.Values.OrderBy(s => s.Config.SortOrder))
            {
                sb.AppendLine($"  - {state.QuestId} [{state.State}] {state.Config.QuestName} ({state.Config.QuestType})");
            }

            Debug.Log(sb.ToString().TrimEnd());
        }

        [ContextMenu("Evaluate Dialogue Quest via LLM")]
        public async void EvaluateDialogueQuestViaLLM()
        {
            await EvaluateDialogueQuestViaLLMAsync();
        }

        public async Task EvaluateDialogueQuestViaLLMAsync()
        {
            if (!EnsureInitialized())
            {
                return;
            }

            QuestRuntimeState target = _questManager
                .GetActiveTargetDialogueQuestsForNpc(npcIdField)
                .FirstOrDefault();

            if (target == null)
            {
                Debug.LogWarning(
                    $"[QuestSystemTester] No active TargetDialogue quest for NPC {npcIdField}. " +
                    "Check the quest is Active and its TargetNPCID matches.");
                return;
            }

            AIServiceSettings settings = Settings;
            if (settings == null)
            {
                Debug.LogError("[QuestSystemTester] No AIServiceConfig assigned; cannot run the LLM evaluator.");
                return;
            }

            ILLMService llm = ServiceFactory.CreateLLMService(settings);
            if (llm == null)
            {
                return;
            }

            var evaluator = new LLMQuestEvaluator(llm, _promptManager);
            var request = new QuestEvalRequest(
                target.Config,
                playerText: playerTextField,
                conversationHistory: conversationHistoryField);

            Debug.Log(
                $"[QuestSystemTester] Evaluating quest {target.QuestId} ('{target.Config.QuestName}') " +
                $"for NPC {npcIdField} via {settings.Llm.Provider}...");

            QuestEvalResult result = await evaluator.EvaluateAsync(request, CancellationToken.None);
            Debug.Log($"[QuestSystemTester] LLM verdict for quest {target.QuestId}: {result}");

            if (!result.IsSuccess)
            {
                return;
            }

            target.LastReason = result.Reason;
            target.LastConfidence = result.Confidence;

            if (result.IsCompleted)
            {
                _questManager.TryCompleteQuest(target.QuestId);
            }
        }

        private void OnQuestStateChanged(QuestStateChangedEventArgs args)
        {
            Debug.Log($"[QuestSystemTester] EVENT {args}");
        }

        private bool EnsureInitialized()
        {
            if (_questManager == null)
            {
                Debug.LogWarning("[QuestSystemTester] Quest system not initialized; run 'Init Quest System' first.");
                return false;
            }

            return true;
        }
    }
}
