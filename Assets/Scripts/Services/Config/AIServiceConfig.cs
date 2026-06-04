using System.IO;
using UnityEngine;

namespace MultiAgentNPC.Services
{
    /// <summary>
    /// Scene component that exposes the AI service settings to the rest of the game.
    ///
    /// API keys are NOT entered in the Inspector and are NOT serialized into the
    /// scene/prefab. Instead they are loaded at runtime from a local, git-ignored
    /// JSON file (project-root <c>Secrets/ai_keys.json</c> by default; see
    /// <see cref="SecretsLoader"/>). The Inspector only holds non-secret settings
    /// (provider, model, endpoints, voice, timeouts).
    ///
    /// <see cref="Settings"/> returns a deep-cloned, secrets-injected copy, so the
    /// serialized object on this component is never mutated and the keys can never
    /// leak back into the committed scene file.
    /// </summary>
    [AddComponentMenu("MultiAgentNPC/AI Service Config")]
    public class AIServiceConfig : MonoBehaviour
    {
        [Tooltip("Non-secret configuration. Leave the ApiKey fields BLANK; keys load from the secrets file.")]
        [SerializeField] private AIServiceSettings settings = new AIServiceSettings();

        [Header("Secrets (Git-safe)")]
        [Tooltip("Load API keys from the local git-ignored secrets file instead of the Inspector.")]
        [SerializeField] private bool loadSecretsFromFile = true;

        [Tooltip("Optional. Blank = <ProjectRoot>/Secrets/ai_keys.json. Relative paths resolve from the project root.")]
        [SerializeField] private string secretsFilePathOverride = string.Empty;

        private AIServiceSettings _effectiveSettings;

        /// <summary>
        /// Effective settings with secrets applied. Built lazily and cached; call
        /// <see cref="ReloadSettings"/> to force a refresh after editing the secrets file.
        /// </summary>
        public AIServiceSettings Settings => GetEffectiveSettings();

        public AIServiceSettings GetEffectiveSettings(bool forceReload = false)
        {
            if (_effectiveSettings != null && !forceReload)
            {
                return _effectiveSettings;
            }

            // Deep clone so the serialized component object is never mutated (keys can
            // therefore never be written back into the scene file).
            _effectiveSettings = CloneSettings(settings);

            if (loadSecretsFromFile)
            {
                AIServiceSecrets secrets = SecretsLoader.Load(ResolveSecretsPath());
                ApplySecrets(_effectiveSettings, secrets);
            }

            return _effectiveSettings;
        }

        /// <summary>Drops the cached effective settings so the next access reloads the file.</summary>
        public void ReloadSettings()
        {
            _effectiveSettings = null;
        }

        private string ResolveSecretsPath()
        {
            if (string.IsNullOrWhiteSpace(secretsFilePathOverride))
            {
                return SecretsLoader.DefaultPath;
            }

            return Path.IsPathRooted(secretsFilePathOverride)
                ? secretsFilePathOverride
                : Path.Combine(SecretsLoader.ProjectRoot, secretsFilePathOverride);
        }

        private static AIServiceSettings CloneSettings(AIServiceSettings source)
        {
            if (source == null)
            {
                return new AIServiceSettings();
            }

            // JsonUtility round-trip deep-copies all [Serializable] fields.
            return JsonUtility.FromJson<AIServiceSettings>(JsonUtility.ToJson(source));
        }

        private static void ApplySecrets(AIServiceSettings target, AIServiceSecrets secrets)
        {
            if (target == null || secrets == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(secrets.llmApiKey))
            {
                target.Llm.ApiKey = secrets.llmApiKey.Trim();
            }

            if (!string.IsNullOrWhiteSpace(secrets.sttApiKey))
            {
                target.Stt.ApiKey = secrets.sttApiKey.Trim();
            }

            if (!string.IsNullOrWhiteSpace(secrets.sttRegion))
            {
                target.Stt.Region = secrets.sttRegion.Trim();
            }

            if (!string.IsNullOrWhiteSpace(secrets.ttsApiKey))
            {
                target.Tts.ApiKey = secrets.ttsApiKey.Trim();
            }

            if (!string.IsNullOrWhiteSpace(secrets.ttsRegion))
            {
                target.Tts.Region = secrets.ttsRegion.Trim();
            }
        }
    }
}
