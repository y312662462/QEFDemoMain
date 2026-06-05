namespace MultiAgentNPC.Dialogue
{
    /// <summary>
    /// One sentence of an NPC reply, matching the LLM JSON schema
    /// (<c>sentences[].text / actionId / expressionId</c>, see
    /// <c>StreamingAssets/Prompts/Shared/json_response_rule.txt</c>).
    ///
    /// Sprint 6 only plays the text and records the ids; driving an Animator from
    /// <see cref="ActionId"/> is deferred to a later sprint.
    /// </summary>
    public class NPCSentence
    {
        /// <summary>The spoken line. Never null (empty when missing).</summary>
        public string Text;

        /// <summary>Animation/action id from the configured action table.</summary>
        public int ActionId;

        /// <summary>Facial expression id.</summary>
        public int ExpressionId;

        public NPCSentence()
        {
        }

        public NPCSentence(string text, int actionId, int expressionId)
        {
            Text = text ?? string.Empty;
            ActionId = actionId;
            ExpressionId = expressionId;
        }
    }
}
