using System;
using System.IO;
using System.Linq;
using System.Text;
using RevitAgenticAICompanion.Runtime;

namespace RevitAgenticAICompanion.Storage
{
    public sealed class ArtifactStore
    {
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);
        private readonly LocalStoragePaths _paths;

        public ArtifactStore(LocalStoragePaths paths)
        {
            _paths = paths;
        }

        public string WriteProposal(
            RevitContextSnapshot snapshot,
            PlanningRequest planningRequest,
            ProposalCandidate proposal,
            ValidationReport validation,
            GeneratedActionCompilationResult compilation,
            ExecutionFailurePacket failurePacket = null)
        {
            var directoryName = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + proposal.ProposalId;
            var directory = Path.Combine(_paths.ArtifactsPath, directoryName);
            Directory.CreateDirectory(directory);

            File.WriteAllText(Path.Combine(directory, "generated-action.cs"), proposal.GeneratedSource ?? string.Empty, Utf8NoBom);
            File.WriteAllText(Path.Combine(directory, "summary.txt"), BuildSummary(snapshot, planningRequest, proposal, validation, compilation, failurePacket), Utf8NoBom);
            File.WriteAllText(Path.Combine(directory, "compile.txt"), BuildCompilationSummary(compilation), Utf8NoBom);
            if (compilation.AssemblyBytes != null && compilation.AssemblyBytes.Length > 0)
            {
                File.WriteAllBytes(Path.Combine(directory, "generated-action.dll"), compilation.AssemblyBytes);
            }

            return directory;
        }

        public void WriteExecution(PlanningSession session, ProposalExecutionResult execution)
        {
            if (session == null || string.IsNullOrWhiteSpace(session.Proposal?.ArtifactDirectory))
            {
                return;
            }

            File.WriteAllText(
                Path.Combine(session.Proposal.ArtifactDirectory, "execution.txt"),
                BuildExecutionSummary(session, execution),
                Utf8NoBom);
        }

        public void WritePreview(PlanningSession session, GeneratedActionPreviewResult preview)
        {
            if (session == null || preview == null || string.IsNullOrWhiteSpace(session.Proposal?.ArtifactDirectory))
            {
                return;
            }

            File.WriteAllText(
                Path.Combine(session.Proposal.ArtifactDirectory, "preview.txt"),
                BuildPreviewSummary(session, preview),
                Utf8NoBom);
        }

        public void WriteFailurePacket(PlanningSession session, ExecutionFailurePacket failurePacket)
        {
            if (session == null || failurePacket == null || string.IsNullOrWhiteSpace(session.Proposal?.ArtifactDirectory))
            {
                return;
            }

            File.WriteAllText(
                Path.Combine(session.Proposal.ArtifactDirectory, "failure-packet.txt"),
                BuildFailurePacketSummary(failurePacket),
                Utf8NoBom);
        }

        public void WriteFailureAnalysis(PlanningSession failedSession, PlanningSession analysisSession)
        {
            if (failedSession == null || analysisSession == null || string.IsNullOrWhiteSpace(failedSession.Proposal?.ArtifactDirectory))
            {
                return;
            }

            var builder = new StringBuilder();
            builder.AppendLine("Failure analysis run:");
            builder.AppendLine(analysisSession.Proposal?.ProposalId ?? string.Empty);
            builder.AppendLine("Response kind: " + analysisSession.Proposal?.ResponseKind);
            builder.AppendLine("Planner: " + (analysisSession.Proposal?.Provenance?.Summary ?? "Unknown"));
            builder.AppendLine("Summary:");
            builder.AppendLine(analysisSession.Proposal?.ActionSummary ?? analysisSession.Proposal?.ReplyText ?? string.Empty);
            builder.AppendLine("Artifact directory:");
            builder.AppendLine(analysisSession.Proposal?.ArtifactDirectory ?? string.Empty);
            File.WriteAllText(
                Path.Combine(failedSession.Proposal.ArtifactDirectory, "failure-analysis.txt"),
                builder.ToString(),
                Utf8NoBom);
        }

