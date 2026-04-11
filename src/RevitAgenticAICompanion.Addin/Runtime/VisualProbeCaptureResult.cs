using System.Collections.Generic;

namespace RevitAgenticAICompanion.Runtime
{
    public sealed class VisualProbeCaptureResult
    {
        public VisualProbeCaptureResult(
            bool isSuccess,
            string summary,
            IReadOnlyList<string> imagePaths,
            string metadataPath,
            string error)
        {
            IsSuccess = isSuccess;
            Summary = summary ?? string.Empty;
            ImagePaths = imagePaths ?? new string[0];
            MetadataPath = metadataPath ?? string.Empty;
            Error = error ?? string.Empty;
        }

        public bool IsSuccess { get; }
        public string Summary { get; }
        public IReadOnlyList<string> ImagePaths { get; }
        public string MetadataPath { get; }
        public string Error { get; }
    }
}
