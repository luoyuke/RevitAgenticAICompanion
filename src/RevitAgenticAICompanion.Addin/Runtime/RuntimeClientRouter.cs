using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
namespace RevitAgenticAICompanion.Runtime
{
    public sealed class RuntimeClientRouter : IAgentRuntimeClient, IDisposable
    {
        private readonly IReadOnlyDictionary<AgentRuntimeProvider, IAgentRuntimeClient> _clients;
        private readonly IReadOnlyList<IDisposable> _disposables;
        public RuntimeClientRouter(
            IReadOnlyDictionary<AgentRuntimeProvider, IAgentRuntimeClient> clients,
            IReadOnlyList<IDisposable> disposables,
            AgentRuntimeProvider selectedProvider)
        {
            _clients = clients ?? throw new ArgumentNullException(nameof(clients));
            _disposables = disposables ?? Array.Empty<IDisposable>();
            SelectedProvider = selectedProvider;
        }
        public AgentRuntimeProvider SelectedProvider { get; set; }
        public Task<AgentRuntimeStatus> GetStatusAsync(CancellationToken cancellationToken)
        {
            return Current.GetStatusAsync(cancellationToken);
        }
        public Task<LoginStartResult> StartLoginAsync(CancellationToken cancellationToken)
        {
            return Current.StartLoginAsync(cancellationToken);
        }
        public Task<ProposalCandidate> CreateProposalAsync(
            PlanningRequest request,
            RuntimeInvocationOptions runtimeOptions,
            CancellationToken cancellationToken)
        {
            return Current.CreateProposalAsync(request, runtimeOptions, cancellationToken);
        }
        public Task<ProposalCandidate> RepairProposalAsync(
            PlanningRequest request,
            ProposalCandidate failedProposal,
            GeneratedActionCompilationResult compilation,
            RuntimeInvocationOptions runtimeOptions,
            CancellationToken cancellationToken)
        {
            return Current.RepairProposalAsync(request, failedProposal, compilation, runtimeOptions, cancellationToken);
        }
        public Task<ProposalCandidate> AnalyzeFailureAsync(
            PlanningRequest request,
            ProposalCandidate failedProposal,
            ExecutionFailurePacket failurePacket,
            RuntimeInvocationOptions runtimeOptions,
            CancellationToken cancellationToken)
        {
            return Current.AnalyzeFailureAsync(request, failedProposal, failurePacket, runtimeOptions, cancellationToken);
        }
        public void Dispose()
        {
            foreach (var disposable in _disposables)
            {
                try { disposable?.Dispose(); } catch { }
            }
        }
        private IAgentRuntimeClient Current
        {
            get
            {
                return _clients.TryGetValue(SelectedProvider, out var client)
                    ? client
                    : _clients[AgentRuntimeProvider.Codex];
            }
        }
    }
}