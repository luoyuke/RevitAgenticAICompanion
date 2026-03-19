namespace RevitAgenticAICompanion.Runtime
{
    public sealed class AgentRuntimeStatus
    {
        public AgentRuntimeStatus(
            string mode,
            bool isAvailable,
            bool canPlan,
            bool isAuthenticated,
            bool supportsLogin,
            string detail)
        {
            Mode = mode ?? string.Empty;
            IsAvailable = isAvailable;
            CanPlan = canPlan;
            IsAuthenticated = isAuthenticated;
            SupportsLogin = supportsLogin;
            Detail = detail ?? string.Empty;
        }

        public string Mode { get; }
        public bool IsAvailable { get; }
        public bool CanPlan { get; }
        public bool IsAuthenticated { get; }
        public bool SupportsLogin { get; }
        public string Detail { get; }
    }
}
