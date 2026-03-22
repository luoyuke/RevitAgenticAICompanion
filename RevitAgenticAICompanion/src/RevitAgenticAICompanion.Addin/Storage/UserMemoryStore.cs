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
            "routing_generation_mode",
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

        public void UpdateFromTurn(string userPrompt, ProposalCandidate proposal, ProposalExecutionResult execution)
        {
            lock (_gate)
            {
                EnsureLoaded();
                var updates = DetectPreferences(userPrompt);
                if (updates.Count == 0)
                {
                    return;
                }

                foreach (var update in updates)
                {
                    _entries[update.Key] = update;
                }

                Save();
            }
        }

        private static List<UserPreferenceRecord> DetectPreferences(string userPrompt)
        {
            var updates = new List<UserPreferenceRecord>();
            var text = (userPrompt ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text) || !HasMemoryIntent(text))
            {
                return updates;
            }

            AddIfDetected(updates, DetectPreferredLanguage(text));
            AddIfDetected(updates, DetectExplanationStyle(text));
            AddIfDetected(updates, DetectApprovalStyle(text));
            AddIfDetected(updates, DetectInspectionBias(text));
            AddIfDetected(updates, DetectRoutingGenerationMode(text));
            return updates;
        }

        private static void AddIfDetected(List<UserPreferenceRecord> updates, UserPreferenceRecord candidate)
        {
            if (candidate != null)
            {
                updates.Add(candidate);
            }
        }

        private static UserPreferenceRecord DetectPreferredLanguage(string text)
        {
            if (!ContainsAny(text, "language", "reply", "respond", "answer", "write", "speak"))
            {
                return null;
            }

            if (ContainsAny(text, "french", "francais", "français"))
            {
                return CreateRecord("preferred_language", "French");
            }

            if (ContainsAny(text, "english"))
            {
                return CreateRecord("preferred_language", "English");
            }

            if (ContainsAny(text, "german", "deutsch"))
            {
                return CreateRecord("preferred_language", "German");
            }

            if (ContainsAny(text, "chinese", "中文"))
            {
                return CreateRecord("preferred_language", "Chinese");
            }

            return null;
        }

        private static UserPreferenceRecord DetectExplanationStyle(string text)
        {
            if (!ContainsAny(text, "explain", "explanation", "reply", "response", "style", "concise", "brief", "detailed", "detail"))
            {
                return null;
            }

            if (ContainsAny(text, "concise", "brief", "short"))
            {
                return CreateRecord("explanation_style", "concise");
            }

            if (ContainsAny(text, "detailed", "more detail", "thorough", "go deeper"))
            {
                return CreateRecord("explanation_style", "detailed");
            }

            return null;
        }

        private static UserPreferenceRecord DetectApprovalStyle(string text)
        {
            if (!ContainsAny(text, "approval", "approve", "confirm", "write", "execute"))
            {
                return null;
            }

            if (ContainsAny(text, "explicit approval", "ask before write", "approval before write", "ask before executing", "confirm before write", "confirm before executing", "wait for approval"))
            {
                return CreateRecord("approval_style", "explicit before write");
            }

            return null;
        }

        private static UserPreferenceRecord DetectInspectionBias(string text)
        {
            if (!ContainsAny(text, "inspect", "guess", "ambiguous", "live project data", "evidence"))
            {
                return null;
            }

            if (ContainsAny(text, "inspect first", "inspection first", "do not guess", "don't guess", "inspect before write", "live project data", "evidence driven", "when ambiguous"))
            {
                return CreateRecord("inspection_bias", "inspect first when ambiguous");
            }

            return null;
        }

        private static UserPreferenceRecord DetectRoutingGenerationMode(string text)
        {
            if (!ContainsAny(text, "pipe", "pipework", "piping", "duct", "ductwork", "routing", "route", "layout"))
            {
                return null;
            }

            if (ContainsAny(text, "placeholder", "placeholders"))
            {
                return CreateRecord("routing_generation_mode", "placeholders_only");
            }

            return null;
        }

        private static bool HasMemoryIntent(string text)
        {
            if (ContainsAny(text, "remember", "store this", "save this", "save that", "keep this", "keep that", "by default", "default", "preference", "preferences"))
            {
                return true;
            }

            if (ContainsAny(text, "set my", "set ma", "set the") &&
                ContainsAny(text, "preference", "default"))
            {
                return true;
            }

            if (ContainsAny(text, "from now on") &&
                ContainsAny(text, "language", "reply", "approval", "inspect", "explain", "style", "pipe", "pipework", "duct", "ductwork", "routing", "placeholder"))
            {
                return true;
            }

            return false;
        }

        private static bool ContainsAny(string text, params string[] needles)
        {
            foreach (var needle in needles)
            {
                if (!string.IsNullOrWhiteSpace(needle) &&
                    text.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static UserPreferenceRecord CreateRecord(string key, string value)
        {
            if (!AllowedKeys.Contains(key) || string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return new UserPreferenceRecord(
                key,
                value,
                "high",
                "explicit user statement",
                DateTimeOffset.UtcNow);
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
            if (_entries == null || _entries.Count == 0)
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
            builder.AppendLine("- Evaluate for update after every completed reply");
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
