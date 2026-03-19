using System;

namespace RevitAgenticAICompanion.Runtime
{
    public sealed class GeneratedCodeValidator
    {
        private static readonly string[] UndoHostileTokens =
        {
            ".Unload(",
            ".Reload(",
            ".LoadFrom(",
            ".Load(",
            ".SaveAs(",
            ".Close(",
            "ClearUndo",
        };

        private static readonly string[] ForbiddenTokens =
        {
            "new Transaction(",
            "new TransactionGroup(",
        };

        public ValidationReport Validate(ProposalCandidate proposal)
        {
            var report = new ValidationReport();
            if (proposal == null)
            {
                report.Errors.Add("No proposal was generated.");
                return report;
            }

            if (!proposal.RequiresCompilation)
            {
                return report;
            }

            if (string.IsNullOrWhiteSpace(proposal.GeneratedSource))
            {
                report.Errors.Add("Generated source is empty.");
            }

            if (string.IsNullOrWhiteSpace(proposal.CapabilityBand))
            {
                report.Errors.Add("A capability band is required.");
            }

            if (string.IsNullOrWhiteSpace(proposal.RiskLevel))
            {
                report.Errors.Add("A risk level is required.");
            }

            if (proposal.RequiresApproval && (proposal.TransactionNames == null || proposal.TransactionNames.Count == 0))
            {
                report.Errors.Add("At least one human-readable transaction name is required.");
            }

            if (proposal.RequiresApproval &&
                string.Equals(proposal.CapabilityBand, "creation_workflow", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(proposal.RiskLevel, "low", StringComparison.OrdinalIgnoreCase))
            {
                report.Warnings.Add("Creation workflow proposals should normally be marked medium or high risk.");
            }

            if (proposal.RequiresApproval &&
                string.Equals(proposal.CapabilityBand, "clash_coordination", StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(proposal.ScopeSummary))
            {
                report.Warnings.Add("Clash coordination proposals should describe a bounded scope.");
            }

            var source = proposal.GeneratedSource ?? string.Empty;
            foreach (var forbiddenToken in ForbiddenTokens)
            {
                if (source.IndexOf(forbiddenToken, StringComparison.Ordinal) >= 0)
                {
                    report.Errors.Add("Generated code must not own top-level transaction lifecycle: " + forbiddenToken);
                }
            }

            foreach (var undoHostileToken in UndoHostileTokens)
            {
                if (source.IndexOf(undoHostileToken, StringComparison.Ordinal) >= 0)
                {
                    report.IsUndoHostile = true;
                    report.Warnings.Add("Detected undo-hostile API usage: " + undoHostileToken);
                }
            }

            if (source.IndexOf("System.IO", StringComparison.Ordinal) >= 0)
            {
                report.Warnings.Add("Generated code references System.IO. File output is allowed for the demo but should still be reviewed.");
            }

            return report;
        }
    }
}
