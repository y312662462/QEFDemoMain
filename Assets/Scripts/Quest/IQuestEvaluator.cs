using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MultiAgentNPC.Quest
{
    /// <summary>
    /// Input for a single quest evaluation. Carries everything an evaluator might
    /// need so evaluators stay free of any reference to the live <see cref="QuestManager"/>.
    /// </summary>
    public class QuestEvalRequest
    {
        /// <summary>Quest being evaluated.</summary>
        public QuestConfig Quest { get; }

        /// <summary>Latest player utterance (TargetDialogue evaluation).</summary>
        public string PlayerText { get; }

        /// <summary>Recent conversation history text (TargetDialogue evaluation).</summary>
        public string ConversationHistory { get; }

        /// <summary>Current NPC display name (TargetDialogue evaluation).</summary>
        public string NpcName { get; }

        /// <summary>The NPC's reply text for the current turn (TargetDialogue evaluation).</summary>
        public string NpcResponseText { get; }

        /// <summary>
        /// Snapshot of child quest states, keyed by child quest id (Composite evaluation).
        /// Empty for non-composite quests.
        /// </summary>
        public IReadOnlyDictionary<int, QuestState> ChildStates { get; }

        public QuestEvalRequest(
            QuestConfig quest,
            string playerText = null,
            string conversationHistory = null,
            IReadOnlyDictionary<int, QuestState> childStates = null,
            string npcName = null,
            string npcResponseText = null)
        {
            Quest = quest;
            PlayerText = playerText;
            ConversationHistory = conversationHistory;
            ChildStates = childStates ?? new Dictionary<int, QuestState>();
            NpcName = npcName;
            NpcResponseText = npcResponseText;
        }
    }

    /// <summary>
    /// Outcome of a quest evaluation. Mirrors the judging JSON
    /// <c>{ "isCompleted", "reason", "confidence" }</c> plus a transport-level
    /// success flag so callers can distinguish "evaluated, not completed" from
    /// "evaluation failed".
    /// </summary>
    public class QuestEvalResult
    {
        /// <summary>True when the evaluation ran and produced a verdict.</summary>
        public bool IsSuccess { get; private set; }

        /// <summary>The verdict: whether the quest objective is met.</summary>
        public bool IsCompleted { get; private set; }

        /// <summary>Human-readable rationale from the evaluator.</summary>
        public string Reason { get; private set; }

        /// <summary>Confidence in [0,1]; 0 when not provided.</summary>
        public float Confidence { get; private set; }

        /// <summary>Populated when <see cref="IsSuccess"/> is false.</summary>
        public string ErrorMessage { get; private set; }

        public static QuestEvalResult Completed(string reason, float confidence)
        {
            return new QuestEvalResult
            {
                IsSuccess = true,
                IsCompleted = true,
                Reason = reason,
                Confidence = confidence
            };
        }

        public static QuestEvalResult NotCompleted(string reason, float confidence)
        {
            return new QuestEvalResult
            {
                IsSuccess = true,
                IsCompleted = false,
                Reason = reason,
                Confidence = confidence
            };
        }

        public static QuestEvalResult Verdict(bool isCompleted, string reason, float confidence)
        {
            return isCompleted ? Completed(reason, confidence) : NotCompleted(reason, confidence);
        }

        public static QuestEvalResult Failure(string errorMessage)
        {
            return new QuestEvalResult
            {
                IsSuccess = false,
                IsCompleted = false,
                ErrorMessage = errorMessage
            };
        }

        public override string ToString()
        {
            return IsSuccess
                ? $"Eval(completed={IsCompleted}, confidence={Confidence:0.00}, reason='{Reason}')"
                : $"Eval(FAILED, msg='{ErrorMessage}')";
        }
    }

    /// <summary>
    /// Decides whether a quest's objective has been met. Implementations may be
    /// rule-based (synchronous) or LLM-backed (asynchronous); both return a Task so
    /// the manager/test entry can await them uniformly.
    /// </summary>
    public interface IQuestEvaluator
    {
        Task<QuestEvalResult> EvaluateAsync(QuestEvalRequest request, CancellationToken cancellationToken = default);
    }
}
