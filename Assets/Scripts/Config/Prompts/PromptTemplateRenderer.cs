using System.Text.RegularExpressions;
using UnityEngine;

namespace MultiAgentNPC.Prompts
{
    /// <summary>
    /// Replaces <c>{{variable}}</c> tokens in a prompt template using a
    /// <see cref="PromptVariableContext"/>.
    ///
    /// Unknown variables are left in place (token kept visible) and logged as a
    /// warning so prompt authors notice missing data instead of silently shipping
    /// a broken prompt. Pure/static so it is trivial to unit test.
    /// </summary>
    public static class PromptTemplateRenderer
    {
        // Matches {{ variable_name }} with optional surrounding whitespace.
        private static readonly Regex TokenRegex =
            new Regex(@"\{\{\s*([A-Za-z0-9_]+)\s*\}\}", RegexOptions.Compiled);

        /// <summary>
        /// Renders <paramref name="template"/> against <paramref name="context"/>.
        /// </summary>
        /// <param name="sourceName">Optional name used only for log messages.</param>
        public static string Render(string template, PromptVariableContext context, string sourceName = null)
        {
            if (string.IsNullOrEmpty(template))
            {
                return template ?? string.Empty;
            }

            if (context == null)
            {
                Debug.LogWarning(
                    $"[PromptTemplateRenderer] Null variable context for '{sourceName ?? "<template>"}'. Returning template unchanged.");
                return template;
            }

            string where = string.IsNullOrEmpty(sourceName) ? string.Empty : $" in '{sourceName}'";

            return TokenRegex.Replace(template, match =>
            {
                string key = match.Groups[1].Value;
                if (context.TryGet(key, out string value))
                {
                    return value;
                }

                Debug.LogWarning(
                    $"[PromptTemplateRenderer] Unknown variable '{{{{{key}}}}}'{where}. Leaving token unchanged.");
                return match.Value;
            });
        }
    }
}
