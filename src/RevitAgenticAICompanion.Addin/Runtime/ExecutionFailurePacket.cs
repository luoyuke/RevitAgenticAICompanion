using System;
using System.Collections.Generic;
using System.Linq;

namespace RevitAgenticAICompanion.Runtime
{
    public sealed class ExecutionFailurePacket
    {
        public ExecutionFailurePacket(
            string originalRunId,
            string originalPrompt,
            string actionSummary,
            string responseKind,
            string transactionName,
            string sourceHash,
            string artifactDirectory,
            string failureStage,
            string exceptionType,
            string exceptionMessage,
            IReadOnlyList<string> stackTraceTop,
            string documentFingerprint,
            string previewSummary,
            IReadOnlyList<long> changedElementIds,
            bool isUndoHostile,
            bool wasApproved,
            string rawError)
        {
            OriginalRunId = originalRunId ?? string.Empty;
            OriginalPrompt = originalPrompt ?? string.Empty;
            ActionSummary = actionSummary ?? string.Empty;
            ResponseKind = responseKind ?? string.Empty;
            TransactionName = transactionName ?? string.Empty;
            SourceHash = sourceHash ?? string.Empty;
            ArtifactDirectory = artifactDirectory ?? string.Empty;
            FailureStage = failureStage ?? string.Empty;
            ExceptionType = exceptionType ?? string.Empty;
            ExceptionMessage = exceptionMessage ?? string.Empty;
            StackTraceTop = stackTraceTop ?? Array.Empty<string>();
            DocumentFingerprint = documentFingerprint ?? string.Empty;
            PreviewSummary = previewSummary ?? string.Empty;
            ChangedElementIds = changedElementIds ?? Array.Empty<long>();
            IsUndoHostile = isUndoHostile;
            WasApproved = wasApproved;
            RawError = rawError ?? string.Empty;
        }

        public string OriginalRunId { get; }
        public string OriginalPrompt { get; }
        public string ActionSummary { get; }
        public string ResponseKind { get; }
        public string TransactionName { get; }
        public string SourceHash { get; }
        public string ArtifactDirectory { get; }
        public string FailureStage { get; }
        public string ExceptionType { get; }
        public string ExceptionMessage { get; }
        public IReadOnlyList<string> StackTraceTop { get; }
        public string DocumentFingerprint { get; }
        public string PreviewSummary { get; }
        public IReadOnlyList<long> ChangedElementIds { get; }
        public bool IsUndoHostile { get; }
        public bool WasApproved { get; }
        public string RawError { get; }

        public static ExecutionFailurePacket FromSession(PlanningSession session, ProposalExecutionResult execution, string failureStage)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            if (execution == null)
            {
                throw new ArgumentNullException(nameof(execution));
            }

            var rawError = execution.Error ?? string.Empty;
            ParseError(rawError, out var exceptionType, out var exceptionMessage, out var stackTraceTop);

            return new ExecutionFailurePacket(
                session.Proposal?.ProposalId,
                session.Proposal?.UserPrompt,
                session.Proposal?.ActionSummary,
                session.Proposal?.ResponseKind.ToString(),
                execution.TransactionName,
                session.Proposal?.SourceHash,
                session.Proposal?.ArtifactDirectory,
                failureStage,
                exceptionType,
                exceptionMessage,
                stackTraceTop,
                session.ContextSnapshot?.Fingerprint?.ToString(),
                session.PreviewResult?.Summary,
                execution.ChangedElementIds,
                session.ValidationReport != null && session.ValidationReport.IsUndoHostile,
                session.IsApproved,
                rawError);
        }

        private static void ParseError(string rawError, out string exceptionType, out string exceptionMessage, out IReadOnlyList<string> stackTraceTop)
        {
            exceptionType = string.Empty;
            exceptionMessage = string.Empty;
            stackTraceTop = Array.Empty<string>();

            var lines = (rawError ?? string.Empty)
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToArray();

            if (lines.Length == 0)
            {
                return;
            }

            foreach (var line in lines)
            {
                if (line.StartsWith("at ", StringComparison.Ordinal))
                {
                    continue;
                }

                var separatorIndex = line.IndexOf(':');
                if (separatorIndex > 0)
                {
                    exceptionType = line.Substring(0, separatorIndex).Trim();
                    exceptionMessage = line.Substring(separatorIndex + 1).Trim();
                    break;
                }
            }

            if (string.IsNullOrWhiteSpace(exceptionType))
            {
                exceptionMessage = lines[0];
            }

            stackTraceTop = lines
                .Where(line => line.StartsWith("at ", StringComparison.Ordinal))
                .Take(12)
                .ToArray();
        }
    }
}
