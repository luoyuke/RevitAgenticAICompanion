using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
        private readonly UserMemoryStore _userMemoryStore;
        private static readonly TimeSpan PlanningBudget = TimeSpan.FromMinutes(8);
        private const int MaxInspectionProbes = 3;

        public RuntimeCoordinator(
            RevitRequestDispatcher dispatcher,
            DocumentStateTracker documentStateTracker,
            IAgentRuntimeClient agentRuntimeClient,
            GeneratedCodeValidator validator,
            GeneratedActionCompiler compiler,
            GeneratedActionExecutor executor,
            ArtifactStore artifactStore,
            AuditStore auditStore,
            UserMemoryStore userMemoryStore)
        {
            _dispatcher = dispatcher;
            _documentStateTracker = documentStateTracker;
            _agentRuntimeClient = agentRuntimeClient;
            _validator = validator;
            _compiler = compiler;
            _executor = executor;
            _artifactStore = artifactStore;
            _auditStore = auditStore;
            _userMemoryStore = userMemoryStore;
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
            var userPreferences = _userMemoryStore.GetPreferences();
            var planningRequest = new PlanningRequest(
                prompt,
                snapshot,
                userPreferences: userPreferences,
                maxProbeCount: MaxInspectionProbes);
            var planningStopwatch = Stopwatch.StartNew();

            while (true)
            {
                // The host owns the outer loop so Codex can inspect in bounded steps without
                // getting stuck in an unbounded "probe forever" cycle.
                if (planningStopwatch.Elapsed >= PlanningBudget)
                {
                    var timeoutProposal = ProposalCandidate.CreateReply(
                        prompt,
                        BuildBudgetExceededMessage(planningRequest, planningStopwatch.Elapsed),
                        "reply",
                        "low",
                        "Planning paused after bounded inspection.",
                        "low",
                        Array.Empty<string>(),
                        new ProposalProvenance("Host timeout", 0));
                    CurrentSession = BuildSession(snapshot, planningRequest, timeoutProposal, new ValidationReport(), GeneratedActionCompilationResult.NotApplicable());
                    UpdateUserMemory(CurrentSession, null);
                    _auditStore.WritePlanning(CurrentSession);
                    return CurrentSession;
                }

                var proposal = await _agentRuntimeClient.CreateProposalAsync(planningRequest, cancellationToken);
                proposal.SourceHash = ComputeSourceHash(proposal.GeneratedSource);

                var validation = new ValidationReport();
                var compilation = GeneratedActionCompilationResult.NotApplicable();
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

                proposal.ArtifactDirectory = _artifactStore.WriteProposal(snapshot, planningRequest, proposal, validation, compilation);
                var session = BuildSession(snapshot, planningRequest, proposal, validation, compilation);

                if (proposal.ContinuesPlanning)
                {
                    if (planningRequest.CompletedProbeCount >= planningRequest.MaxProbeCount)
                    {
                        var probeLimitProposal = ProposalCandidate.CreateReply(
                            prompt,
                            BuildProbeLimitMessage(planningRequest),
                            "reply",
                            "low",
                            "Planning paused after bounded inspection.",
                            "low",
                            Array.Empty<string>(),
                            new ProposalProvenance("Host probe limit", proposal.Provenance?.RepairCount ?? 0));
                        CurrentSession = BuildSession(snapshot, planningRequest, probeLimitProposal, new ValidationReport(), GeneratedActionCompilationResult.NotApplicable());
                        UpdateUserMemory(CurrentSession, null);
                        _auditStore.WritePlanning(CurrentSession);
                        return CurrentSession;
                    }

                    if (!validation.IsValid || !compilation.IsSuccess)
                    {
                        CurrentSession = session;
                        _auditStore.WritePlanning(CurrentSession);
                        return CurrentSession;
                    }

                    var probeExecution = await _dispatcher.Enqueue(new ExecuteReadOnlyProposalRequest(session, _executor));
                    session.ExecutionResult = probeExecution;
                    _artifactStore.WriteExecution(session, probeExecution);
                    _auditStore.WritePlanning(session);
                    _auditStore.WriteExecution(session, probeExecution);

                    if (!probeExecution.IsSuccess)
                    {
                        CurrentSession = await HandleFailureAnalysisAsync(session, probeExecution, "probe");
                        return CurrentSession;
                    }

                    // Probe output becomes authoritative turn-local evidence for the next Codex step.
                    var evidence = new ProbeEvidence(
                        proposal.ProposalId,
                        planningRequest.CompletedProbeCount + 1,
                        proposal.ProbePurpose,
                        proposal.ProbeQuestion,
                        probeExecution.Summary,
                        probeExecution.ChangedElementIds,
                        proposal.SourceHash,
                        proposal.ArtifactDirectory);
                    planningRequest = planningRequest.WithEvidence(evidence, userPreferences);
                    continue;
                }

                CurrentSession = session;

                await FinalizeSessionAsync(CurrentSession);
                return CurrentSession;
            }
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
            if (result.IsSuccess)
            {
                UpdateUserMemory(CurrentSession, result);
                _artifactStore.WriteExecution(CurrentSession, result);
                _auditStore.WriteExecution(CurrentSession, result);
                return result;
            }

            var failedSession = CurrentSession;
            var failurePacket = ExecutionFailurePacket.FromSession(failedSession, result, "execute");
            _artifactStore.WriteExecution(failedSession, result);
            _artifactStore.WriteFailurePacket(failedSession, failurePacket);
            _auditStore.WriteExecution(failedSession, result);
            _auditStore.WriteFailure(failedSession, failurePacket, string.Empty);

            try
            {
                // Failed writes are never retried blindly. Codex gets a compact failure packet and
                // may return either an explanation or a fresh proposal that must pass approval again.
                var analysisSession = await AnalyzeFailureAsync(failedSession, failurePacket);
                if (analysisSession != null)
                {
                    CurrentSession = analysisSession;
                    _artifactStore.WriteFailureAnalysis(failedSession, analysisSession);
                    _auditStore.WriteFailure(failedSession, failurePacket, analysisSession.Proposal?.ProposalId ?? string.Empty);
                }
            }
            catch
            {
                CurrentSession = failedSession;
            }

            return result;
        }

        private PlanningSession BuildSession(
            RevitContextSnapshot snapshot,
            PlanningRequest planningRequest,
            ProposalCandidate proposal,
            ValidationReport validation,
            GeneratedActionCompilationResult compilation,
            ExecutionFailurePacket failurePacket = null)
        {
            return new PlanningSession(
                proposal,
                validation,
                compilation,
                snapshot,
                planningRequest?.RetrievedEvidence ?? Array.Empty<ProbeEvidence>(),
                planningRequest?.UserPreferences ?? Array.Empty<UserPreferenceRecord>(),
                failurePacket);
        }

        private static string BuildBudgetExceededMessage(PlanningRequest request, TimeSpan elapsed)
        {
            var lastEvidence = request?.RetrievedEvidence?.LastOrDefault();
            if (lastEvidence == null)
            {
                return "Planning exceeded the 8-minute budget before a grounded proposal was ready. Ask me to continue if you want me to keep inspecting.";
            }

            return "Planning exceeded the 8-minute budget after " + request.CompletedProbeCount +
                " inspection step(s). Latest evidence: " + lastEvidence.Summary +
                " Ask me to continue if you want me to keep inspecting.";
        }

        private static string BuildProbeLimitMessage(PlanningRequest request)
        {
            var evidenceLines = request?.RetrievedEvidence?
                .Select(evidence => evidence.ProbeOrdinal + ". " + evidence.Summary)
                .ToArray() ?? Array.Empty<string>();

            return "I reached the maximum of " + request?.MaxProbeCount +
                " read-only inspection steps. Evidence gathered: " +
                string.Join(" | ", evidenceLines) +
                ". Clarify the target or ask me to continue with these assumptions.";
        }

        private void UpdateUserMemory(PlanningSession session, ProposalExecutionResult execution)
        {
            if (session == null)
            {
                return;
            }

            _userMemoryStore.UpdateFromTurn(session.Proposal?.UserPrompt, session.Proposal, execution);
        }

        private async Task FinalizeSessionAsync(PlanningSession session)
        {
            if (session == null)
            {
                return;
            }

            // Read-only queries execute immediately after planning; write actions stop at preview
            // until the user explicitly approves them.
            if (session.Proposal.RequiresCompilation && session.ValidationReport.IsValid && session.CompilationResult.IsSuccess)
            {
                if (session.Proposal.ExecutesReadOnly)
                {
                    var execution = await _dispatcher.Enqueue(new ExecuteReadOnlyProposalRequest(session, _executor));
                    session.ExecutionResult = execution;
                    _artifactStore.WriteExecution(session, execution);
                    if (!execution.IsSuccess && session.FailurePacket == null)
                    {
                        CurrentSession = await HandleFailureAnalysisAsync(session, execution, "read-only");
                        return;
                    }
                }
                else if (session.Proposal.RequiresPreview)
                {
                    var preview = await _dispatcher.Enqueue(new PreviewGeneratedProposalRequest(session, _executor));
                    session.PreviewResult = preview;
                    _artifactStore.WritePreview(session, preview);
                }
            }

            UpdateUserMemory(session, session.ExecutionResult);
            _auditStore.WritePlanning(session);
            if (session.ExecutionResult != null)
            {
                _auditStore.WriteExecution(session, session.ExecutionResult);
            }
        }

        private async Task<PlanningSession> HandleFailureAnalysisAsync(
            PlanningSession failedSession,
            ProposalExecutionResult failureResult,
            string failureStage)
        {
            if (failedSession == null)
            {
                return null;
            }

            if (failureResult == null || failureResult.IsSuccess || failedSession.FailurePacket != null)
            {
                return failedSession;
            }

            var failurePacket = ExecutionFailurePacket.FromSession(failedSession, failureResult, failureStage);
            _artifactStore.WriteFailurePacket(failedSession, failurePacket);
            _auditStore.WriteFailure(failedSession, failurePacket, string.Empty);

            try
            {
                var analysisSession = await AnalyzeFailureAsync(failedSession, failurePacket);
                if (analysisSession != null)
                {
                    _artifactStore.WriteFailureAnalysis(failedSession, analysisSession);
                    _auditStore.WriteFailure(failedSession, failurePacket, analysisSession.Proposal?.ProposalId ?? string.Empty);
                    return analysisSession;
                }
            }
            catch
            {
                // Keep the original failed session when analysis itself fails.
            }

            return failedSession;
        }

        private async Task<PlanningSession> AnalyzeFailureAsync(PlanningSession failedSession, ExecutionFailurePacket failurePacket)
        {
            // Failure analysis reuses the same prompt, snapshot, evidence, and user preferences so
            // Codex repairs the exact plan that failed, not a reconstructed approximation.
            var planningRequest = new PlanningRequest(
                failedSession.Proposal?.UserPrompt,
                failedSession.ContextSnapshot,
                failedSession.RetrievedEvidence,
                failedSession.UserPreferences,
                failedSession.RetrievedEvidence?.Count ?? 0,
                MaxInspectionProbes);

            var analyzedProposal = await _agentRuntimeClient.AnalyzeFailureAsync(
                planningRequest,
                failedSession.Proposal,
                failurePacket,
                CancellationToken.None);
            analyzedProposal.SourceHash = ComputeSourceHash(analyzedProposal.GeneratedSource);

            var validation = new ValidationReport();
            var compilation = GeneratedActionCompilationResult.NotApplicable();
            if (analyzedProposal.RequiresCompilation)
            {
                validation = _validator.Validate(analyzedProposal);
                validation.IsUndoHostile |= analyzedProposal.IsUndoHostile;
                compilation = _compiler.Compile(analyzedProposal);
                if (!compilation.IsSuccess)
                {
                    analyzedProposal = await _agentRuntimeClient.RepairProposalAsync(planningRequest, analyzedProposal, compilation, CancellationToken.None);
                    analyzedProposal.SourceHash = ComputeSourceHash(analyzedProposal.GeneratedSource);
                    validation = _validator.Validate(analyzedProposal);
                    validation.IsUndoHostile |= analyzedProposal.IsUndoHostile;
                    compilation = analyzedProposal.RequiresCompilation
                        ? _compiler.Compile(analyzedProposal)
                        : GeneratedActionCompilationResult.NotApplicable();
                }

                if (!compilation.IsSuccess)
                {
                    validation.Errors.Add("Generated code failed compilation.");
                }
            }

            analyzedProposal.ArtifactDirectory = _artifactStore.WriteProposal(
                failedSession.ContextSnapshot,
                planningRequest,
                analyzedProposal,
                validation,
                compilation,
                failurePacket);

            var analysisSession = BuildSession(
                failedSession.ContextSnapshot,
                planningRequest,
                analyzedProposal,
                validation,
                compilation,
                failurePacket);

            await FinalizeSessionAsync(analysisSession);
            return analysisSession;
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
