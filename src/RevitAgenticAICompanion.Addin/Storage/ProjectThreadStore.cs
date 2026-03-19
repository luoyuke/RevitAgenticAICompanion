using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

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
            lock (_gate)
            {
                EnsureLoaded();
                if (string.IsNullOrWhiteSpace(projectKey))
                {
                    return string.Empty;
                }

                return _entries.TryGetValue(projectKey, out var threadId) ? threadId : string.Empty;
            }
        }

        public void SetThreadId(string projectKey, string threadId)
        {
            if (string.IsNullOrWhiteSpace(projectKey) || string.IsNullOrWhiteSpace(threadId))
            {
                return;
            }

            lock (_gate)
            {
                EnsureLoaded();
                _entries[projectKey] = threadId;
                Save();
            }
        }

        public void ClearThreadId(string projectKey)
        {
            if (string.IsNullOrWhiteSpace(projectKey))
            {
                return;
            }

            lock (_gate)
            {
                EnsureLoaded();
                if (_entries.Remove(projectKey))
                {
                    Save();
                }
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
                _entries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                return;
            }

            try
            {
                _entries = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(_path))
                    ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                _entries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path) ?? string.Empty);
            File.WriteAllText(_path, JsonSerializer.Serialize(_entries, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
