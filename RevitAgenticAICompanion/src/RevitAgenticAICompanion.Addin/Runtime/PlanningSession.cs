using System;
using System.Collections.Generic;

namespace RevitAgenticAICompanion.Runtime
{
    public sealed class PlanningSession
    {
        public PlanningSession(
            ProposalCandidate proposal,
            ValidationReport validationReport,
            GeneratedActionCompilationResult compilationResult,
            RevitContextSnapshot contextSnapshot,
            IReadOnlyList<ProbeEvidence> retrievedEvidence,
            IReadOnlyList<ProjectConventionRecord> projectConventions)
        {
            Proposal = proposal;
            ValidationReport = validationReport;
            CompilationResult = compilationResult;
            ContextSnapshot = contextSnapshot;
            RetrievedEvidence = retrievedEvidence ?? Array.Empty<ProbeEvidence>();
            ProjectConventions = projectConventions ?? Array.Empty<ProjectConventionRecord>();
        }

        public ProposalCandidate Proposal { get; }
        public ValidationReport ValidationReport { get; }
        public GeneratedActionCompilationResult CompilationResult { get; }
        public RevitContextSnapshot ContextSnapshot { get; }
        public IReadOnlyList<ProbeEvidence> RetrievedEvidence { get; }
        public IReadOnlyList<ProjectConventionRecord> ProjectConventions { get; }
        public GeneratedActionPreviewResult PreviewResult { get; set; }
        public bool IsApproved { get; set; }
        public ProposalExecutionResult ExecutionResult { get; set; }
    }
}
