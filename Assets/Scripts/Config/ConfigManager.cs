using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using MultiAgentNPC.Quest;

namespace MultiAgentNPC.Config
{
    /// <summary>
    /// Loads and owns the planner-facing CSV configuration (NPCs and quests).
    /// Reads from <c>Application.streamingAssetsPath/Config</c> by default.
    ///
    /// This is a plain class (not a MonoBehaviour) so the same API can later be
    /// backed by ScriptableObjects or a remote config service. Loading is defensive:
    /// any failure logs a clear error/warning and leaves the manager in a usable,
    /// non-throwing state.
    /// </summary>
    public class ConfigManager
    {
        public const string DefaultConfigFolder = "Config";
        public const string NpcConfigFileName = "NPCConfig.csv";
        public const string QuestConfigFileName = "QuestConfig.csv";

        // Maximum quest bindings represented by dedicated CSV columns (Task1*, Task2*).
        // The in-memory model uses a list, so this is only about CSV column parsing.
        private const int CsvTaskBindingColumns = 2;

        private readonly string _configFolderPath;

        private readonly Dictionary<int, NPCConfig> _npcConfigs = new Dictionary<int, NPCConfig>();
        private readonly Dictionary<int, QuestConfig> _questConfigs = new Dictionary<int, QuestConfig>();

        public IReadOnlyDictionary<int, NPCConfig> NpcConfigs => _npcConfigs;
        public IReadOnlyDictionary<int, QuestConfig> QuestConfigs => _questConfigs;

        public bool NpcConfigLoaded { get; private set; }
        public bool QuestConfigLoaded { get; private set; }

        /// <summary>
        /// Creates a config manager. When <paramref name="configFolderPath"/> is null,
        /// defaults to <c>Application.streamingAssetsPath/Config</c>.
        /// </summary>
        public ConfigManager(string configFolderPath = null)
        {
            _configFolderPath = string.IsNullOrEmpty(configFolderPath)
                ? Path.Combine(Application.streamingAssetsPath, DefaultConfigFolder)
                : configFolderPath;
        }

        /// <summary>Absolute folder the CSV files are read from.</summary>
        public string ConfigFolderPath => _configFolderPath;

        /// <summary>Loads every config table. Returns true only if all tables loaded.</summary>
        public bool LoadAll()
        {
            bool npcOk = LoadNpcConfigs();
            bool questOk = LoadQuestConfigs();
            return npcOk && questOk;
        }

        public bool TryGetNpc(int npcId, out NPCConfig config) => _npcConfigs.TryGetValue(npcId, out config);

        public bool TryGetQuest(int questId, out QuestConfig config) => _questConfigs.TryGetValue(questId, out config);

        public bool LoadNpcConfigs()
        {
            _npcConfigs.Clear();
            NpcConfigLoaded = false;

            string path = Path.Combine(_configFolderPath, NpcConfigFileName);
            List<Dictionary<string, string>> rows = CsvTableLoader.LoadFromFile(path);
            if (rows.Count == 0)
            {
                Debug.LogError($"[ConfigManager] No NPC rows loaded from {path}.");
                return false;
            }

            int rowNumber = 1; // header is row 1; data starts at row 2
            foreach (Dictionary<string, string> row in rows)
            {
                rowNumber++;
                NPCConfig config = ParseNpcRow(row, rowNumber);
                if (config == null)
                {
                    continue;
                }

                if (_npcConfigs.ContainsKey(config.NpcId))
                {
                    Debug.LogError(
                        $"[ConfigManager] Duplicate NPCID {config.NpcId} at {NpcConfigFileName} row {rowNumber}. Keeping the first occurrence.");
                    continue;
                }

                _npcConfigs.Add(config.NpcId, config);
            }

            NpcConfigLoaded = _npcConfigs.Count > 0;
            return NpcConfigLoaded;
        }

        public bool LoadQuestConfigs()
        {
            _questConfigs.Clear();
            QuestConfigLoaded = false;

            string path = Path.Combine(_configFolderPath, QuestConfigFileName);
            List<Dictionary<string, string>> rows = CsvTableLoader.LoadFromFile(path);
            if (rows.Count == 0)
            {
                Debug.LogError($"[ConfigManager] No Quest rows loaded from {path}.");
                return false;
            }

            int rowNumber = 1;
            foreach (Dictionary<string, string> row in rows)
            {
                rowNumber++;
                QuestConfig config = ParseQuestRow(row, rowNumber);
                if (config == null)
                {
                    continue;
                }

                if (_questConfigs.ContainsKey(config.QuestId))
                {
                    Debug.LogError(
                        $"[ConfigManager] Duplicate QuestID {config.QuestId} at {QuestConfigFileName} row {rowNumber}. Keeping the first occurrence.");
                    continue;
                }

                _questConfigs.Add(config.QuestId, config);
            }

            QuestConfigLoaded = _questConfigs.Count > 0;
            return QuestConfigLoaded;
        }

