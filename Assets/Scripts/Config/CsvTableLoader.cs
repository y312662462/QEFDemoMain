using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace MultiAgentNPC.Config
{
    /// <summary>
    /// Generic CSV reader for the planner-facing config input layer.
    /// Row 1 is treated as the header; every following row becomes a
    /// dictionary keyed by column name. Handles quoted fields, embedded
    /// commas/newlines, escaped double-quotes ("") and CRLF/LF line endings.
    ///
    /// This type only knows about parsing, not about file location, so it can
    /// later be reused for ScriptableObject export or remote config.
    /// </summary>
    public static class CsvTableLoader
    {
        /// <summary>
        /// Loads a CSV file from an absolute path and returns one dictionary per data row.
        /// Returns an empty list (never null) when the file is missing or malformed.
        /// </summary>
        public static List<Dictionary<string, string>> LoadFromFile(string absolutePath)
        {
            var rows = new List<Dictionary<string, string>>();

            if (string.IsNullOrEmpty(absolutePath))
            {
                Debug.LogError("[CsvTableLoader] Empty path passed to LoadFromFile.");
                return rows;
            }

            if (!File.Exists(absolutePath))
            {
                Debug.LogError($"[CsvTableLoader] CSV file not found: {absolutePath}");
                return rows;
            }

            string text;
            try
            {
                text = File.ReadAllText(absolutePath, Encoding.UTF8);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[CsvTableLoader] Failed to read CSV '{absolutePath}': {e.Message}");
                return rows;
            }

            return Parse(text, absolutePath);
        }

        /// <summary>
        /// Parses raw CSV text. <paramref name="sourceName"/> is used only for log messages.
        /// </summary>
        public static List<Dictionary<string, string>> Parse(string text, string sourceName = "<memory>")
        {
            var rows = new List<Dictionary<string, string>>();
            if (string.IsNullOrEmpty(text))
            {
                Debug.LogError($"[CsvTableLoader] CSV content is empty: {sourceName}");
                return rows;
            }

            // Strip a leading UTF-8 BOM if present.
            if (text.Length > 0 && text[0] == '\uFEFF')
            {
                text = text.Substring(1);
            }

            List<List<string>> records = SplitRecords(text);
            if (records.Count == 0)
            {
                Debug.LogError($"[CsvTableLoader] No rows found in CSV: {sourceName}");
                return rows;
            }

            List<string> header = records[0];
            if (header.Count == 0 || IsEmptyRecord(header))
            {
                Debug.LogError($"[CsvTableLoader] CSV header row is empty: {sourceName}");
                return rows;
            }

            for (int i = 1; i < records.Count; i++)
            {
                List<string> record = records[i];

                // Skip fully blank lines (common at end of file).
                if (IsEmptyRecord(record))
                {
                    continue;
                }

                var map = new Dictionary<string, string>(header.Count);
                for (int c = 0; c < header.Count; c++)
                {
                    string key = header[c].Trim();
                    if (string.IsNullOrEmpty(key))
                    {
                        continue;
                    }

                    string value = c < record.Count ? record[c] : string.Empty;
                    map[key] = value;
                }

                rows.Add(map);
            }

            return rows;
        }

        private static bool IsEmptyRecord(List<string> record)
        {
            for (int i = 0; i < record.Count; i++)
            {
                if (!string.IsNullOrEmpty(record[i]) && !string.IsNullOrWhiteSpace(record[i]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Tokenizes CSV text into records of fields, respecting RFC-4180 style quoting.
        /// </summary>
        private static List<List<string>> SplitRecords(string text)
        {
            var records = new List<List<string>>();
            var current = new List<string>();
            var field = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];

                if (inQuotes)
                {
                    if (ch == '"')
                    {
                        // Escaped quote ("") inside a quoted field.
                        if (i + 1 < text.Length && text[i + 1] == '"')
                        {
                            field.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        field.Append(ch);
                    }

                    continue;
                }

                switch (ch)
                {
                    case '"':
                        inQuotes = true;
                        break;
                    case ',':
                        current.Add(field.ToString());
                        field.Clear();
                        break;
                    case '\r':
                        // Swallow CR; the following LF (if any) closes the record.
                        break;
                    case '\n':
                        current.Add(field.ToString());
                        field.Clear();
                        records.Add(current);
                        current = new List<string>();
                        break;
                    default:
                        field.Append(ch);
                        break;
                }
            }

            // Flush trailing field/record (file without final newline).
            if (field.Length > 0 || current.Count > 0)
            {
                current.Add(field.ToString());
                records.Add(current);
            }

            return records;
        }
    }
}
