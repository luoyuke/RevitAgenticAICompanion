namespace RevitAgenticAICompanion.Runtime
{
    public sealed class PlanningRequest
    {
        public PlanningRequest(string prompt, RevitContextSnapshot contextSnapshot)
        {
            Prompt = prompt;
            ContextSnapshot = contextSnapshot;
        }

        public string Prompt { get; }
        public RevitContextSnapshot ContextSnapshot { get; }
    }
}
