using System.Collections.Generic;

namespace RevitAgenticAICompanion.Runtime
{
    public sealed class ProbeEvidence
    {
        public ProbeEvidence(
            string probeId,
            int probeOrdinal,
            ProbeMode probeMode,
            string purpose,
            string question,
            string summary,
            IReadOnlyList<long> elementIds,
            IReadOnlyList<string> imagePaths,
            string metadataPath,
            string whySemanticIsInsufficient,
            string sourceHash,
            string artifactDirectory)
        {
            ProbeId = probeId ?? string.Empty;
            ProbeOrdinal = probeOrdinal;
            ProbeMode = probeMode;
            Purpose = purpose ?? string.Empty;
            Question = question ?? string.Empty;
            Summary = summary ?? string.Empty;
            ElementIds = elementIds ?? new long[0];
            ImagePaths = imagePaths ?? new string[0];
            MetadataPath = metadataPath ?? string.Empty;
            WhySemanticIsInsufficient = whySemanticIsInsufficient ?? string.Empty;
            SourceHash = sourceHash ?? string.Empty;
            ArtifactDirectory = artifactDirectory ?? string.Empty;
        }

        public string ProbeId { get; }
        public int ProbeOrdinal { get; }
        public ProbeMode ProbeMode { get; }
        public string Purpose { get; }
        public string Question { get; }
        public string Summary { get; }
        public IReadOnlyList<long> ElementIds { get; }
        public IReadOnlyList<string> ImagePaths { get; }
        public string MetadataPath { get; }
        public string WhySemanticIsInsufficient { get; }
        public string SourceHash { get; }
        public string ArtifactDirectory { get; }
    }
}
