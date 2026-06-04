using System.Collections.Generic;

namespace MultiAgentNPC.Services
{
    /// <summary>One chat message for an LLM request.</summary>
    public class LLMMessage
    {
        public string Role;
        public string Content;

        public LLMMessage(string role, string content)
        {
            Role = role;
            Content = content;
        }

        public static LLMMessage System(string content) => new LLMMessage("system", content);
        public static LLMMessage User(string content) => new LLMMessage("user", content);
        public static LLMMessage Assistant(string content) => new LLMMessage("assistant", content);
    }

    /// <summary>
    /// Provider-agnostic LLM request. The service decides how to map this onto a
    /// concrete vendor API; the business layer never builds vendor payloads.
    /// </summary>
    public class LLMRequest
    {
        public List<LLMMessage> Messages = new List<LLMMessage>();

        /// <summary>Optional per-request temperature override; null uses the configured default.</summary>
        public float? TemperatureOverride;

        /// <summary>Optional per-request max tokens override; null uses the configured default.</summary>
        public int? MaxTokensOverride;

        /// <summary>When true, asks the model to return strict JSON (no Markdown / prose).</summary>
        public bool ForceJson;

        public LLMRequest()
        {
        }

        public LLMRequest(IEnumerable<LLMMessage> messages, bool forceJson = false)
        {
            if (messages != null)
            {
                Messages.AddRange(messages);
            }

            ForceJson = forceJson;
        }
    }

    /// <summary>Provider-agnostic speech-to-text request.</summary>
    public class STTRequest
    {
        public byte[] AudioBytes;

        /// <summary>BCP-47 language tag, e.g. "en-US". Null/empty uses the configured default.</summary>
        public string Language;

        /// <summary>Audio container/format hint, e.g. "wav". Used for file naming / content type.</summary>
        public string Format;

        public STTRequest(byte[] audioBytes, string language = null, string format = "wav")
        {
            AudioBytes = audioBytes;
            Language = language;
            Format = format;
        }
    }

    /// <summary>Provider-agnostic text-to-speech request.</summary>
    public class TTSRequest
    {
        public string Text;

        /// <summary>Optional voice override; null/empty uses the configured default voice.</summary>
        public string VoiceOverride;

        public TTSRequest(string text, string voiceOverride = null)
        {
            Text = text;
            VoiceOverride = voiceOverride;
        }
    }
}
