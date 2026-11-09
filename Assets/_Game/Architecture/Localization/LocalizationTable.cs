using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Game.Localization
{
    /// <summary>Both sources produce the same immutable lookup table. Empty cells are untranslated.</summary>
    public sealed class LocalizationTable
    {
        private const int MinColumns = 2;

        private readonly Dictionary<string, Dictionary<string, string>> _languages =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        public bool TryGet(string key, string language, out string value)
        {
            value = null;
            return language != null && _languages.TryGetValue(language, out var entries)
                && entries.TryGetValue(key, out value);
        }

        public static LocalizationTable FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new FormatException("Localization JSON is empty.");

            JsonDocument document;
            try
            {
                document = JsonUtility.FromJson<JsonDocument>(json.TrimStart('\uFEFF'));
            }
            catch (ArgumentException exception)
            {
                throw new FormatException("Invalid localization JSON.", exception);
            }
            if (document?.languages == null || document.languages.Length == 0)
                throw new FormatException("Localization JSON needs a languages array.");

            var table = new LocalizationTable();
            foreach (var language in document.languages)
            {
                if (language == null || string.IsNullOrWhiteSpace(language.code) || language.entries == null)
                    throw new FormatException("Each JSON language needs a code and entries.");

                var entries = table.AddLanguage(language.code.Trim());
                var keys = new HashSet<string>(StringComparer.Ordinal);
                foreach (var entry in language.entries)
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.key))
                        throw new FormatException("A localization key is empty.");

                    var key = entry.key.Trim();
                    if (!keys.Add(key))
                        throw new FormatException($"Duplicate localization key: {key}.");

                    if (!string.IsNullOrWhiteSpace(entry.value))
                        entries.Add(key, entry.value);
                }
            }
            table.RequireTranslations();
            return table;
        }

        public static LocalizationTable FromCsv(string csv)
        {
            var rows = ReadCsv(csv);
            if (rows.Count == 0 || rows[0].Length < MinColumns ||
                !string.Equals(rows[0][0].Trim(), "key", StringComparison.OrdinalIgnoreCase))
                throw new FormatException("Localization CSV must start with key and language columns.");

            var table = new LocalizationTable();
            var header = rows[0];
            for (var column = 1; column < header.Length; column++)
            {
                header[column] = header[column].Trim();
                table.AddLanguage(header[column]);
            }

            var keys = new HashSet<string>(StringComparer.Ordinal);
            for (var rowIndex = 1; rowIndex < rows.Count; rowIndex++)
            {
                var row = rows[rowIndex];
                var key = row[0].Trim();
                if (string.IsNullOrWhiteSpace(key) || row.Length > header.Length)
                    throw new FormatException($"Invalid localization CSV row {rowIndex + 1}.");

                if (!keys.Add(key))
                    throw new FormatException($"Duplicate localization key: {key}.");

                for (var column = 1; column < row.Length; column++)
                    if (!string.IsNullOrWhiteSpace(row[column]))
                        table._languages[header[column]].Add(key, row[column]);
            }
            table.RequireTranslations();
            return table;
        }

        private Dictionary<string, string> AddLanguage(string code)
        {
            if (string.IsNullOrWhiteSpace(code) || _languages.ContainsKey(code))
                throw new FormatException($"Empty or duplicate localization language: {code}.");

            var entries = new Dictionary<string, string>(StringComparer.Ordinal);
            _languages.Add(code, entries);
            return entries;
        }

        private void RequireTranslations()
        {
            foreach (var entries in _languages.Values)
                if (entries.Count > 0)
                    return;

            throw new FormatException("Localization data has no translations.");
        }

        private static List<string[]> ReadCsv(string csv)
        {
            if (string.IsNullOrWhiteSpace(csv))
                throw new FormatException("Localization CSV is empty.");

            var rows = new List<string[]>();
            var cells = new List<string>();
            var value = new StringBuilder();
            var quoted = false;
            var closedQuote = false;
            csv = csv.TrimStart('\uFEFF');
            for (var index = 0; index < csv.Length; index++)
            {
                var character = csv[index];
                if (quoted)
                {
                    if (character != '"')
                        value.Append(character);
                    else if (index + 1 < csv.Length && csv[index + 1] == '"')
                    {
                        value.Append('"');
                        index++;
                    }
                    else
                    {
                        quoted = false;
                        closedQuote = true;
                    }
                    continue;
                }

                if (character == ',' || character == '\r' || character == '\n')
                {
                    cells.Add(value.ToString());
                    value.Clear();
                    closedQuote = false;
                    if (character == ',')
                        continue;

                    if (character == '\r' && index + 1 < csv.Length && csv[index + 1] == '\n')
                        index++;
                    AddRow(rows, cells);
                }
                else if (character == '"' && value.Length == 0 && !closedQuote)
                    quoted = true;
                else
                {
                    if (closedQuote || character == '"')
                        throw new FormatException("Invalid quote in localization CSV.");

                    value.Append(character);
                }
            }
            if (quoted)
                throw new FormatException("Unclosed quote in localization CSV.");

            cells.Add(value.ToString());
            AddRow(rows, cells);
            return rows;
        }

        private static void AddRow(List<string[]> rows, List<string> cells)
        {
            if (cells.Exists(cell => !string.IsNullOrWhiteSpace(cell)))
                rows.Add(cells.ToArray());
            cells.Clear();
        }

        [Serializable] private sealed class JsonDocument { public JsonLanguage[] languages; }
        [Serializable] private sealed class JsonLanguage { public string code; public JsonEntry[] entries; }
        [Serializable] private sealed class JsonEntry { public string key; public string value; }
    }
}
