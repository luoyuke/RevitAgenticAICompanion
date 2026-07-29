using System;
using System.Collections.Generic;
using System.Linq;

namespace RevitAgenticAICompanion.Runtime
{
    public sealed class AgentRuntimeHealthReport
    {
        public AgentRuntimeHealthReport(
            bool isAvailable,
            bool isAuthenticated,
            bool hasBlockingIssue,
            string detail,
            string executablePath,
            string cliVersion,
            string configPath,
            string configModel,
            string configReasoningEffort,
            string modelCatalogPath,
            IReadOnlyList<string> availableModels,
            bool supportsModelOverride = false,
            bool supportsConfigOverride = false,
            string executableSource = "",
            IReadOnlyList<string> executableResolutionDiagnostics = null)
        {
            IsAvailable = isAvailable;
            IsAuthenticated = isAuthenticated;
            HasBlockingIssue = hasBlockingIssue;
            Detail = detail ?? string.Empty;
            ExecutablePath = executablePath ?? string.Empty;
            CliVersion = cliVersion ?? string.Empty;
            ConfigPath = configPath ?? string.Empty;
            ConfigModel = configModel ?? string.Empty;
            ConfigReasoningEffort = configReasoningEffort ?? string.Empty;
            ModelCatalogPath = modelCatalogPath ?? string.Empty;
            AvailableModels = availableModels ?? Array.Empty<string>();
            SupportsModelOverride = supportsModelOverride;
            SupportsConfigOverride = supportsConfigOverride;
            ExecutableSource = executableSource ?? string.Empty;
            ExecutableResolutionDiagnostics = executableResolutionDiagnostics ?? Array.Empty<string>();
        }

        public bool IsAvailable { get; }
        public bool IsAuthenticated { get; }
        public bool HasBlockingIssue { get; }
        public string Detail { get; }
        public string ExecutablePath { get; }
        public string CliVersion { get; }
        public string ConfigPath { get; }
        public string ConfigModel { get; }
        public string ConfigReasoningEffort { get; }
        public string ModelCatalogPath { get; }
        public IReadOnlyList<string> AvailableModels { get; }
        public bool SupportsModelOverride { get; }
        public bool SupportsConfigOverride { get; }
        public string ExecutableSource { get; }
        public IReadOnlyList<string> ExecutableResolutionDiagnostics { get; }

        public string BuildDiagnosticSummary()
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(CliVersion))
            {
                parts.Add("CLI: " + CliVersion);
            }

            if (!string.IsNullOrWhiteSpace(ExecutablePath))
            {
                parts.Add("Executable: " + ExecutablePath);
            }

            if (!string.IsNullOrWhiteSpace(ExecutableSource))
            {
                parts.Add("Resolver: " + ExecutableSource);
            }

            if (!string.IsNullOrWhiteSpace(ConfigModel))
            {
                parts.Add("Config model: " + ConfigModel);
            }

            if (!string.IsNullOrWhiteSpace(ConfigReasoningEffort))
            {
                parts.Add("Config reasoning: " + ConfigReasoningEffort);
            }

            if (AvailableModels != null && AvailableModels.Count > 0)
            {
                parts.Add("Known models: " + string.Join(", ", AvailableModels.Take(12)));
            }

            parts.Add("CLI overrides: model=" + SupportsModelOverride + ", config=" + SupportsConfigOverride);

            return string.Join(" | ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
        }
    }
}