        private static string BuildSummary(
            RevitContextSnapshot snapshot,
            PlanningRequest planningRequest,
            ProposalCandidate proposal,
            ValidationReport validation,
            GeneratedActionCompilationResult compilation,
            ExecutionFailurePacket failurePacket)
        {
            var builder = new StringBuilder();
            builder.AppendLine("Prompt:");
            builder.AppendLine(proposal.UserPrompt ?? string.Empty);
            builder.AppendLine();
            builder.AppendLine("Document:");
            builder.AppendLine(snapshot.DocumentTitle);
            builder.AppendLine(snapshot.DocumentPath);
            builder.AppendLine("Active View: " + snapshot.ActiveViewName);
            builder.AppendLine("Fingerprint: " + snapshot.Fingerprint);
            builder.AppendLine();
            builder.AppendLine("Summary:");
            builder.AppendLine(proposal.ActionSummary ?? proposal.ReplyText ?? string.Empty);
            builder.AppendLine();
            builder.AppendLine("Response Kind:");
            builder.AppendLine(proposal.ResponseKind.ToString());
            builder.AppendLine();
            builder.AppendLine("Probe Mode:");
            builder.AppendLine(proposal.ProbeMode.ToString());
            builder.AppendLine();
            builder.AppendLine("Capability Band:");
            builder.AppendLine(proposal.CapabilityBand ?? string.Empty);
            builder.AppendLine();
            builder.AppendLine("Risk Level:");
            builder.AppendLine(proposal.RiskLevel ?? string.Empty);
            builder.AppendLine();
            builder.AppendLine("Scope Summary:");
            builder.AppendLine(proposal.ScopeSummary ?? string.Empty);
            builder.AppendLine();
            builder.AppendLine("Confidence Level:");
            builder.AppendLine(proposal.ConfidenceLevel ?? string.Empty);
            builder.AppendLine();
            builder.AppendLine("Evidence Summary:");
            builder.AppendLine(proposal.EvidenceSummary ?? string.Empty);
            builder.AppendLine();
            builder.AppendLine("Planner:");
            builder.AppendLine(proposal.Provenance?.Summary ?? "Unknown");
            builder.AppendLine();
            if (failurePacket != null)
            {
                builder.AppendLine("Failure Context:");
                builder.AppendLine("Stage: " + failurePacket.FailureStage);
                builder.AppendLine("Exception Type: " + failurePacket.ExceptionType);
                builder.AppendLine("Exception Message: " + failurePacket.ExceptionMessage);
                builder.AppendLine("Original Run Id: " + failurePacket.OriginalRunId);
                builder.AppendLine();
            }

            if (proposal.Assumptions.Count > 0)
            {
                builder.AppendLine("Assumptions:");
                foreach (var assumption in proposal.Assumptions)
                {
                    builder.AppendLine("- " + assumption);
                }

                builder.AppendLine();
            }

            if (proposal.ContinuesPlanning)
            {
                builder.AppendLine("Probe Purpose:");
                builder.AppendLine(proposal.ProbePurpose ?? string.Empty);
                builder.AppendLine("Probe Question:");
                builder.AppendLine(proposal.ProbeQuestion ?? string.Empty);
                if (proposal.ProbeMode == ProbeMode.Visual)
                {
                    builder.AppendLine("Why Semantic Is Insufficient:");
                    builder.AppendLine(proposal.WhySemanticIsInsufficient ?? string.Empty);
                }

                builder.AppendLine();
            }

            builder.AppendLine("Selected Categories:");
            builder.AppendLine(string.Join(", ", snapshot.SelectedCategoryNames ?? Enumerable.Empty<string>()));
            builder.AppendLine("Selected Element Ids:");
            builder.AppendLine(string.Join(", ", snapshot.SelectedElementIds ?? Enumerable.Empty<int>()));
            builder.AppendLine();
            builder.AppendLine("Retrieved Evidence:");
            foreach (var evidence in planningRequest?.RetrievedEvidence ?? Array.Empty<ProbeEvidence>())
            {
                builder.AppendLine("- Probe " + evidence.ProbeOrdinal + " [" + evidence.ProbeMode + "]: " + evidence.Purpose);
                builder.AppendLine("  Question: " + evidence.Question);
                builder.AppendLine("  Summary: " + evidence.Summary);
                builder.AppendLine("  Element ids: " + string.Join(", ", evidence.ElementIds ?? Enumerable.Empty<long>()));
                if (evidence.ProbeMode == ProbeMode.Visual)
                {
                    builder.AppendLine("  Why semantic is insufficient: " + evidence.WhySemanticIsInsufficient);
                    builder.AppendLine("  Image paths: " + string.Join(", ", evidence.ImagePaths ?? Enumerable.Empty<string>()));
                    builder.AppendLine("  Metadata path: " + (evidence.MetadataPath ?? string.Empty));
                }
            }

            builder.AppendLine();
            builder.AppendLine("User Preferences:");
            foreach (var preference in planningRequest?.UserPreferences ?? Array.Empty<UserPreferenceRecord>())
            {
                builder.AppendLine("- [" + preference.ConfidenceLevel + "] " + preference.Key + " = " + preference.Value + " (" + preference.Source + ")");
            }

            builder.AppendLine();
            if (proposal.ResponseKind == ProposalResponseKind.ReplyOnly)
            {
                builder.AppendLine("Reply:");
                builder.AppendLine(proposal.ReplyText ?? string.Empty);
                builder.AppendLine();
            }
            builder.AppendLine("Transaction Names:");
            foreach (var transactionName in proposal.TransactionNames ?? Enumerable.Empty<string>())
            {
                builder.AppendLine("- " + transactionName);
            }

            builder.AppendLine();
            builder.AppendLine("Source Hash:");
            builder.AppendLine(proposal.SourceHash ?? string.Empty);
            builder.AppendLine();
            builder.AppendLine("Validation:");
            builder.AppendLine("Valid: " + validation.IsValid);
            builder.AppendLine("UndoHostile: " + validation.IsUndoHostile);
            builder.AppendLine("Compiled: " + compilation.IsSuccess);

            if (validation.Errors.Count > 0)
            {
                builder.AppendLine("Errors:");
                foreach (var error in validation.Errors)
                {
                    builder.AppendLine("- " + error);
                }
            }

            if (validation.Warnings.Count > 0)
            {
                builder.AppendLine("Warnings:");
                foreach (var warning in validation.Warnings)
                {
                    builder.AppendLine("- " + warning);
                }
            }

            return builder.ToString();
        }

