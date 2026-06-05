using UnityEngine;

namespace MultiAgentNPC.Animation
{
    /// <summary>
    /// Placeholder facial-expression driver (Sprint 10). The LLM JSON keeps an
    /// <c>expressionId</c> field, but no real expression resources exist yet, so this
    /// component only records the requested id and safely ignores it - it must NEVER raise
    /// an error when an expression is missing (requirements doc 9.3 / 19.4.6).
    ///
    /// Unknown ids are normalised to <see cref="DefaultExpressionId"/> (2000). A later
    /// sprint can map expression ids to BlendShapes, textures, or an expression Animator
    /// layer without changing the call site.
    /// </summary>
    [AddComponentMenu("MultiAgentNPC/Expression Controller")]
    public class ExpressionController : MonoBehaviour
    {
        /// <summary>Neutral/default expression id (per json_response_rule.txt rule 8).</summary>
        public const int DefaultExpressionId = 2000;

        [Header("Debug")]
        [Tooltip("Log each requested expression id to the Console.")]
        [SerializeField] private bool logExpressions;

        /// <summary>The most recently requested (normalised) expression id.</summary>
        public int CurrentExpressionId { get; private set; } = DefaultExpressionId;

        /// <summary>
        /// Records the requested expression. Since no expression resources exist yet, this
        /// is intentionally a no-op beyond bookkeeping; unknown/invalid ids normalise to
        /// <see cref="DefaultExpressionId"/>. Never throws.
        /// </summary>
        public void ApplyExpression(int expressionId)
        {
            int resolved = IsKnown(expressionId) ? expressionId : DefaultExpressionId;
            CurrentExpressionId = resolved;

            if (logExpressions)
            {
                Debug.Log(
                    $"[ExpressionController] '{name}' expression {expressionId} " +
                    $"(reserved; applied as {resolved}, no resource yet).");
            }
        }

        /// <summary>
        /// Whether an expression id is "known". No real resources exist yet, so only the
        /// neutral default is recognised; everything else falls back to the default. This
        /// keeps the field reserved without ever erroring.
        /// </summary>
        private static bool IsKnown(int expressionId)
        {
            return expressionId == DefaultExpressionId;
        }
    }
}
