using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RevitAgenticAICompanion.Infrastructure;
using RevitAgenticAICompanion.Revit;
using RevitAgenticAICompanion.Revit.Requests;
using RevitAgenticAICompanion.Storage;

namespace RevitAgenticAICompanion.Runtime
{
    public sealed class RuntimeCoordinator
    {
        private readonly RevitRequestDispatcher _dispatcher;
        private readonly DocumentStateTracker _documentStateTracker;
        private readonly IAgentRuntimeClient _agentRuntimeClient;
        private readonly GeneratedCodeValidator _validator;
        private readonly GeneratedActionCompiler _compiler;
        private readonly GeneratedActionExecutor _executor;
        private readonly ArtifactStore _artifactStore;
        private readonly AuditStore _auditStore;

        public RuntimeCoordinator(
            RevitRequestDispatcher dispatcher,
            DocumentStateTracker documentStateTracker,
            IAgentRuntimeClient agentRuntimeClient,
            GeneratedCodeValidator validator,
            GeneratedActionCompiler compiler,
            GeneratedActionExecutor executor,
            ArtifactStore artifactStore,
            AuditStore auditStore)
        {
            _dispatcher = dispatcher;
            _documentStateTracker = documentStateTracker;
            _agentRuntimeClient = agentRuntimeClient;
            _validator = validator;
            _compiler = compiler;
            _executor = executor;
            _artifactStore = artifactStore;
            _auditStore = auditStore;
        }

        public PlanningSession CurrentSession { get; private set; }

        public Task<AgentRuntimeStatus> GetRuntimeStatusAsync(CancellationToken cancellationToken)
        {
            return _agentRuntimeClient.GetStatusAsync(cancellationToken);
        }

        public Task<LoginStartResult> StartLoginAsync(CancellationToken cancellationToken)
        {
            return _agentRuntimeClient.StartLoginAsync(cancellationToken);
        }

        public async Task<PlanningSession> CreateProposalAsync(string prompt, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(prompt))
            {
                throw new InvalidOperationException("A prompt is required.");
            }

            var snapshot = await _dispatcher.Enqueue(new CaptureContextSnapshotRequest(_documentStateTracker));
            var planningRequest = new PlanningRequest(prompt, snapshot);
            var proposal = await _agentRuntimeClient.CreateProposalAsync(planningRequest, cancellationToken);
            proposal.SourceHash = ComputeSourceHash(proposal.GeneratedSource);

            ValidationReport validation;
            GeneratedActionCompilationResult compilation;
            if (proposal.RequiresCompilation)
            {
                validation = _validator.Validate(proposal);
                validation.IsUndoHostile |= proposal.IsUndoHostile;
                compilation = _compiler.Compile(proposal);
                if (!compilation.IsSuccess)
                {
                    proposal = await _agentRuntimeClient.RepairProposalAsync(planningRequest, proposal, compilation, cancellationToken);
                    proposal.SourceHash = ComputeSourceHash(proposal.GeneratedSource);
                    validation = _validator.Validate(proposal);
                    validation.IsUndoHostile |= proposal.IsUndoHostile;
                    compilation = proposal.RequiresCompilation
                        ? _compiler.Compile(proposal)
                        : GeneratedActionCompilationResult.NotApplicable();
                }

                if (!compilation.IsSuccess)
                {
                    validation.Errors.Add("Generated code failed compilation.");
                }
            }
            else
            {
                validation = new ValidationReport();
                compilation = GeneratedActionCompilationResult.NotApplicable();
            }

            proposal.ArtifactDirectory = _artifactStore.WriteProposal(snapshot, proposal, validation, compilation);
            CurrentSession = new PlanningSession(proposal, validation, compilation, snapshot);

            if (proposal.RequiresCompilation && validation.IsValid && compilation.IsSuccess)
            {
                if (proposal.ExecutesReadOnly)
                {
                    var execution = await _dispatcher.Enqueue(new ExecuteReadOnlyProposalRequest(CurrentSession, _executor));
                    CurrentSession.ExecutionResult = execution;
                    _artifactStore.WriteExecution(CurrentSession, execution);
                }
                else if (proposal.RequiresPreview)
                {
                    var preview = await _dispatcher.Enqueue(new PreviewGeneratedProposalRequest(CurrentSession, _executor));
                    CurrentSession.PreviewResult = preview;
                    _artifactStore.WritePreview(CurrentSession, preview);
                }
            }

            _auditStore.WritePlanning(CurrentSession);
            if (CurrentSession.ExecutionResult != null)
            {
                _auditStore.WriteExecution(CurrentSession, CurrentSession.ExecutionResult);
            }

            return CurrentSession;
        }

        public async Task<bool> ApproveCurrentProposalAsync(bool explicitConfirm)
        {
            if (CurrentSession == null)
            {
                return false;
            }

            if (!CurrentSession.Proposal.RequiresApproval)
            {
                return false;
            }

            if (CurrentSession.ValidationReport.IsUndoHostile && !explicitConfirm)
            {
                return false;
            }

            if (!CurrentSession.CompilationResult.IsSuccess)
            {
                return false;
            }

            if (CurrentSession.PreviewResult == null || !CurrentSession.PreviewResult.IsSuccess)
            {
                return false;
            }

            var currentFingerprint = await _dispatcher.Enqueue(new CaptureDocumentFingerprintRequest(_documentStateTracker));
            if (!CurrentSession.ContextSnapshot.Fingerprint.Matches(currentFingerprint))
            {
                return false;
            }

            CurrentSession.IsApproved = true;
            _auditStore.WritePlanning(CurrentSession);
            return true;
        }

        public async Task<ProposalExecutionResult> ExecuteCurrentProposalAsync()
        {
            if (CurrentSession == null)
            {
                return new ProposalExecutionResult(false, string.Empty, string.Empty, null, "No active proposal.");
            }

            if (!CurrentSession.IsApproved)
            {
                return new ProposalExecutionResult(false, string.Empty, string.Empty, null, "The proposal is not approved.");
            }

            if (!CurrentSession.Proposal.RequiresApproval)
            {
                return new ProposalExecutionResult(false, string.Empty, string.Empty, null, "The current response is not a write action and cannot be approved/executed here.");
            }

            var currentFingerprint = await _dispatcher.Enqueue(new CaptureDocumentFingerprintRequest(_documentStateTracker));
            if (!CurrentSession.ContextSnapshot.Fingerprint.Matches(currentFingerprint))
            {
                return new ProposalExecutionResult(false, string.Empty, string.Empty, null, "The document changed after approval. Re-plan before execution.");
            }

            var result = await _dispatcher.Enqueue(new ExecuteGeneratedProposalRequest(CurrentSession, _executor));
            CurrentSession.ExecutionResult = result;
            _artifactStore.WriteExecution(CurrentSession, result);
            _auditStore.WriteExecution(CurrentSession, result);
            return result;
        }

        private static string ComputeSourceHash(string source)
        {
            var bytes = Encoding.UTF8.GetBytes(source ?? string.Empty);
            using (var sha256 = SHA256.Create())
            {
                var hash = sha256.ComputeHash(bytes);
                return BitConverter.ToString(hash).Replace("-", string.Empty);
            }
        }
    }
}
