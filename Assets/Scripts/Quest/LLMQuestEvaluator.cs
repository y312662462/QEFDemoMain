using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using MultiAgentNPC.Prompts;
using MultiAgentNPC.Services;

namespace MultiAgentNPC.Quest
{
    /// <summary>
    /// Evaluates <see cref="QuestType.TargetDialogue"/> quests by asking an LLM to
    /// judge the player's utterance against the quest's evaluation prompt.
    ///
    /// Depends only on <see cref="ILLMService"/> (built by the ServiceFactory) and
    /// <see cref="PromptManager"/>; it never references a concrete vendor SDK
    /// (requirements doc 18.3). The expected judging JSON is
    /// <c>{ "isCompleted": bool, "reason": string, "confidence": number }</c>.
    /// </summary>
    public class LLMQuestEvaluator : IQuestEvaluator
    {
        private readonly ILLMService _llm;
        private readonly PromptManager _prompts;

        public LLMQuestEvaluator(ILLMService llm, PromptManager prompts)
        {
            _llm = llm;
            _prompts = prompts;
        }

        public async Task<QuestEvalResult> EvaluateAsync(
            QuestEvalRequest request, CancellationToken cancellationToken = default)
        {
            QuestConfig quest = request?.Quest;
            if (quest == null)
            {
                return QuestEvalResult.Failure("LLMQuestEvaluator received a null quest.");
            }

            if (quest.QuestType != QuestType.TargetDialogue)
            {
                return QuestEvalResult.Failure(
                    $"LLMQuestEvaluator only handles TargetDialogue quests; quest {quest.QuestId} is {quest.QuestType}.");
            }

            if (_llm == null)
            {
                return QuestEvalResult.Failure("LLMQuestEvaluator has no ILLMService.");
            }

            if (_prompts == null)
            {
                return QuestEvalResult.Failure("LLMQuestEvaluator has no PromptManager.");
            }

            string promptFile = quest.GetEvalPromptFileName();
            if (string.IsNullOrWhiteSpace(promptFile))
            {
                return QuestEvalResult.Failure(
                    $"Quest {quest.QuestId} has no evaluation prompt file (QuestParam).");
            }

            var context = new PromptVariableContext()
                .Set(PromptVariableContext.CurrentQuestId, quest.QuestId)
                .Set(PromptVariableContext.CurrentQuestName, quest.QuestName)
                .Set(PromptVariableContext.CurrentQuestDescription, quest.QuestDescription)
                .Set(PromptVariableContext.TargetNpcId, quest.TargetNpcId)
                .Set(PromptVariableContext.PlayerText, request.PlayerText ?? string.Empty)
                .Set(PromptVariableContext.ConversationHistory,
                    string.IsNullOrEmpty(request.ConversationHistory)
                        ? "(no previous turns)"
                        : request.ConversationHistory);

            string rendered = _prompts.GetRenderedQuestEvalPrompt(promptFile, context);
            if (string.IsNullOrEmpty(rendered))
            {
                return QuestEvalResult.Failure(
                    $"Evaluation prompt '{promptFile}' for quest {quest.QuestId} rendered empty.");
            }

            var messages = new List<LLMMessage>
            {
                LLMMessage.System(rendered),
                LLMMessage.User("Return the JSON verdict now.")
            };
            var llmRequest = new LLMRequest(messages, forceJson: true);

            ServiceResult<string> result = await _llm.CompleteAsync(llmRequest, cancellationToken);
            if (!result.IsSuccess)
            {
                return QuestEvalResult.Failure($"LLM call failed: {result}");
            }

            return ParseVerdict(result.Value, quest.QuestId);
        }

        private static QuestEvalResult ParseVerdict(string content, int questId)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return QuestEvalResult.Failure($"Quest {questId}: LLM returned empty content.");
            }

            string json = ExtractJsonObject(content);

            try
            {
                JObject obj = JObject.Parse(json);

                bool isCompleted = obj.Value<bool?>("isCompleted") ?? false;
                string reason = obj.Value<string>("reason") ?? string.Empty;
                float confidence = obj.Value<float?>("confidence") ?? 0f;

                return QuestEvalResult.Verdict(isCompleted, reason, confidence);
            }
            catch (Exception e)
            {
                Debug.LogWarning(
                    $"[LLMQuestEvaluator] Quest {questId}: could not parse verdict JSON: {e.Message}\nContent: {content}");
                return QuestEvalResult.Failure($"Quest {questId}: invalid verdict JSON ({e.Message}).");
            }
        }

        /// <summary>
        /// Tolerates models that wrap JSON in Markdown fences or add stray prose by
        /// extracting the substring between the first '{' and the last '}'.
        /// </summary>
        private static string ExtractJsonObject(string content)
        {
            int start = content.IndexOf('{');
            int end = content.LastIndexOf('}');
            if (start >= 0 && end > start)
            {
                return content.Substring(start, end - start + 1);
            }

            return content;
        }
    }
}
