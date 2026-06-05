using MultiAgentNPC.Config;
using MultiAgentNPC.Prompts;
using MultiAgentNPC.Quest;

namespace MultiAgentNPC.Dialogue
{
    /// <summary>
    /// Result of resolving an NPC prompt: the rendered text to send to the LLM plus the
    /// prompt file and quest context used (for diagnostics / DebugStateStore).
    /// </summary>
    public class ResolvedPrompt
    {
        public string RenderedPrompt;
        public string PromptFile;
        public int QuestId;
        public string QuestName;

        public bool IsValid => !string.IsNullOrEmpty(RenderedPrompt);
    }

    /// <summary>
    /// Chooses which NPC prompt file to use based on <see cref="NPCConfig"/> and the
    /// current <see cref="QuestState"/>, then renders it through the
    /// <see cref="PromptManager"/>. Prompt text itself is never hard-coded here
    /// (Sprint 6 requirement 5): only the file selection and variable values live here.
    ///
    /// Selection order:
    /// 1. An active <c>TargetDialogue</c> quest targeting this NPC -> its binding's ActivePrompt.
    /// 2. Otherwise the best binding by quest state (Active &gt; Completed &gt; Inactive).
    /// 3. Otherwise <see cref="NPCConfig.DefaultPrompt"/>.
    /// </summary>
    public class NPCPromptResolver
    {
        private const string SharedJsonRuleFile = "json_response_rule.txt";

        private readonly PromptManager _prompts;
        private readonly QuestManager _quests;

        /// <summary>Target language passed to the template (doc 12.6 variable).</summary>
        public string TargetLanguage { get; set; } = "English";

        /// <summary>Max sentences the model may return.</summary>
        public int MaxSentenceCount { get; set; } = 3;

        /// <summary>Max characters per sentence.</summary>
        public int MaxSentenceLength { get; set; } = 40;

        /// <param name="prompts">Required prompt source.</param>
        /// <param name="quests">Optional live quest manager; null = always use DefaultPrompt.</param>
        public NPCPromptResolver(PromptManager prompts, QuestManager quests)
        {
            _prompts = prompts;
            _quests = quests;
        }

        /// <summary>
        /// Resolves and renders the prompt for the given NPC and player input. Returns a
        /// result whose <see cref="ResolvedPrompt.IsValid"/> is false when no prompt file
        /// could be selected or it rendered empty.
        /// </summary>
        public ResolvedPrompt Resolve(NPCConfig npc, string playerText, string conversationHistory)
        {
            var result = new ResolvedPrompt();
            if (npc == null || _prompts == null)
            {
                return result;
            }

            string promptFile = SelectPromptFile(npc, result);
            if (string.IsNullOrWhiteSpace(promptFile))
            {
                return result;
            }

            string jsonSchema = _prompts.GetRawPrompt(SharedJsonRuleFile, PromptManager.SubFolder.Shared);

            var context = new PromptVariableContext()
                .Set(PromptVariableContext.NpcId, npc.NpcId)
                .Set(PromptVariableContext.NpcName, npc.NpcName)
                .Set(PromptVariableContext.PlayerText, playerText ?? string.Empty)
                .Set(PromptVariableContext.ConversationHistory,
                    string.IsNullOrEmpty(conversationHistory) ? "(no previous turns)" : conversationHistory)
                .Set(PromptVariableContext.TargetLanguage, TargetLanguage)
                .Set(PromptVariableContext.MaxSentenceCount, MaxSentenceCount)
                .Set(PromptVariableContext.MaxSentenceLength, MaxSentenceLength)
                .Set(PromptVariableContext.JsonSchema, jsonSchema);

            if (result.QuestId != 0 && _quests != null &&
                _quests.TryGetRuntimeState(result.QuestId, out QuestRuntimeState qrs) && qrs.Config != null)
            {
                context
                    .Set(PromptVariableContext.CurrentQuestId, qrs.Config.QuestId)
                    .Set(PromptVariableContext.CurrentQuestName, qrs.Config.QuestName)
                    .Set(PromptVariableContext.CurrentQuestDescription, qrs.Config.QuestDescription)
                    .Set(PromptVariableContext.QuestState, qrs.State.ToString());
            }

            string rendered = _prompts.GetRenderedNpcPrompt(promptFile, context);

            // A binding may reference a prompt file that does not exist on disk (it
            // renders empty). Fall back to the NPC's DefaultPrompt so the turn still runs
            // instead of dead-ending (Sprint 6 requirement 4).
            if (string.IsNullOrEmpty(rendered) &&
                !string.IsNullOrWhiteSpace(npc.DefaultPrompt) &&
                !string.Equals(promptFile, npc.DefaultPrompt, System.StringComparison.OrdinalIgnoreCase))
            {
                UnityEngine.Debug.LogWarning(
                    $"[NPCPromptResolver] Prompt '{promptFile}' for NPC {npc.NpcId} was empty/missing; " +
                    $"falling back to DefaultPrompt '{npc.DefaultPrompt}'.");
                promptFile = npc.DefaultPrompt;
                rendered = _prompts.GetRenderedNpcPrompt(promptFile, context);
            }

            result.PromptFile = promptFile;
            result.RenderedPrompt = rendered;
            return result;
        }

