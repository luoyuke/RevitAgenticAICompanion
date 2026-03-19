using System;
using RevitAgenticAICompanion.Runtime;

namespace RevitAgenticAICompanion.Revit.Requests
{
    public sealed class PreviewGeneratedProposalRequest : RevitRequest<GeneratedActionPreviewResult>
    {
        private readonly PlanningSession _session;
        private readonly GeneratedActionExecutor _executor;

        public PreviewGeneratedProposalRequest(PlanningSession session, GeneratedActionExecutor executor)
        {
            _session = session;
            _executor = executor;
        }

        protected override GeneratedActionPreviewResult ExecuteCore(RevitRequestContext context)
        {
            if (_session == null || _session.Proposal == null)
            {
                return new GeneratedActionPreviewResult(false, string.Empty, null, "No active proposal.");
            }

            if (context.UIApplication == null)
            {
                return new GeneratedActionPreviewResult(false, string.Empty, null, "No active Revit UI application.");
            }

            try
            {
                return _executor.Preview(_session.CompilationResult, _session.Proposal, context.UIApplication);
            }
            catch (Exception ex)
            {
                return new GeneratedActionPreviewResult(false, string.Empty, null, ex.ToString());
            }
        }
    }
}
