using System.IO;
using UnityEngine;

namespace MultiAgentNPC.Services
{
    /// <summary>
    /// Loads <see cref="AIServiceSecrets"/> from a local JSON file that lives OUTSIDE
    /// the Unity <c>Assets</c> folder (project-root <c>Secrets/</c> by default), so the
    /// file is never imported, never bundled into a build, and trivially git-ignored.
    ///
    /// Never throws: a missing or malformed file logs a clear hint and returns null,
    /// letting the caller fall back to Inspector values.
    /// </summary>
    public static class SecretsLoader
    {
        public const string DefaultSecretsFolder = "Secrets";
        public const string DefaultSecretsFileName = "ai_keys.json";
        public const string TemplateFileName = "ai_keys.template.json";

        /// <summary>Absolute path to the Unity project root (the parent of <c>Assets</c>).</summary>
        public static string ProjectRoot
        {
            get
            {
                DirectoryInfo parent = Directory.GetParent(Application.dataPath);
                return parent != null ? parent.FullName : Application.dataPath;
            }
        }

        /// <summary>Default secrets path: <c>&lt;ProjectRoot&gt;/Secrets/ai_keys.json</c>.</summary>
        public static string DefaultPath =>
            Path.Combine(ProjectRoot, DefaultSecretsFolder, DefaultSecretsFileName);

        /// <summary>
        /// Loads secrets from <paramref name="path"/> (or <see cref="DefaultPath"/> when blank).
        /// Returns null when the file is missing or cannot be parsed.
        /// </summary>
        public static AIServiceSecrets Load(string path = null)
        {
            path = string.IsNullOrWhiteSpace(path) ? DefaultPath : path;

            if (!File.Exists(path))
            {
                Debug.LogWarning(
                    $"[SecretsLoader] Secrets file not found: {path}\n" +
                    $"Copy '{TemplateFileName}' to '{DefaultSecretsFileName}' next to it and fill in your keys. " +
                    "API keys will then load from there instead of the Inspector.");
                return null;
            }

            string json;
            try
            {
                json = File.ReadAllText(path);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SecretsLoader] Failed to read secrets file '{path}': {e.Message}");
                return null;
            }

            if (string.IsNullOrWhiteSpace(json))
            {
                Debug.LogWarning($"[SecretsLoader] Secrets file is empty: {path}");
                return null;
            }

            try
            {
                AIServiceSecrets secrets = JsonUtility.FromJson<AIServiceSecrets>(json);
                if (secrets == null)
                {
                    Debug.LogError($"[SecretsLoader] Secrets file parsed to null: {path}");
                    return null;
                }

                // Do NOT log key values.
                Debug.Log($"[SecretsLoader] Loaded API keys from {path}.");
                return secrets;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SecretsLoader] Secrets file is not valid JSON ('{path}'): {e.Message}");
                return null;
            }
        }
    }
}