        private static string BuildCompilationSummary(GeneratedActionCompilationResult compilation)
        {
            var builder = new StringBuilder();
            builder.AppendLine("Compilation success: " + compilation.IsSuccess);
            builder.AppendLine("Diagnostics:");
            foreach (var diagnostic in compilation.Diagnostics ?? Enumerable.Empty<string>())
            {
                builder.AppendLine("- " + diagnostic);
            }

            return builder.ToString();
        }

        private static string BuildExecutionSummary(PlanningSession session, ProposalExecutionResult execution)
        {
            var builder = new StringBuilder();
            builder.AppendLine("Approved: " + session.IsApproved);
            builder.AppendLine("Execution success: " + execution.IsSuccess);
            builder.AppendLine("Transaction: " + execution.TransactionName);
            builder.AppendLine("Summary: " + execution.Summary);
            builder.AppendLine("Changed element ids: " + string.Join(", ", execution.ChangedElementIds ?? Enumerable.Empty<long>()));
            builder.AppendLine("Error: " + execution.Error);
            return builder.ToString();
        }

        private static string BuildPreviewSummary(PlanningSession session, GeneratedActionPreviewResult preview)
        {
            var builder = new StringBuilder();
            builder.AppendLine("Preview success: " + preview.IsSuccess);
            builder.AppendLine("Summary: " + preview.Summary);
            builder.AppendLine("Target element ids: " + string.Join(", ", preview.TargetElementIds ?? Enumerable.Empty<long>()));
            builder.AppendLine("Error: " + preview.Error);
            builder.AppendLine("Prompt: " + (session?.Proposal?.UserPrompt ?? string.Empty));
            return builder.ToString();
        }

        private static string BuildFailurePacketSummary(ExecutionFailurePacket failurePacket)
        {
            var builder = new StringBuilder();
            builder.AppendLine("Failure stage: " + failurePacket.FailureStage);
            builder.AppendLine("Exception type: " + failurePacket.ExceptionType);
            builder.AppendLine("Exception message: " + failurePacket.ExceptionMessage);
            builder.AppendLine("Transaction: " + failurePacket.TransactionName);
            builder.AppendLine("Source hash: " + failurePacket.SourceHash);
            builder.AppendLine("Document fingerprint: " + failurePacket.DocumentFingerprint);
            builder.AppendLine("Preview summary: " + failurePacket.PreviewSummary);
            builder.AppendLine("Changed element ids: " + string.Join(", ", failurePacket.ChangedElementIds ?? Enumerable.Empty<long>()));
            builder.AppendLine("Was approved: " + failurePacket.WasApproved);
            builder.AppendLine("Undo-hostile: " + failurePacket.IsUndoHostile);
            builder.AppendLine("Stack trace top:");
            foreach (var line in failurePacket.StackTraceTop ?? Array.Empty<string>())
            {
                builder.AppendLine("- " + line);
            }

            builder.AppendLine("Raw error:");
            builder.AppendLine(failurePacket.RawError ?? string.Empty);
            return builder.ToString();
        }
    }
}
