using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using RevitAgenticAICompanion.Runtime;

namespace RevitAgenticAICompanion.Storage
{
    public sealed class ProjectMemoryStore
    {
        private readonly string _path;
        private readonly object _gate;
        private Dictionary<string, List<ProjectConventionEntry>> _entries;

        public ProjectMemoryStore(LocalStoragePaths paths)
        {
            _path = Path.Combine(paths.StatePath, "project-memory.json");
            _gate = new object();
        }

        public IReadOnlyList<ProjectConventionRecord> GetConventions(RevitContextSnapshot snapshot)
        {
            var projectKey = ProjectKeyBuilder.FromSnapshot(snapshot);
            lock (_gate)
            {
                EnsureLoaded();
                if (!_entries.TryGetValue(projectKey, out var conventions))
                {
                    return Array.Empty<ProjectConventionRecord>();
                }

                return conventions
                    .OrderByDescending(entry => entry.LastObservedUtc)
                    .Select(ToRecord)
                    .ToArray();
            }
        }

        public void UpsertConventions(
            RevitContextSnapshot snapshot,
            IEnumerable<ProjectConventionRecord> conventions,
            string sourceProposalId)
        {
            if (conventions == null)
            {
                return;
            }

            var projectKey = ProjectKeyBuilder.FromSnapshot(snapshot);
            lock (_gate)
            {
                EnsureLoaded();
                if (!_entries.TryGetValue(projectKey, out var bucket))
                {
                    bucket = new List<ProjectConventionEntry>();
                    _entries[projectKey] = bucket;
                }

                var now = DateTime.UtcNow.ToString("O");
                foreach (var convention in conventions)
                {
                    if (convention == null
                        || string.IsNullOrWhiteSpace(convention.ConventionType)
                        || string.IsNullOrWhiteSpace(convention.Value))
                    {
                        continue;
                    }

                    var existing = bucket.FirstOrDefault(entry =>
                        string.Equals(entry.ConventionType, convention.ConventionType, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(entry.Name, convention.Name, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(entry.Value, convention.Value, StringComparison.OrdinalIgnoreCase));

                    if (existing == null)
                    {
                        bucket.Add(new ProjectConventionEntry
                        {
                            ConventionType = convention.ConventionType,
                            Name = convention.Name,
                            Value = convention.Value,
                            ConfidenceLevel = convention.ConfidenceLevel,
                            Rationale = convention.Rationale,
                            Source = string.IsNullOrWhiteSpace(convention.Source) ? sourceProposalId : convention.Source,
                            LastObservedUtc = now,
                        });
                    }
                    else
                    {
                        existing.ConfidenceLevel = convention.ConfidenceLevel;
                        existing.Rationale = convention.Rationale;
                        existing.Source = string.IsNullOrWhiteSpace(convention.Source) ? sourceProposalId : convention.Source;
                        existing.LastObservedUtc = now;
                    }
                }

                Save();
            }
        }

        private void EnsureLoaded()
        {
            if (_entries != null)
            {
                return;
            }

            if (!File.Exists(_path))
            {
                _entries = new Dictionary<string, List<ProjectConventionEntry>>(StringComparer.OrdinalIgnoreCase);
                return;
            }

            try
            {
                _entries = JsonSerializer.Deserialize<Dictionary<string, List<ProjectConventionEntry>>>(File.ReadAllText(_path))
                    ?? new Dictionary<string, List<ProjectConventionEntry>>(StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                _entries = new Dictionary<string, List<ProjectConventionEntry>>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path) ?? string.Empty);
            File.WriteAllText(_path, JsonSerializer.Serialize(_entries, new JsonSerializerOptions { WriteIndented = true }));
        }

        private static ProjectConventionRecord ToRecord(ProjectConventionEntry entry)
        {
            return new ProjectConventionRecord(
                entry.ConventionType,
                entry.Name,
                entry.Value,
                entry.ConfidenceLevel,
                entry.Rationale,
                entry.Source);
        }

        private sealed class ProjectConventionEntry
        {
            public string ConventionType { get; set; }
            public string Name { get; set; }
            public string Value { get; set; }
            public string ConfidenceLevel { get; set; }
            public string Rationale { get; set; }
            public string Source { get; set; }
            public string LastObservedUtc { get; set; }
        }
    }
}
