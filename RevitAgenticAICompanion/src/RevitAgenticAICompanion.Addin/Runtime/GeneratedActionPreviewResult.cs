using System.Collections.Generic;

namespace RevitAgenticAICompanion.Runtime
{
    public sealed class GeneratedActionPreviewResult
    {
        public GeneratedActionPreviewResult(
            bool isSuccess,
            string summary,
            IReadOnlyList<long> targetElementIds,
            string error)
        {
            IsSuccess = isSuccess;
            Summary = summary ?? string.Empty;
            TargetElementIds = targetElementIds ?? new long[0];
            Error = error ?? string.Empty;
        }

        public bool IsSuccess { get; }
        public string Summary { get; }
        public IReadOnlyList<long> TargetElementIds { get; }
        public string Error { get; }
    }
}
