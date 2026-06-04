using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MultiAgentNPC.Prompts
{
    /// <summary>
    /// Reads and caches prompt <c>.txt</c> files from
    /// <c>Application.streamingAssetsPath/Prompts/{SubFolder}</c> (requirements doc 12.5).
    ///
    /// Callers reference prompts by file name only (doc 12.6); this manager resolves
    /// the folder. Raw text is cached so repeated renders do not re-read from disk.
    /// Missing files log a clear error and return an empty string rather than throwing.
    /// </summary>
    public class PromptManager
    {
        public const string DefaultPromptsFolder = "Prompts";

        /// <summary>Known prompt sub-folders (doc 12.5).</summary>
        public static class SubFolder
        {
            public const string System = "System";
            public const string Npc = "NPC";
            public const string QuestEval = "QuestEval";
            public const string Shared = "Shared";
        }

        private readonly string _promptsRootPath;
        private readonly Dictionary<string, string> _rawCache = new Dictionary<string, string>();

        /// <summary>
        /// Creates a prompt manager. When <paramref name="promptsRootPath"/> is null,
        /// defaults to <c>Application.streamingAssetsPath/Prompts</c>.
        /// </summary>
        public PromptManager(string promptsRootPath = null)
        {
            _promptsRootPath = string.IsNullOrEmpty(promptsRootPath)
                ? Path.Combine(Application.streamingAssetsPath, DefaultPromptsFolder)
                : promptsRootPath;
        }

        /// <summary>Absolute root folder prompts are read from.</summary>
        public string PromptsRootPath => _promptsRootPath;

        /// <summary>
        /// Returns the raw (un-rendered) text of a prompt file, using a cache.
        /// <paramref name="fileName"/> is a name only (e.g. "apple_seller.txt");
        /// the ".txt" extension is added automatically when omitted.
        /// Returns an empty string when the file is missing.
        /// </summary>
        public string GetRawPrompt(string fileName, string subFolder)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                Debug.LogError($"[PromptManager] Empty prompt file name requested (subFolder='{subFolder}').");
                return string.Empty;
            }

            string normalized = NormalizeFileName(fileName);
            string cacheKey = $"{subFolder}/{normalized}";
            if (_rawCache.TryGetValue(cacheKey, out string cached))
            {
                return cached;
            }

            string path = Path.Combine(_promptsRootPath, subFolder ?? string.Empty, normalized);
            if (!File.Exists(path))
            {
                Debug.LogError($"[PromptManager] Prompt file not found: {path}");
                return string.Empty;
            }

            string text;
            try
            {
                text = File.ReadAllText(path, System.Text.Encoding.UTF8);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[PromptManager] Failed to read prompt '{path}': {e.Message}");
                return string.Empty;
            }

            _rawCache[cacheKey] = text;
            return text;
        }

        /// <summary>
        /// Loads a prompt file and renders its <c>{{variables}}</c> with the given context.
        /// </summary>
        public string GetRendered(string fileName, string subFolder, PromptVariableContext context)
        {
            string raw = GetRawPrompt(fileName, subFolder);
            if (string.IsNullOrEmpty(raw))
            {
                return raw;
            }

            return PromptTemplateRenderer.Render(raw, context, $"{subFolder}/{NormalizeFileName(fileName)}");
        }

        /// <summary>Convenience overload for NPC prompts.</summary>
        public string GetRenderedNpcPrompt(string fileName, PromptVariableContext context)
        {
            return GetRendered(fileName, SubFolder.Npc, context);
        }

        /// <summary>Convenience overload for quest evaluation prompts.</summary>
        public string GetRenderedQuestEvalPrompt(string fileName, PromptVariableContext context)
        {
            return GetRendered(fileName, SubFolder.QuestEval, context);
        }

        /// <summary>Clears the raw text cache (e.g. for an editor "Reload Config" action).</summary>
        public void ClearCache()
        {
            _rawCache.Clear();
        }

        private static string NormalizeFileName(string fileName)
        {
            fileName = fileName.Trim();
            if (!fileName.EndsWith(".txt", System.StringComparison.OrdinalIgnoreCase))
            {
                fileName += ".txt";
            }

            return fileName;
        }
    }
}
