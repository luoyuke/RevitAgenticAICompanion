using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using RevitAgenticAICompanion.Runtime;

namespace RevitAgenticAICompanion.Storage
{
    public sealed class UserMemoryStore
    {
        private static readonly string[] OrderedKeys =
        {
            "preferred_language",
            "explanation_style",
            "approval_style",
            "inspection_bias",
        };

        private static readonly HashSet<string> AllowedKeys = new HashSet<string>(OrderedKeys, StringComparer.OrdinalIgnoreCase);
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);
        private readonly string _path;
        private readonly object _gate;
        private Dictionary<string, UserPreferenceRecord> _entries;

        public UserMemoryStore(LocalStoragePaths paths)
        {
            _path = paths.UserMemoryPath;
            _gate = new object();
        }

        public IReadOnlyList<UserPreferenceRecord> GetPreferences()
        {
            lock (_gate)
            {
                EnsureLoaded();
                return OrderedKeys
                    .Select(key => _entries.TryGetValue(key, out var record) ? record : null)
                    .Where(record => record != null)
                    .ToArray();
            }
        }

        public IReadOnlyList<string> GetAllowedKeys()
        {
            lock (_gate)
            {
                return OrderedKeys.ToArray();
            }
        }

        public bool TrySetPreference(string key, string value, out UserPreferenceRecord record, out string error)
        {
            lock (_gate)
            {
                EnsureLoaded();

                var normalizedKey = NormalizeKey(key);
                if (!AllowedKeys.Contains(normalizedKey))
                {
                    record = null;
                    error = "Unknown memory key: " + (key ?? string.Empty) + ".";
                    return false;
                }

                var normalizedValue = NormalizeValue(normalizedKey, value);
                if (string.IsNullOrWhiteSpace(normalizedValue))
                {
                    record = null;
                    error = "Memory value cannot be empty.";
                    return false;
                }

                record = new UserPreferenceRecord(
                    normalizedKey,
                    normalizedValue,
                    "high",
                    "explicit slash command",
                    DateTimeOffset.UtcNow);

                _entries[normalizedKey] = record;
                Save();
                error = string.Empty;
                return true;
            }
        }

        public bool TryClearPreference(string key, out bool removed, out string error)
        {
            lock (_gate)
            {
                EnsureLoaded();

                var normalizedKey = NormalizeKey(key);
                if (!AllowedKeys.Contains(normalizedKey))
                {
                    removed = false;
                    error = "Unknown memory key: " + (key ?? string.Empty) + ".";
                    return false;
                }

                removed = _entries.Remove(normalizedKey);
                if (removed)
                {
                    Save();
                }

                error = string.Empty;
                return true;
            }
        }

        private void EnsureLoaded()
        {
            if (_entries != null)
            {
                return;
            }

            _entries = new Dictionary<string, UserPreferenceRecord>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(_path))
            {
                return;
            }

            UserPreferenceRecord current = null;
            foreach (var rawLine in File.ReadAllLines(_path))
            {
                var line = rawLine?.Trim() ?? string.Empty;
                if (line.StartsWith("### ", StringComparison.Ordinal))
                {
                    var key = NormalizeKey(line.Substring(4).Trim());
                    current = AllowedKeys.Contains(key)
                        ? new UserPreferenceRecord(key, string.Empty, string.Empty, string.Empty, DateTimeOffset.MinValue)
                        : null;
                    if (current != null)
                    {
                        _entries[key] = current;
                    }

                    continue;
                }

                if (current == null || !IsPreferenceFieldLine(line))
                {
                    continue;
                }

                var separatorIndex = line.IndexOf(':');
                if (separatorIndex <= 2)
                {
                    continue;
                }

                var field = NormalizeKey(line.Substring(2, separatorIndex - 2).Trim());
                var value = line.Substring(separatorIndex + 1).Trim();
                current = UpdateField(current, field, value);
                _entries[current.Key] = current;
            }

            _entries = _entries
                .Where(pair => !string.IsNullOrWhiteSpace(pair.Value.Value))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        }

        private static bool IsPreferenceFieldLine(string line)
        {
            return line.StartsWith("- ", StringComparison.Ordinal) ||
                line.StartsWith("* ", StringComparison.Ordinal);
        }

        private static string NormalizeKey(string key)
        {
            return (key ?? string.Empty)
                .Trim()
                .Replace("\\_", "_");
        }

        private static string NormalizeValue(string key, string value)
        {
            var normalizedValue = (value ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedValue))
            {
                return null;
            }

            switch (NormalizeKey(key))
            {
                case "preferred_language":
                    if (string.Equals(normalizedValue, "english", StringComparison.OrdinalIgnoreCase))
                    {
                        return "English";
                    }

                    if (string.Equals(normalizedValue, "french", StringComparison.OrdinalIgnoreCase))
                    {
                        return "French";
                    }

                    if (string.Equals(normalizedValue, "german", StringComparison.OrdinalIgnoreCase))
                    {
                        return "German";
                    }

                    if (string.Equals(normalizedValue, "chinese", StringComparison.OrdinalIgnoreCase))
                    {
                        return "Chinese";
                    }

                    return normalizedValue;
                case "explanation_style":
                case "approval_style":
                case "inspection_bias":
                    return normalizedValue.ToLowerInvariant();
                default:
                    return normalizedValue;
            }
        }

        private static UserPreferenceRecord UpdateField(UserPreferenceRecord record, string field, string value)
        {
            switch ((field ?? string.Empty).ToLowerInvariant())
            {
                case "value":
                    return new UserPreferenceRecord(record.Key, value, record.ConfidenceLevel, record.Source, record.LastUpdatedUtc);
                case "confidence":
                    return new UserPreferenceRecord(record.Key, record.Value, value, record.Source, record.LastUpdatedUtc);
                case "source":
                    return new UserPreferenceRecord(record.Key, record.Value, record.ConfidenceLevel, value, record.LastUpdatedUtc);
                case "last_updated_utc":
                    if (DateTimeOffset.TryParse(value, out var parsed))
                    {
                        return new UserPreferenceRecord(record.Key, record.Value, record.ConfidenceLevel, record.Source, parsed);
                    }

                    return record;
                default:
                    return record;
            }
        }

        private void Save()
        {
            if (_entries == null)
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(_path) ?? string.Empty);

            var builder = new StringBuilder();
            builder.AppendLine("# Revit Agentic AI Companion User Memory");
            builder.AppendLine();
            builder.AppendLine("## Rules");
            builder.AppendLine("- Cross-project user preferences only");
            builder.AppendLine("- No project-specific conventions");
            builder.AppendLine("- No audit history");
            builder.AppendLine("- No session transcript");
            builder.AppendLine("- Read automatically on every prompt");
            builder.AppendLine("- Update only with explicit /memory commands");
            builder.AppendLine("- When in doubt, do not write it");
            builder.AppendLine("- Delete or edit by hand if needed");
            builder.AppendLine();
            builder.AppendLine("## Preferences");
            builder.AppendLine();

            foreach (var key in OrderedKeys)
            {
                if (!_entries.TryGetValue(key, out var record) || string.IsNullOrWhiteSpace(record.Value))
                {
                    continue;
                }

                builder.AppendLine("### " + record.Key);
                builder.AppendLine("- value: " + record.Value);
                builder.AppendLine("- confidence: " + record.ConfidenceLevel);
                builder.AppendLine("- source: " + record.Source);
                builder.AppendLine("- last_updated_utc: " + record.LastUpdatedUtc.ToString("O"));
                builder.AppendLine();
            }

            File.WriteAllText(_path, builder.ToString(), Utf8NoBom);
        }
    }
}
