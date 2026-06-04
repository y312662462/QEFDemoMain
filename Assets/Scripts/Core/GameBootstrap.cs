using System.Text;
using UnityEngine;
using MultiAgentNPC.Config;
using MultiAgentNPC.Prompts;
using MultiAgentNPC.Quest;

namespace MultiAgentNPC.Core
{
    /// <summary>
    /// Single startup entry point for the multi-agent NPC demo.
    /// Sprint 1: loads the CSV config tables and the prompt files, then logs a
    /// summary to the Console. NPC interaction, quest runtime, dialogue, LLM/STT/TTS
    /// are intentionally not started here yet.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        [Header("Sprint 1 self-test")]
        [Tooltip("Render a sample NPC prompt on startup and log the result.")]
        [SerializeField] private bool runPromptRenderSample = true;

        [Tooltip("NPCID whose default prompt is used for the render sample.")]
        [SerializeField] private int samplePromptNpcId = 10001;

        private ConfigManager _configManager;
        private PromptManager _promptManager;

        private void Awake()
        {
            // Wrapped so a config/prompt failure logs but never freezes startup (doc 12.8.5).
            try
            {
                LoadConfiguration();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[GameBootstrap] Unexpected error during startup load: {e}");
            }
        }

        private void LoadConfiguration()
        {
            _configManager = new ConfigManager();
            _promptManager = new PromptManager();

            Debug.Log($"[GameBootstrap] Loading config from: {_configManager.ConfigFolderPath}");
            _configManager.LoadAll();

            LogNpcSummary();
            LogQuestSummary();

            if (runPromptRenderSample)
            {
                LogPromptRenderSample();
            }
        }

        private void LogNpcSummary()
        {
            if (!_configManager.NpcConfigLoaded)
            {
                Debug.LogWarning("[GameBootstrap] No NPC configs were loaded.");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"[GameBootstrap] Loaded {_configManager.NpcConfigs.Count} NPC config(s):");
            foreach (var kvp in _configManager.NpcConfigs)
            {
                NPCConfig npc = kvp.Value;
                sb.AppendLine($"  - {npc.NpcId}: {npc.NpcName} | default='{npc.DefaultPrompt}' | bindings={npc.TaskBindings.Count}");
            }

            Debug.Log(sb.ToString().TrimEnd());
        }

        private void LogQuestSummary()
        {
            if (!_configManager.QuestConfigLoaded)
            {
                Debug.LogWarning("[GameBootstrap] No Quest configs were loaded.");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"[GameBootstrap] Loaded {_configManager.QuestConfigs.Count} Quest config(s):");
            foreach (var kvp in _configManager.QuestConfigs)
            {
                QuestConfig quest = kvp.Value;
                string extra = quest.QuestType == QuestType.Composite
                    ? $"children=[{string.Join(",", quest.GetChildQuestIds())}]"
                    : $"evalPrompt='{quest.GetEvalPromptFileName()}'";
                sb.AppendLine($"  - {quest.QuestId}: {quest.QuestName} | {quest.QuestType} | next={quest.NextQuestId} | {extra}");
            }

            Debug.Log(sb.ToString().TrimEnd());
        }

        private void LogPromptRenderSample()
        {
            if (!_configManager.TryGetNpc(samplePromptNpcId, out NPCConfig npc))
            {
                Debug.LogWarning($"[GameBootstrap] Prompt render sample skipped: NPCID {samplePromptNpcId} not found.");
                return;
            }

            if (string.IsNullOrWhiteSpace(npc.DefaultPrompt))
            {
                Debug.LogWarning($"[GameBootstrap] Prompt render sample skipped: NPCID {samplePromptNpcId} has no DefaultPrompt.");
                return;
            }

            string jsonSchema = _promptManager.GetRawPrompt("json_response_rule.txt", PromptManager.SubFolder.Shared);

            var context = new PromptVariableContext()
                .Set(PromptVariableContext.NpcId, npc.NpcId)
                .Set(PromptVariableContext.NpcName, npc.NpcName)
                .Set(PromptVariableContext.PlayerText, "I want an apple, please.")
                .Set(PromptVariableContext.ConversationHistory, "(no previous turns)")
                .Set(PromptVariableContext.TargetLanguage, "English")
                .Set(PromptVariableContext.MaxSentenceCount, 3)
                .Set(PromptVariableContext.MaxSentenceLength, 40)
                .Set(PromptVariableContext.JsonSchema, jsonSchema);

            string rendered = _promptManager.GetRenderedNpcPrompt(npc.DefaultPrompt, context);
            Debug.Log(
                $"[GameBootstrap] Rendered prompt sample for NPC {npc.NpcId} ('{npc.DefaultPrompt}'):\n{rendered}");
        }
    }
}
