using System;
using System.Threading;
using System.Threading.Tasks;

namespace RevitAgenticAICompanion.Runtime
{
    public sealed class FallbackAgentRuntimeClient : IAgentRuntimeClient
    {
        private readonly IAgentRuntimeClient _primary;
        private readonly IAgentRuntimeClient _fallback;

        public FallbackAgentRuntimeClient(IAgentRuntimeClient primary, IAgentRuntimeClient fallback)
        {
            _primary = primary;
            _fallback = fallback;
        }

        public async Task<AgentRuntimeStatus> GetStatusAsync(CancellationToken cancellationToken)
        {
            var primaryStatus = await _primary.GetStatusAsync(cancellationToken);
            if (primaryStatus.IsAvailable)
            {
                return primaryStatus;
            }

            var fallbackStatus = await _fallback.GetStatusAsync(cancellationToken);
            return new AgentRuntimeStatus(
                fallbackStatus.Mode,
                fallbackStatus.IsAvailable,
                fallbackStatus.CanPlan,
                primaryStatus.IsAuthenticated,
                primaryStatus.SupportsLogin,
                primaryStatus.Detail + " Falling back to local review planning.");
        }

        public async Task<LoginStartResult> StartLoginAsync(CancellationToken cancellationToken)
        {
            var primaryStatus = await _primary.GetStatusAsync(cancellationToken);
            if (!primaryStatus.IsAvailable)
            {
                return new LoginStartResult(false, string.Empty, primaryStatus.Detail);
            }

            return await _primary.StartLoginAsync(cancellationToken);
        }

        public async Task<ProposalCandidate> CreateProposalAsync(
            PlanningRequest request,
            RuntimeInvocationOptions runtimeOptions,
            CancellationToken cancellationToken)
        {
            var primaryStatus = await _primary.GetStatusAsync(cancellationToken);
            if (!primaryStatus.IsAvailable)
            {
                return await _fallback.CreateProposalAsync(request, runtimeOptions, cancellationToken);
            }

            if (!primaryStatus.CanPlan)
            {
                throw new InvalidOperationException(primaryStatus.Detail);
            }

            return await _primary.CreateProposalAsync(request, runtimeOptions, cancellationToken);
        }

        public async Task<ProposalCandidate> RepairProposalAsync(
            PlanningRequest request,
            ProposalCandidate failedProposal,
            GeneratedActionCompilationResult compilation,
            RuntimeInvocationOptions runtimeOptions,
            CancellationToken cancellationToken)
        {
            var primaryStatus = await _primary.GetStatusAsync(cancellationToken);
            if (!primaryStatus.IsAvailable)
            {
                return await _fallback.RepairProposalAsync(request, failedProposal, compilation, runtimeOptions, cancellationToken);
            }

            if (!primaryStatus.CanPlan)
            {
                throw new InvalidOperationException(primaryStatus.Detail);
            }

            return await _primary.RepairProposalAsync(request, failedProposal, compilation, runtimeOptions, cancellationToken);
        }

        public async Task<ProposalCandidate> AnalyzeFailureAsync(
            PlanningRequest request,
            ProposalCandidate failedProposal,
            ExecutionFailurePacket failurePacket,
            RuntimeInvocationOptions runtimeOptions,
            CancellationToken cancellationToken)
        {
            var primaryStatus = await _primary.GetStatusAsync(cancellationToken);
            if (!primaryStatus.IsAvailable)
            {
                return await _fallback.AnalyzeFailureAsync(request, failedProposal, failurePacket, runtimeOptions, cancellationToken);
            }

            if (!primaryStatus.CanPlan)
            {
                throw new InvalidOperationException(primaryStatus.Detail);
            }

            return await _primary.AnalyzeFailureAsync(request, failedProposal, failurePacket, runtimeOptions, cancellationToken);
        }
    }
}
