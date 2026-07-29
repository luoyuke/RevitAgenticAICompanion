using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using RevitAgenticAICompanion.Runtime;

namespace RevitAgenticAICompanion.Storage
{
    public sealed class ProjectThreadStore
    {
        private readonly string _path;
        private readonly object _gate;
        private Dictionary<string, string> _entries;

        public ProjectThreadStore(LocalStoragePaths paths)
        {
            _path = Path.Combine(paths.StatePath, "project-threads.json");
            _gate = new object();
        }

        public string GetThreadId(string projectKey)
        {
            return GetThreadId(AgentRuntimeProvider.Codex, projectKey);
        }

        public string GetThreadId(AgentRuntimeProvider provider, string projectKey)
        {
            lock (_gate)
            {
                EnsureLoaded();
                var key = BuildKey(provider, projectKey);
                if (string.IsNullOrWhiteSpace(key))
                {
                    return string.Empty;
                }

                return _entries.TryGetValue(key, out var threadId) ? threadId : string.Empty;
            }
        }

        public void SetThreadId(string projectKey, string threadId)
        {
            SetThreadId(AgentRuntimeProvider.Codex, projectKey, threadId);
        }

        public void SetThreadId(AgentRuntimeProvider provider, string projectKey, string threadId)
        {
            var key = BuildKey(provider, projectKey);
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(threadId))
            {
                return;
            }

            lock (_gate)
            {
                EnsureLoaded();
                _entries[key] = threadId;
                Save();
            }
        }

        public void ClearThreadId(string projectKey)
        {
            ClearThreadId(AgentRuntimeProvider.Codex, projectKey);
        }

        public void ClearThreadId(AgentRuntimeProvider provider, string projectKey)
        {
            var key = BuildKey(provider, projectKey);
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            lock (_gate)
            {
                EnsureLoaded();
                if (_entries.Remove(key))
                {
                    Save();
                }
            }
        }

        private static string BuildKey(AgentRuntimeProvider provider, string projectKey)
        {
            return string.IsNullOrWhiteSpace(projectKey)
                ? string.Empty
                : provider.ToString().ToLowerInvariant() + ":" + projectKey.Trim();
        }

        private void EnsureLoaded()
        {
            if (_entries != null)
            {
                return;
            }

            if (!File.Exists(_path))
            {
                _entries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                return;
            }

            try
            {
                _entries = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(_path))
                    ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                MigrateLegacyCodexKeys();
            }
            catch
            {
                _entries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private void MigrateLegacyCodexKeys()
        {
            var additions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in _entries)
            {
                if (entry.Key.IndexOf(":", StringComparison.Ordinal) >= 0)
                {
                    continue;
                }

                additions[BuildKey(AgentRuntimeProvider.Codex, entry.Key)] = entry.Value;
            }

            foreach (var addition in additions)
            {
                _entries[addition.Key] = addition.Value;
            }
        }

        private void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path) ?? string.Empty);
            File.WriteAllText(_path, JsonSerializer.Serialize(_entries, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
