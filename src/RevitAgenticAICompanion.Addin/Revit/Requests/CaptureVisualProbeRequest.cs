using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Autodesk.Revit.DB;
using RevitAgenticAICompanion.Runtime;

namespace RevitAgenticAICompanion.Revit.Requests
{
    public sealed class CaptureVisualProbeRequest : RevitRequest<VisualProbeCaptureResult>
    {
        private readonly PlanningSession _session;

        public CaptureVisualProbeRequest(PlanningSession session)
        {
            _session = session;
        }

        protected override VisualProbeCaptureResult ExecuteCore(RevitRequestContext context)
        {
            if (_session == null || _session.Proposal == null)
            {
                return new VisualProbeCaptureResult(false, string.Empty, null, string.Empty, "No active proposal.");
            }

            if (context.Document == null || context.UIDocument == null)
            {
                return new VisualProbeCaptureResult(false, string.Empty, null, string.Empty, "No active document.");
            }

            if (string.IsNullOrWhiteSpace(_session.Proposal.ArtifactDirectory))
            {
                return new VisualProbeCaptureResult(false, string.Empty, null, string.Empty, "No artifact directory is available for the visual probe.");
            }

            var activeView = context.Document.ActiveView;
            if (activeView == null)
            {
                return new VisualProbeCaptureResult(false, string.Empty, null, string.Empty, "No active view is available for the visual probe.");
            }

            Directory.CreateDirectory(_session.Proposal.ArtifactDirectory);
            var exportPrefix = Path.Combine(_session.Proposal.ArtifactDirectory, "visual-probe-full");
            foreach (var existingFile in Directory.GetFiles(_session.Proposal.ArtifactDirectory, "visual-probe-full*.png"))
            {
                File.Delete(existingFile);
            }

            var options = new ImageExportOptions
            {
                ExportRange = ExportRange.CurrentView,
                FilePath = exportPrefix,
                HLRandWFViewsFileType = ImageFileType.PNG,
                ShadowViewsFileType = ImageFileType.PNG,
                ImageResolution = ImageResolution.DPI_150,
                ZoomType = ZoomFitType.FitToPage,
                PixelSize = 1800,
            };

            context.Document.ExportImage(options);

            var imagePath = Directory.GetFiles(_session.Proposal.ArtifactDirectory, "visual-probe-full*.png")
                .OrderByDescending(path => path, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            {
                return new VisualProbeCaptureResult(false, string.Empty, null, string.Empty, "Revit did not produce a visual probe image.");
            }

            var metadataPath = Path.Combine(_session.Proposal.ArtifactDirectory, "visual-probe-metadata.json");
            var metadata = new
            {
                documentTitle = context.Document.Title,
                documentPath = context.Document.PathName,
                activeViewName = activeView.Name,
                activeViewId = activeView.Id.Value,
                selectedElementIds = _session.ContextSnapshot?.SelectedElementIds ?? Array.Empty<int>(),
                selectedCategoryNames = _session.ContextSnapshot?.SelectedCategoryNames ?? Array.Empty<string>(),
                probePurpose = _session.Proposal.ProbePurpose ?? string.Empty,
                probeQuestion = _session.Proposal.ProbeQuestion ?? string.Empty,
                whySemanticIsInsufficient = _session.Proposal.WhySemanticIsInsufficient ?? string.Empty,
                capturedUtc = DateTimeOffset.UtcNow,
            };
            File.WriteAllText(metadataPath, JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true }));

            var summary = "Captured active-view visual probe for '" + activeView.Name + "' with 1 image.";
            return new VisualProbeCaptureResult(true, summary, new[] { imagePath }, metadataPath, string.Empty);
        }
    }
}
