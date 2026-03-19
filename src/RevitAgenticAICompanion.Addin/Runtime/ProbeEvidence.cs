using System.Collections.Generic;

namespace RevitAgenticAICompanion.Runtime
{
    public sealed class ProbeEvidence
    {
        public ProbeEvidence(
            string probeId,
            int probeOrdinal,
            string purpose,
            string question,
            string summary,
            IReadOnlyList<long> elementIds,
            string sourceHash,
            string artifactDirectory)
        {
            ProbeId = probeId ?? string.Empty;
            ProbeOrdinal = probeOrdinal;
            Purpose = purpose ?? string.Empty;
            Question = question ?? string.Empty;
            Summary = summary ?? string.Empty;
            ElementIds = elementIds ?? new long[0];
            SourceHash = sourceHash ?? string.Empty;
            ArtifactDirectory = artifactDirectory ?? string.Empty;
        }

        public string ProbeId { get; }
        public int ProbeOrdinal { get; }
        public string Purpose { get; }
        public string Question { get; }
        public string Summary { get; }
        public IReadOnlyList<long> ElementIds { get; }
        public string SourceHash { get; }
        public string ArtifactDirectory { get; }
    }
}
