using System;
using System.IO;
using System.Text.Json;
using RevitAgenticAICompanion.Runtime;
namespace RevitAgenticAICompanion.Storage
{
    public sealed class RuntimeProviderSettings
    {
        private readonly string _path;
        private readonly object _gate;
        public RuntimeProviderSettings(LocalStoragePaths paths)
        {
            _path = Path.Combine(paths.StatePath, "runtime-settings.json");
            _gate = new object();
        }
        public AgentRuntimeProvider GetProvider()
        {
            lock (_gate)
            {
                try
                {
                    if (!File.Exists(_path))
                    {
                        return AgentRuntimeProvider.Codex;
                    }
                    using (var document = JsonDocument.Parse(File.ReadAllText(_path)))
                    {
                        if (document.RootElement.TryGetProperty("provider", out var providerElement) &&
                            Enum.TryParse(providerElement.GetString(), ignoreCase: true, out AgentRuntimeProvider provider))
                        {
                            return provider;
                        }
                    }
                }
                catch
                {
                }
                return AgentRuntimeProvider.Codex;
            }
        }
        public void SetProvider(AgentRuntimeProvider provider)
        {
            lock (_gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path) ?? string.Empty);
                var payload = JsonSerializer.Serialize(
                    new RuntimeSettingsPayload { Provider = provider.ToString() },
                    new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_path, payload);
            }
        }
        private sealed class RuntimeSettingsPayload
        {
            public string Provider { get; set; }
        }
    }
}