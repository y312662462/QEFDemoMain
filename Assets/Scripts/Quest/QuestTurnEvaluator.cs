using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using MultiAgentNPC.DebugTools;
using MultiAgentNPC.Prompts;
using MultiAgentNPC.Services;

namespace MultiAgentNPC.Quest
{
    /// <summary>
    /// Sprint 9 orchestrator that turns a committed dialogue turn into a quest verdict.
    /// Owned by the dialogue host and invoked fire-and-forget AFTER a turn has been played
    /// and committed to History, so it can never block playback or the return to InRange.
    ///
    /// Responsibilities:
    /// - Only evaluate Active <see cref="QuestType.TargetDialogue"/> quests whose
    ///   <c>TargetNpcId</c> matches the committed turn's NPC (no LLM call otherwise).
    /// - Run the real <see cref="LLMQuestEvaluator"/> under a per-evaluation timeout linked
    ///   to a manager-lifetime token (NOT the dialogue session token).
    /// - Re-validate the quest's live state before applying completion, since results may
    ///   return late. Completion goes through <see cref="QuestManager.TryCompleteQuest"/>
    ///   which is idempotent and cascades to composite parents.
    /// - Surface a verdict summary to <see cref="DebugStateStore"/> on every path and never
    ///   throw out to the caller.
    /// </summary>
    public class QuestTurnEvaluator
    {
        private readonly ILLMService _llm;
        private readonly PromptManager _prompts;

        /// <summary>Per-evaluation timeout in milliseconds (req 2).</summary>
        public int EvalTimeoutMs { get; set; } = 15000;

        public QuestTurnEvaluator(ILLMService llm, PromptManager prompts)
        {
            _llm = llm;
            _prompts = prompts;
        }

        /// <summary>
        /// Evaluates the active TargetDialogue quest(s) for the committed turn's NPC.
        /// Never throws; all failures/timeouts are reported to the debug store only.
        /// </summary>
        public async Task EvaluateTurnAsync(QuestManager quests, CommittedTurn turn, CancellationToken managerToken)
        {
            if (quests == null || turn == null)
            {
                return;
            }

            if (_llm == null || _prompts == null)
            {
                DebugStateStore.Instance.SetLastQuestVerdict("Quest eval unavailable (no LLM/PromptManager).");
                return;
            }

            // Snapshot matching quest ids up front: TryCompleteQuest mutates the manager's
            // state dictionary, so we must not iterate the live enumerable while applying.
            var questIds = new List<int>();
            foreach (QuestRuntimeState state in quests.GetActiveTargetDialogueQuestsForNpc(turn.NpcId))
            {
                questIds.Add(state.QuestId);
            }

            // Req 6: wrong NPC (no active TargetDialogue quest bound to this NPC) -> no LLM
            // call, no state change, just a local debug message so testing is clear.
            if (questIds.Count == 0)
            {
                DebugStateStore.Instance.SetLastQuestVerdict("Skipped: no active TargetDialogue quest for this NPC.");
                return;
            }

            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(managerToken))
            {
                if (EvalTimeoutMs > 0)
                {
                    linked.CancelAfter(EvalTimeoutMs);
                }

                var evaluator = new LLMQuestEvaluator(_llm, _prompts);

                foreach (int questId in questIds)
                {
                    await EvaluateOneAsync(quests, evaluator, questId, turn, linked.Token);
                }
            }
        }

        private async Task EvaluateOneAsync(
            QuestManager quests,
            LLMQuestEvaluator evaluator,
            int questId,
            CommittedTurn turn,
            CancellationToken token)
        {
            try
            {
                if (!quests.TryGetRuntimeState(questId, out QuestRuntimeState state) || state == null)
                {
                    return;
                }

                QuestConfig config = state.Config;
                var request = new QuestEvalRequest(
                    config,
                    playerText: turn.PlayerText,
                    conversationHistory: turn.PriorHistory,
                    npcName: turn.NpcName,
                    npcResponseText: turn.NpcResponseText);

                QuestEvalResult result = await evaluator.EvaluateAsync(request, token);

                if (!result.IsSuccess)
                {
                    DebugStateStore.Instance.SetLastQuestVerdict(
                        $"Quest {questId} eval failed: {result.ErrorMessage}");
                    return;
                }

                DebugStateStore.Instance.SetCurrentQuest(questId, config.QuestName);
                DebugStateStore.Instance.SetLastQuestVerdict(
                    $"Quest {questId} '{config.QuestName}': completed={result.IsCompleted}, " +
                    $"confidence={result.Confidence:0.00}, reason='{result.Reason}'");

                if (!result.IsCompleted)
                {
                    return;
                }

                // Req 5: late-result guard. The result may return long after the turn (the
                // player may have moved on). Re-validate the LIVE quest state before applying.
                if (!quests.TryGetRuntimeState(questId, out QuestRuntimeState live) || live == null)
                {
                    return;
                }

                if (live.State != QuestState.Active ||
                    live.Config.QuestType != QuestType.TargetDialogue ||
                    live.Config.TargetNpcId != turn.NpcId)
                {
                    // No longer applicable (already completed elsewhere, deactivated, or NPC
                    // mismatch). TryCompleteQuest is also idempotent, but skipping here keeps
                    // the verdict log honest.
                    return;
                }

                live.LastReason = result.Reason;
                live.LastConfidence = result.Confidence;

                quests.TryCompleteQuest(questId);
            }
            catch (OperationCanceledException)
            {
                // Timeout or manager shutdown. Never affects dialogue; just note it.
                DebugStateStore.Instance.SetLastQuestVerdict(
                    $"Quest {questId} eval cancelled (timeout/shutdown).");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[QuestTurnEvaluator] Quest {questId} evaluation error: {e}");
                DebugStateStore.Instance.SetLastQuestVerdict($"Quest {questId} eval error: {e.Message}");
            }
        }
    }
}
