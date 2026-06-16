using System.Collections.Generic;

namespace RevitAgenticAICompanion.Runtime
{
    public sealed class CodexExecutableResolution
    {
        public CodexExecutableResolution(
            string executablePath,
            string version,
            string source,
            IReadOnlyList<string> diagnostics)
        {
            ExecutablePath = executablePath ?? string.Empty;
            Version = version ?? string.Empty;
            Source = source ?? string.Empty;
            Diagnostics = diagnostics ?? new string[0];
        }

        public string ExecutablePath { get; }
        public string Version { get; }
        public string Source { get; }
        public IReadOnlyList<string> Diagnostics { get; }

        public string BuildSummary()
        {
            if (string.IsNullOrWhiteSpace(Source))
            {
                return string.Empty;
            }

            return Source;
        }
    }
}
