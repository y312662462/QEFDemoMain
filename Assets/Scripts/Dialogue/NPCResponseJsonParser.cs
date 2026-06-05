using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace MultiAgentNPC.Dialogue
{
    /// <summary>
    /// Parses the LLM reply into an <see cref="NPCResponse"/>. Tolerant by design
    /// (Sprint 6 requirement 2): it strips Markdown code fences / stray prose and reads
    /// the first JSON object. When that still fails it returns a safe single-sentence
    /// fallback (<see cref="NPCResponse.IsFallback"/> = true) so the pipeline keeps
    /// flowing instead of dead-ending.
    /// </summary>
    public static class NPCResponseJsonParser
    {
        /// <summary>Default action id when the model is unsure (per json_response_rule.txt rule 7).</summary>
        public const int DefaultActionId = 1203;

        /// <summary>Neutral expression id (per json_response_rule.txt rule 8).</summary>
        public const int DefaultExpressionId = 2000;

        /// <summary>Safe English line used when the response cannot be parsed.</summary>
        public const string FallbackText = "Sorry, I didn't catch that. Could you say it again?";

        /// <summary>
        /// Parses <paramref name="raw"/> into an <see cref="NPCResponse"/>. Never throws
        /// and never returns null. <paramref name="summary"/> is a short human-readable
        /// description of the parse outcome for diagnostics.
        /// </summary>
        public static NPCResponse Parse(string raw, out string summary)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                summary = "fallback: empty LLM response";
                return BuildFallback();
            }

            string json = ExtractJsonObject(StripCodeFence(raw));

            try
            {
                JObject root = JObject.Parse(json);
                JToken sentencesToken = root["sentences"];

                if (sentencesToken is JArray sentencesArray && sentencesArray.Count > 0)
                {
                    var sentences = new List<NPCSentence>();
                    foreach (JToken item in sentencesArray)
                    {
                        string text = item.Value<string>("text");
                        if (string.IsNullOrWhiteSpace(text))
                        {
                            continue;
                        }

                        int actionId = item.Value<int?>("actionId") ?? DefaultActionId;
                        int expressionId = item.Value<int?>("expressionId") ?? DefaultExpressionId;
                        sentences.Add(new NPCSentence(text.Trim(), actionId, expressionId));
                    }

                    if (sentences.Count > 0)
                    {
                        summary = $"ok: {sentences.Count} sentence(s)";
                        return new NPCResponse(sentences, isFallback: false);
                    }

                    summary = "fallback: sentences array had no usable text";
                    return BuildFallback();
                }

                summary = "fallback: missing/empty 'sentences' array";
                return BuildFallback();
            }
            catch (Exception e)
            {
                Debug.LogWarning(
                    $"[NPCResponseJsonParser] Could not parse response JSON: {e.Message}\nRaw: {raw}");
                summary = $"fallback: invalid JSON ({e.Message})";
                return BuildFallback();
            }
        }

        /// <summary>Builds the safe single-sentence fallback response.</summary>
        public static NPCResponse BuildFallback()
        {
            var sentences = new List<NPCSentence>
            {
                new NPCSentence(FallbackText, DefaultActionId, DefaultExpressionId)
            };
            return new NPCResponse(sentences, isFallback: true);
        }

        /// <summary>
        /// Removes a leading/trailing Markdown code fence (``` or ```json) when the model
        /// wrapped its JSON despite being asked not to.
        /// </summary>
        private static string StripCodeFence(string content)
        {
            string trimmed = content.Trim();
            if (!trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                return trimmed;
            }

            // Drop the first fence line (``` or ```json).
            int firstNewline = trimmed.IndexOf('\n');
            if (firstNewline >= 0)
            {
                trimmed = trimmed.Substring(firstNewline + 1);
            }

            // Drop a trailing closing fence.
            int closing = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (closing >= 0)
            {
                trimmed = trimmed.Substring(0, closing);
            }

            return trimmed.Trim();
        }

        /// <summary>
        /// Extracts the substring between the first '{' and the last '}' so stray prose
        /// around the JSON object is ignored (mirrors LLMQuestEvaluator's tolerance).
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
