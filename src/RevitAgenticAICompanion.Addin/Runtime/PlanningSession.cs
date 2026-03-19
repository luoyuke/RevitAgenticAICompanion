namespace RevitAgenticAICompanion.Runtime
{
    public sealed class PlanningSession
    {
        public PlanningSession(
            ProposalCandidate proposal,
            ValidationReport validationReport,
            GeneratedActionCompilationResult compilationResult,
            RevitContextSnapshot contextSnapshot)
        {
            Proposal = proposal;
            ValidationReport = validationReport;
            CompilationResult = compilationResult;
            ContextSnapshot = contextSnapshot;
        }

        public ProposalCandidate Proposal { get; }
        public ValidationReport ValidationReport { get; }
        public GeneratedActionCompilationResult CompilationResult { get; }
        public RevitContextSnapshot ContextSnapshot { get; }
        public GeneratedActionPreviewResult PreviewResult { get; set; }
        public bool IsApproved { get; set; }
        public ProposalExecutionResult ExecutionResult { get; set; }
    }
}