        private NPCConfig ParseNpcRow(Dictionary<string, string> row, int rowNumber)
        {
            if (!TryGetRequiredInt(row, "NPCID", NpcConfigFileName, rowNumber, out int npcId))
            {
                return null;
            }

            string npcName = GetString(row, "NPCName");
            if (string.IsNullOrWhiteSpace(npcName))
            {
                Debug.LogWarning(
                    $"[ConfigManager] Missing NPCName for NPCID {npcId} ({NpcConfigFileName} row {rowNumber}).");
            }

            var config = new NPCConfig
            {
                NpcId = npcId,
                NpcName = npcName,
                TtsVoiceId = GetString(row, "TTSVoiceID"),
                DefaultPrompt = GetString(row, "DefaultPrompt"),
                ProximityPromptText = GetString(row, "ProximityPromptText"),
                ProximityPromptAudio = GetString(row, "ProximityPromptAudio"),
                InteractionRadius = GetFloat(row, "InteractionRadius", 0f)
            };

            if (string.IsNullOrWhiteSpace(config.DefaultPrompt))
            {
                Debug.LogWarning(
                    $"[ConfigManager] Missing DefaultPrompt for NPCID {npcId} ({NpcConfigFileName} row {rowNumber}).");
            }

            for (int i = 1; i <= CsvTaskBindingColumns; i++)
            {
                int taskId = GetInt(row, $"TaskID{i}", 0);
                if (taskId == 0)
                {
                    continue; // 0 == no binding
                }

                config.TaskBindings.Add(new NPCPromptBinding(
                    taskId,
                    GetString(row, $"Task{i}InactivePrompt"),
                    GetString(row, $"Task{i}ActivePrompt"),
                    GetString(row, $"Task{i}CompletedPrompt")));
            }

            return config;
        }

        private QuestConfig ParseQuestRow(Dictionary<string, string> row, int rowNumber)
        {
            if (!TryGetRequiredInt(row, "QuestID", QuestConfigFileName, rowNumber, out int questId))
            {
                return null;
            }

            var config = new QuestConfig
            {
                QuestId = questId,
                NextQuestId = GetInt(row, "NextQuestID", 0),
                QuestName = GetString(row, "QuestName"),
                QuestDescription = GetString(row, "QuestDescription"),
                QuestType = QuestTypeExtensions.ParseQuestType(
                    GetString(row, "QuestType"), $"{QuestConfigFileName} row {rowNumber}, QuestID {questId}"),
                TargetNpcId = GetInt(row, "TargetNPCID", 0),
                QuestParam = GetString(row, "QuestParam"),
                ShowInUI = GetBool(row, "ShowInUI", true),
                ParentQuestId = GetInt(row, "ParentQuestID", 0),
                SortOrder = GetInt(row, "SortOrder", 0)
            };

            if (string.IsNullOrWhiteSpace(config.QuestName))
            {
                Debug.LogWarning(
                    $"[ConfigManager] Missing QuestName for QuestID {questId} ({QuestConfigFileName} row {rowNumber}).");
            }

            return config;
        }

        private static string GetString(Dictionary<string, string> row, string key)
        {
            return row.TryGetValue(key, out string value) ? value?.Trim() : string.Empty;
        }

        private static bool TryGetRequiredInt(
            Dictionary<string, string> row, string key, string file, int rowNumber, out int value)
        {
            value = 0;
            if (!row.TryGetValue(key, out string raw) || string.IsNullOrWhiteSpace(raw))
            {
                Debug.LogError($"[ConfigManager] Missing required field '{key}' at {file} row {rowNumber}.");
                return false;
            }

            if (!int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                Debug.LogError(
                    $"[ConfigManager] Field '{key}'='{raw}' is not a valid integer at {file} row {rowNumber}.");
                return false;
            }

            return true;
        }

        private static int GetInt(Dictionary<string, string> row, string key, int fallback)
        {
            if (row.TryGetValue(key, out string raw) && !string.IsNullOrWhiteSpace(raw) &&
                int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
            {
                return value;
            }

            return fallback;
        }

        private static float GetFloat(Dictionary<string, string> row, string key, float fallback)
        {
            if (row.TryGetValue(key, out string raw) && !string.IsNullOrWhiteSpace(raw) &&
                float.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
            {
                return value;
            }

            return fallback;
        }

        private static bool GetBool(Dictionary<string, string> row, string key, bool fallback)
        {
            if (!row.TryGetValue(key, out string raw) || string.IsNullOrWhiteSpace(raw))
            {
                return fallback;
            }

            raw = raw.Trim();
            if (bool.TryParse(raw, out bool parsed))
            {
                return parsed;
            }

            // Accept common CSV truthy/falsy spellings.
            switch (raw.ToLowerInvariant())
            {
                case "1":
                case "yes":
                case "y":
                    return true;
                case "0":
                case "no":
                case "n":
                    return false;
                default:
                    return fallback;
            }
        }
    }
}
