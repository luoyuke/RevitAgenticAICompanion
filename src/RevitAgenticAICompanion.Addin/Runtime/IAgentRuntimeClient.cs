using System.Threading;
using System.Threading.Tasks;

namespace RevitAgenticAICompanion.Runtime
{
    public interface IAgentRuntimeClient
    {
        Task<AgentRuntimeStatus> GetStatusAsync(CancellationToken cancellationToken);
        Task<LoginStartResult> StartLoginAsync(CancellationToken cancellationToken);
        Task<ProposalCandidate> CreateProposalAsync(PlanningRequest request, CancellationToken cancellationToken);
        Task<ProposalCandidate> RepairProposalAsync(
            PlanningRequest request,
            ProposalCandidate failedProposal,
            GeneratedActionCompilationResult compilation,
            CancellationToken cancellationToken);
        Task<ProposalCandidate> AnalyzeFailureAsync(
            PlanningRequest request,
            ProposalCandidate failedProposal,
            ExecutionFailurePacket failurePacket,
            CancellationToken cancellationToken);
    }
}
