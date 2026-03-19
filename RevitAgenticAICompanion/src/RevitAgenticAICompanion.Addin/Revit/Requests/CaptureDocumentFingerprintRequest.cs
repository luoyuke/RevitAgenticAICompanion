using RevitAgenticAICompanion.Infrastructure;

namespace RevitAgenticAICompanion.Revit.Requests
{
    public sealed class CaptureDocumentFingerprintRequest : RevitRequest<DocumentFingerprint>
    {
        private readonly DocumentStateTracker _documentStateTracker;

        public CaptureDocumentFingerprintRequest(DocumentStateTracker documentStateTracker)
        {
            _documentStateTracker = documentStateTracker;
        }

        protected override DocumentFingerprint ExecuteCore(RevitRequestContext context)
        {
            return _documentStateTracker.Capture(context.Document);
        }
    }
}