        private string SelectPromptFile(NPCConfig npc, ResolvedPrompt result)
        {
            if (_quests != null && npc.TaskBindings != null && npc.TaskBindings.Count > 0)
            {
                // 1. Active TargetDialogue quest targeting this NPC.
                foreach (QuestRuntimeState quest in _quests.GetActiveTargetDialogueQuestsForNpc(npc.NpcId))
                {
                    NPCPromptBinding binding = FindBinding(npc, quest.QuestId);
                    if (binding != null && !string.IsNullOrWhiteSpace(binding.ActivePrompt))
                    {
                        result.QuestId = quest.QuestId;
                        result.QuestName = quest.Config != null ? quest.Config.QuestName : string.Empty;
                        return binding.ActivePrompt;
                    }
                }

                // 2. Best binding by quest state, preferring Active > Completed > Inactive.
                string byState = SelectBindingByState(npc, QuestState.Active, result)
                    ?? SelectBindingByState(npc, QuestState.Completed, result)
                    ?? SelectBindingByState(npc, QuestState.Inactive, result);
                if (!string.IsNullOrWhiteSpace(byState))
                {
                    return byState;
                }
            }

            // 3. Default prompt.
            result.QuestId = 0;
            result.QuestName = string.Empty;
            return npc.DefaultPrompt;
        }

        private string SelectBindingByState(NPCConfig npc, QuestState wantedState, ResolvedPrompt result)
        {
            foreach (NPCPromptBinding binding in npc.TaskBindings)
            {
                if (binding == null || binding.TaskId == 0)
                {
                    continue;
                }

                if (_quests.GetState(binding.TaskId) != wantedState)
                {
                    continue;
                }

                string prompt = PickPromptForState(binding, wantedState);
                if (string.IsNullOrWhiteSpace(prompt))
                {
                    continue;
                }

                result.QuestId = binding.TaskId;
                result.QuestName = _quests.TryGetRuntimeState(binding.TaskId, out QuestRuntimeState qrs) && qrs.Config != null
                    ? qrs.Config.QuestName
                    : string.Empty;
                return prompt;
            }

            return null;
        }

        private static string PickPromptForState(NPCPromptBinding binding, QuestState state)
        {
            switch (state)
            {
                case QuestState.Active:
                    return binding.ActivePrompt;
                case QuestState.Completed:
                    return binding.CompletedPrompt;
                default:
                    return binding.InactivePrompt;
            }
        }

        private static NPCPromptBinding FindBinding(NPCConfig npc, int questId)
        {
            foreach (NPCPromptBinding binding in npc.TaskBindings)
            {
                if (binding != null && binding.TaskId == questId)
                {
                    return binding;
                }
            }

            return null;
        }
    }
}
