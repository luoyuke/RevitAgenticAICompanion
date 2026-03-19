using System;
using RevitAgenticAICompanion.Runtime;

namespace RevitAgenticAICompanion.Revit.Requests
{
    public sealed class ExecuteReadOnlyProposalRequest : RevitRequest<ProposalExecutionResult>
    {
        private readonly PlanningSession _session;
        private readonly GeneratedActionExecutor _executor;

        public ExecuteReadOnlyProposalRequest(PlanningSession session, GeneratedActionExecutor executor)
        {
            _session = session;
            _executor = executor;
        }

        protected override ProposalExecutionResult ExecuteCore(RevitRequestContext context)
        {
            if (_session == null || _session.Proposal == null)
            {
                return new ProposalExecutionResult(false, "Read-only query", string.Empty, null, "No active proposal.");
            }

            if (context.UIApplication == null)
            {
                return new ProposalExecutionResult(false, "Read-only query", string.Empty, null, "No active Revit UI application.");
            }

            try
            {
                var actionResult = _executor.Execute(_session.CompilationResult, _session.Proposal, context.UIApplication);
                return new ProposalExecutionResult(true, "Read-only query", actionResult.Summary, actionResult.ChangedElementIds, string.Empty);
            }
            catch (Exception ex)
            {
                return new ProposalExecutionResult(false, "Read-only query", string.Empty, null, ex.ToString());
            }
        }
    }
}
