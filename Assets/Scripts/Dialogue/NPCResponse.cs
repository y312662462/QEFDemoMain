using System.Collections.Generic;
using System.Text;

namespace MultiAgentNPC.Dialogue
{
    /// <summary>
    /// Parsed NPC reply: the ordered list of sentences the pipeline will speak.
    /// <see cref="IsFallback"/> is true when the parser could not read valid JSON from
    /// the LLM and substituted a safe single-sentence reply instead.
    /// </summary>
    public class NPCResponse
    {
        /// <summary>Ordered sentences to play. Never null; may be empty.</summary>
        public List<NPCSentence> Sentences = new List<NPCSentence>();

        /// <summary>True when this response was produced by the parser's fallback path.</summary>
        public bool IsFallback;

        public NPCResponse()
        {
        }

        public NPCResponse(List<NPCSentence> sentences, bool isFallback = false)
        {
            Sentences = sentences ?? new List<NPCSentence>();
            IsFallback = isFallback;
        }

        /// <summary>True when there is at least one sentence with non-blank text.</summary>
        public bool HasContent
        {
            get
            {
                for (int i = 0; i < Sentences.Count; i++)
                {
                    if (Sentences[i] != null && !string.IsNullOrWhiteSpace(Sentences[i].Text))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>All sentence texts joined with a space (for History/subtitles).</summary>
        public string JoinedText()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < Sentences.Count; i++)
            {
                NPCSentence sentence = Sentences[i];
                if (sentence == null || string.IsNullOrWhiteSpace(sentence.Text))
                {
                    continue;
                }

                if (sb.Length > 0)
                {
                    sb.Append(' ');
                }

                sb.Append(sentence.Text.Trim());
            }

            return sb.ToString();
        }
    }
}
