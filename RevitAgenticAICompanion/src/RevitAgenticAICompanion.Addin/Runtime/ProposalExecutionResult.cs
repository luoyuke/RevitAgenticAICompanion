using System.Collections.Generic;

namespace RevitAgenticAICompanion.Runtime
{
    public sealed class ProposalExecutionResult
    {
        public ProposalExecutionResult(
            bool isSuccess,
            string transactionName,
            string summary,
            IReadOnlyList<long> changedElementIds,
            string error)
        {
            IsSuccess = isSuccess;
            TransactionName = transactionName ?? string.Empty;
            Summary = summary ?? string.Empty;
            ChangedElementIds = changedElementIds ?? new long[0];
            Error = error ?? string.Empty;
        }

        public bool IsSuccess { get; }
        public string TransactionName { get; }
        public string Summary { get; }
        public IReadOnlyList<long> ChangedElementIds { get; }
        public string Error { get; }
    }
}
