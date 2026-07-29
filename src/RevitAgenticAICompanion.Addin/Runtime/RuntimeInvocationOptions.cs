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
                    case RuntimeProfile.ProviderDefault:
                        return "Provider default";
                    case RuntimeProfile.Fast:
                        return "Fast";
                    case RuntimeProfile.Deep:
                        return "Deep";
                    case RuntimeProfile.Experimental:
                        return "Experimental";
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
                    case RuntimeProfile.Experimental:
                        return "high";
                    case RuntimeProfile.ProviderDefault:
                    default:
                        return string.Empty;
                }
            }
        }

        public bool UsesProviderDefaultModel
        {
            get { return UsesProviderDefaultModelForProvider(AgentRuntimeProvider.Codex); }
        }

        public bool UsesProviderDefaultReasoning
        {
            get { return Profile == RuntimeProfile.ProviderDefault; }
        }

        public RuntimeInvocationSummary CreateSummary(string runtimeStatusSummary, string fallbackReason = null)
        {
            return CreateSummary(AgentRuntimeProvider.Codex, runtimeStatusSummary, fallbackReason);
        }

        public RuntimeInvocationSummary CreateSummary(AgentRuntimeProvider provider, string runtimeStatusSummary, string fallbackReason = null)
        {
            var effectiveFallbackReason = fallbackReason ??
                (UsesProviderDefaultModelForProvider(provider) && UsesProviderDefaultReasoning
                    ? string.Empty
                    : BuildFallbackReason(provider));

            return new RuntimeInvocationSummary(
                DisplayName,
                RequestedModelForProvider(provider),
                RequestedReasoningEffort,
                UsesProviderDefaultModelForProvider(provider),
                UsesProviderDefaultReasoning,
                BuildCommandOverrideStrategy(provider),
                runtimeStatusSummary,
                effectiveFallbackReason);
        }

        public string RequestedModelForProvider(AgentRuntimeProvider provider)
        {
            if (provider != AgentRuntimeProvider.Claude)
            {
                return string.Empty;
            }

            switch (Profile)
            {
                case RuntimeProfile.Fast:
                case RuntimeProfile.Balanced:
                    return "sonnet";
                case RuntimeProfile.Deep:
                    return "opus";
                case RuntimeProfile.Experimental:
                    return "fable";
                case RuntimeProfile.ProviderDefault:
                default:
                    return string.Empty;
            }
        }

        public bool UsesProviderDefaultModelForProvider(AgentRuntimeProvider provider)
        {
            return string.IsNullOrWhiteSpace(RequestedModelForProvider(provider));
        }

        private string BuildCommandOverrideStrategy(AgentRuntimeProvider provider)
        {
            var usesDefaultModel = UsesProviderDefaultModelForProvider(provider);
            if (usesDefaultModel && UsesProviderDefaultReasoning)
            {
                return provider == AgentRuntimeProvider.Claude
                    ? "Claude provider default config"
                    : "Codex default config";
            }

            if (!usesDefaultModel && !UsesProviderDefaultReasoning)
            {
                return "Model and reasoning override";
            }

            return usesDefaultModel ? "Reasoning override only" : "Model override only";
        }

        private string BuildFallbackReason(AgentRuntimeProvider provider)
        {
            var usesDefaultModel = UsesProviderDefaultModelForProvider(provider);
            if (!usesDefaultModel && UsesProviderDefaultReasoning)
            {
                return "Reasoning override omitted; using provider default/configured reasoning.";
            }

            if (usesDefaultModel && !UsesProviderDefaultReasoning)
            {
                return "Model override omitted; using provider default/configured model.";
            }

            return string.Empty;
        }
    }
}
