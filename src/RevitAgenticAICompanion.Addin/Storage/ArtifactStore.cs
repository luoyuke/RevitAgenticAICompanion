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
            ProposalCandidate proposal,
            ValidationReport validation,
            GeneratedActionCompilationResult compilation)
        {
            var directoryName = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + proposal.ProposalId;
            var directory = Path.Combine(_paths.ArtifactsPath, directoryName);
            Directory.CreateDirectory(directory);

            File.WriteAllText(Path.Combine(directory, "generated-action.cs"), proposal.GeneratedSource ?? string.Empty, Utf8NoBom);
            File.WriteAllText(Path.Combine(directory, "summary.txt"), BuildSummary(snapshot, proposal, validation, compilation), Utf8NoBom);
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

        private static string BuildSummary(
            RevitContextSnapshot snapshot,
            ProposalCandidate proposal,
            ValidationReport validation,
            GeneratedActionCompilationResult compilation)
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
            builder.AppendLine("Capability Band:");
            builder.AppendLine(proposal.CapabilityBand ?? string.Empty);
            builder.AppendLine();
            builder.AppendLine("Risk Level:");
            builder.AppendLine(proposal.RiskLevel ?? string.Empty);
            builder.AppendLine();
            builder.AppendLine("Scope Summary:");
            builder.AppendLine(proposal.ScopeSummary ?? string.Empty);
            builder.AppendLine();
            builder.AppendLine("Planner:");
            builder.AppendLine(proposal.Provenance?.Summary ?? "Unknown");
            builder.AppendLine();
            builder.AppendLine("Selected Categories:");
            builder.AppendLine(string.Join(", ", snapshot.SelectedCategoryNames ?? Enumerable.Empty<string>()));
            builder.AppendLine("Selected Element Ids:");
            builder.AppendLine(string.Join(", ", snapshot.SelectedElementIds ?? Enumerable.Empty<int>()));
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
    }
}
