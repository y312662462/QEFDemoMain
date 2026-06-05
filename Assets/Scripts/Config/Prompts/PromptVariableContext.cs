using System.Collections.Generic;

namespace MultiAgentNPC.Prompts
{
    /// <summary>
    /// Holds the variable values used to render a prompt template (requirements doc 12.6).
    /// Variables are referenced in prompt files as <c>{{variable_name}}</c>.
    ///
    /// Convenience setters exist for the documented variables, but <see cref="Set"/>
    /// accepts any key so new variables can be added without changing this class.
    /// </summary>
    public class PromptVariableContext
    {
        // Documented variable keys (doc 12.6). Centralised so the renderer and
        // callers share the exact same spelling.
        public const string NpcId = "npc_id";
        public const string NpcName = "npc_name";
        public const string NpcResponseText = "npc_response_text";
        public const string PlayerText = "player_text";
        public const string ConversationHistory = "conversation_history";
        public const string CurrentQuestId = "current_quest_id";
        public const string CurrentQuestName = "current_quest_name";
        public const string CurrentQuestDescription = "current_quest_description";
        public const string QuestState = "quest_state";
        public const string TargetNpcId = "target_npc_id";
        public const string ActionTable = "action_table";
        public const string ExpressionTable = "expression_table";
        public const string JsonSchema = "json_schema";
        public const string MaxSentenceCount = "max_sentence_count";
        public const string MaxSentenceLength = "max_sentence_length";
        public const string TargetLanguage = "target_language";

        private readonly Dictionary<string, string> _values = new Dictionary<string, string>();

        /// <summary>Sets a variable value. Null values are stored as empty strings.</summary>
        public PromptVariableContext Set(string key, string value)
        {
            if (string.IsNullOrEmpty(key))
            {
                return this;
            }

            _values[key] = value ?? string.Empty;
            return this;
        }

        /// <summary>Sets a variable using any value convertible to string.</summary>
        public PromptVariableContext Set(string key, object value)
        {
            return Set(key, value?.ToString());
        }

        /// <summary>Returns the value for a variable, or null when it is not set.</summary>
        public string Get(string key)
        {
            return _values.TryGetValue(key, out string value) ? value : null;
        }

        public bool Has(string key) => _values.ContainsKey(key);

        public bool TryGet(string key, out string value) => _values.TryGetValue(key, out value);

        public IReadOnlyDictionary<string, string> Values => _values;
    }
}
