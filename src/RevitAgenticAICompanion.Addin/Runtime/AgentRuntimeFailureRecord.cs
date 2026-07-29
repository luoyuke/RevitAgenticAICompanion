using System;

namespace RevitAgenticAICompanion.Runtime
{
    public sealed class AgentRuntimeFailureRecord
    {
        public AgentRuntimeFailureRecord(
            string stage,
            string message,
            string detail,
            string executablePath,
            string cliVersion,
            string configPath,
            string configModel,
            string configReasoningEffort,
            string command,
            int? exitCode,
            string stdoutSummary,
            string stderrSummary,
            RuntimeInvocationSummary runtimeInvocationSummary = null)
        {
            EventId = Guid.NewGuid().ToString("N");
            OccurredUtc = DateTimeOffset.UtcNow;
            Stage = stage ?? string.Empty;
            Message = message ?? string.Empty;
            Detail = detail ?? string.Empty;
            ExecutablePath = executablePath ?? string.Empty;
            CliVersion = cliVersion ?? string.Empty;
            ConfigPath = configPath ?? string.Empty;
            ConfigModel = configModel ?? string.Empty;
            ConfigReasoningEffort = configReasoningEffort ?? string.Empty;
            Command = command ?? string.Empty;
            ExitCode = exitCode;
            StdoutSummary = stdoutSummary ?? string.Empty;
            StderrSummary = stderrSummary ?? string.Empty;
            RuntimeInvocationSummary = runtimeInvocationSummary;
            ArtifactDirectory = string.Empty;
        }

        public string EventId { get; }
        public DateTimeOffset OccurredUtc { get; }
        public string Stage { get; }
        public string Message { get; }
        public string Detail { get; }
        public string ExecutablePath { get; }
        public string CliVersion { get; }
        public string ConfigPath { get; }
        public string ConfigModel { get; }
        public string ConfigReasoningEffort { get; }
        public string Command { get; }
        public int? ExitCode { get; }
        public string StdoutSummary { get; }
        public string StderrSummary { get; }
        public RuntimeInvocationSummary RuntimeInvocationSummary { get; }
        public string ArtifactDirectory { get; set; }
    }
}
