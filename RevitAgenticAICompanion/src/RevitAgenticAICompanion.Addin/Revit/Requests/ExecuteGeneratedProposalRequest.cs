using System;
using Autodesk.Revit.DB;
using RevitAgenticAICompanion.Runtime;

namespace RevitAgenticAICompanion.Revit.Requests
{
    public sealed class ExecuteGeneratedProposalRequest : RevitRequest<ProposalExecutionResult>
    {
        private readonly PlanningSession _session;
        private readonly GeneratedActionExecutor _executor;

        public ExecuteGeneratedProposalRequest(PlanningSession session, GeneratedActionExecutor executor)
        {
            _session = session;
            _executor = executor;
        }

        protected override ProposalExecutionResult ExecuteCore(RevitRequestContext context)
        {
            if (_session == null || !_session.IsApproved)
            {
                return new ProposalExecutionResult(false, string.Empty, string.Empty, null, "The proposal is not approved.");
            }

            if (context.Document == null)
            {
                return new ProposalExecutionResult(false, string.Empty, string.Empty, null, "No active document.");
            }

            var transactionName = _session.Proposal.TransactionNames.Count > 0
                ? _session.Proposal.TransactionNames[0]
                : "Execute generated action";

            using (var transaction = new Transaction(context.Document, transactionName))
            {
                transaction.Start();
                try
                {
                    var actionResult = _executor.Execute(_session.CompilationResult, _session.Proposal, context.UIApplication);
                    transaction.Commit();

                    return new ProposalExecutionResult(
                        true,
                        transactionName,
                        actionResult.Summary,
                        actionResult.ChangedElementIds,
                        string.Empty);
                }
                catch (Exception ex)
                {
                    if (transaction.GetStatus() == TransactionStatus.Started)
                    {
                        transaction.RollBack();
                    }

                    return new ProposalExecutionResult(
                        false,
                        transactionName,
                        string.Empty,
                        null,
                        ex.ToString());
                }
            }
        }
    }
}
