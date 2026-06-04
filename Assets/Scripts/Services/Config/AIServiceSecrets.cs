using System;

namespace MultiAgentNPC.Services
{
    /// <summary>
    /// Secret-only counterpart to <see cref="AIServiceSettings"/>. These fields are
    /// loaded at runtime from a local, git-ignored JSON file so real API keys are
    /// never serialized into a scene / prefab and never committed to Git.
    ///
    /// Field names must match the JSON keys exactly (Unity <see cref="UnityEngine.JsonUtility"/>).
    /// Leave any value blank to fall back to whatever is configured in the Inspector.
    /// </summary>
    [Serializable]
    public class AIServiceSecrets
    {
        public string llmApiKey = string.Empty;

        public string sttApiKey = string.Empty;
        public string sttRegion = string.Empty;

        public string ttsApiKey = string.Empty;
        public string ttsRegion = string.Empty;
    }
}
