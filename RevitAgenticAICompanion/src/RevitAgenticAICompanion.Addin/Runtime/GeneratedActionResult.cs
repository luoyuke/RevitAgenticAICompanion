using System.Collections.Generic;

namespace RevitAgenticAICompanion.Runtime
{
    public sealed class GeneratedActionResult
    {
        public GeneratedActionResult(string summary, IReadOnlyList<long> changedElementIds)
        {
            Summary = summary ?? string.Empty;
            ChangedElementIds = changedElementIds ?? new long[0];
        }

        public string Summary { get; }
        public IReadOnlyList<long> ChangedElementIds { get; }
    }
}
