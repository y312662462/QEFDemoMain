using System.Threading;
using System.Threading.Tasks;

namespace MultiAgentNPC.Quest
{
    /// <summary>
    /// Rule-based evaluator for <see cref="QuestType.Composite"/> quests: the parent
    /// is considered complete only when every child quest is <see cref="QuestState.Completed"/>.
    /// A composite with no children never auto-completes (it must be driven explicitly),
    /// avoiding an instant cascade for trigger-style placeholder quests.
    /// </summary>
    public class RuleQuestEvaluator : IQuestEvaluator
    {
        public Task<QuestEvalResult> EvaluateAsync(QuestEvalRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Evaluate(request));
        }

        /// <summary>Synchronous evaluation, used directly by <see cref="QuestManager"/>.</summary>
        public QuestEvalResult Evaluate(QuestEvalRequest request)
        {
            QuestConfig quest = request?.Quest;
            if (quest == null)
            {
                return QuestEvalResult.Failure("RuleQuestEvaluator received a null quest.");
            }

            if (quest.QuestType != QuestType.Composite)
            {
                return QuestEvalResult.Failure(
                    $"RuleQuestEvaluator only handles Composite quests; quest {quest.QuestId} is {quest.QuestType}.");
            }

            var childIds = quest.GetChildQuestIds();
            if (childIds.Count == 0)
            {
                return QuestEvalResult.NotCompleted(
                    "Composite quest has no children; it does not auto-complete.", 1f);
            }

            int completed = 0;
            foreach (int childId in childIds)
            {
                if (request.ChildStates.TryGetValue(childId, out QuestState state) &&
                    state == QuestState.Completed)
                {
                    completed++;
                }
            }

            bool allDone = completed == childIds.Count;
            string reason = $"{completed}/{childIds.Count} child quests completed.";
            return QuestEvalResult.Verdict(allDone, reason, 1f);
        }
    }
}
