namespace RevitAgenticAICompanion.Runtime
{
    public sealed class RuntimeInvocationSummary
    {
        public RuntimeInvocationSummary(
            string selectedProfile,
            string requestedModel,
            string requestedReasoningEffort,
            bool usedCodexDefaultModel,
            bool usedCodexDefaultReasoning,
            string commandOverrideStrategy,
            string runtimeStatusSummary,
            string fallbackReason)
        {
            SelectedProfile = selectedProfile ?? string.Empty;
            RequestedModel = requestedModel ?? string.Empty;
            RequestedReasoningEffort = requestedReasoningEffort ?? string.Empty;
            UsedCodexDefaultModel = usedCodexDefaultModel;
            UsedCodexDefaultReasoning = usedCodexDefaultReasoning;
            CommandOverrideStrategy = commandOverrideStrategy ?? string.Empty;
            RuntimeStatusSummary = runtimeStatusSummary ?? string.Empty;
            FallbackReason = fallbackReason ?? string.Empty;
        }

        public string SelectedProfile { get; }
        public string RequestedModel { get; }
        public string RequestedReasoningEffort { get; }
        public bool UsedCodexDefaultModel { get; }
        public bool UsedCodexDefaultReasoning { get; }
        public string CommandOverrideStrategy { get; }
        public string RuntimeStatusSummary { get; }
        public string FallbackReason { get; }
    }
}
