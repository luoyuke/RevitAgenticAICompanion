namespace RevitAgenticAICompanion.Runtime
{
    public sealed class RuntimeInvocationOptions
    {
        public static readonly RuntimeInvocationOptions Default = new RuntimeInvocationOptions(RuntimeProfile.Balanced);

        public RuntimeInvocationOptions(RuntimeProfile profile)
        {
            Profile = profile;
        }

        public RuntimeProfile Profile { get; }

        public string DisplayName
        {
            get
            {
                switch (Profile)
                {
                    case RuntimeProfile.CodexDefault:
                        return "Codex default";
                    case RuntimeProfile.Fast:
                        return "Fast";
                    case RuntimeProfile.Deep:
                        return "Deep";
                    case RuntimeProfile.Balanced:
                    default:
                        return "Balanced";
                }
            }
        }

        public string RequestedReasoningEffort
        {
            get
            {
                switch (Profile)
                {
                    case RuntimeProfile.Fast:
                        return "low";
                    case RuntimeProfile.Balanced:
                        return "medium";
                    case RuntimeProfile.Deep:
                        return "high";
                    case RuntimeProfile.CodexDefault:
                    default:
                        return string.Empty;
                }
            }
        }

        public bool UsesCodexDefaultModel
        {
            get { return true; }
        }

        public bool UsesCodexDefaultReasoning
        {
            get { return Profile == RuntimeProfile.CodexDefault; }
        }

        public RuntimeInvocationSummary CreateSummary(string runtimeStatusSummary, string fallbackReason = null)
        {
            var effectiveFallbackReason = fallbackReason ??
                (Profile == RuntimeProfile.CodexDefault
                    ? string.Empty
                    : "Model override omitted; using Codex default/configured model.");

            return new RuntimeInvocationSummary(
                DisplayName,
                string.Empty,
                RequestedReasoningEffort,
                UsesCodexDefaultModel,
                UsesCodexDefaultReasoning,
                UsesCodexDefaultReasoning ? "Codex default config" : "Reasoning override only",
                runtimeStatusSummary,
                effectiveFallbackReason);
        }
    }
}
