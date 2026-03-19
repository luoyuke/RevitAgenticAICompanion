using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RevitAgenticAICompanion.Runtime
{
    public sealed class LocalReviewAgentRuntimeClient : IAgentRuntimeClient
    {
        public Task<AgentRuntimeStatus> GetStatusAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new AgentRuntimeStatus(
                "Local review",
                true,
                true,
                true,
                false,
                "Using the built-in local review planner."));
        }

        public Task<LoginStartResult> StartLoginAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new LoginStartResult(false, string.Empty, "Local review mode does not use Codex sign-in."));
        }

        public Task<ProposalCandidate> CreateProposalAsync(PlanningRequest request, CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (!LooksLikeScheduleRequest(request.Prompt))
            {
                return Task.FromResult(
                    ProposalCandidate.CreateReply(
                        request.Prompt,
                        "Local review mode is available, but executable planning is limited to the schedule/BOQ demo workflow. Sign in to Codex for broader chat and planning behavior.",
                        "reply",
                        "low",
                        string.Empty,
                        "low",
                        Array.Empty<string>(),
                        Array.Empty<ProjectConventionRecord>(),
                        new ProposalProvenance("Local review", 0)));
            }

            var categoryName = ResolveCategoryName(request);
            var scheduleName = categoryName + " BOQ";
            var transactionName = "Create " + scheduleName + " schedule";

            var summary = new StringBuilder();
            summary.AppendLine("Review-mode proposal for the first demo workflow.");
            summary.AppendLine("Category: " + categoryName);
            summary.AppendLine("Action: create a new quantity schedule in the current document.");
            summary.AppendLine("Schedule name: " + scheduleName);
            summary.AppendLine("Transaction: " + transactionName);
            summary.AppendLine("Sort/filter scope: minimal default fields for the first slice.");

            var source = BuildScheduleSource(categoryName, scheduleName);
            var proposal = ProposalCandidate.CreateAction(
                request.Prompt,
                summary.ToString().Trim(),
                source,
                new[] { transactionName },
                false,
                "GeneratedActions.CompanionAction",
                "Execute",
                "Preview",
                "schedule_workflow",
                "medium",
                "Current document; category: " + categoryName,
                "medium",
                string.Empty,
                Array.Empty<string>(),
                Array.Empty<ProjectConventionRecord>(),
                new ProposalProvenance("Local review", 0));

            return Task.FromResult(proposal);
        }

        public Task<ProposalCandidate> RepairProposalAsync(
            PlanningRequest request,
            ProposalCandidate failedProposal,
            GeneratedActionCompilationResult compilation,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(failedProposal);
        }

        private static string ResolveCategoryName(PlanningRequest request)
        {
            if (request.ContextSnapshot.SelectedCategoryNames.Count > 0)
            {
                return request.ContextSnapshot.SelectedCategoryNames[0];
            }

            var prompt = request.Prompt ?? string.Empty;
            var availableCategories = request.ContextSnapshot.AvailableModelCategories;
            var availableMatch = availableCategories.FirstOrDefault(category =>
                prompt.IndexOf(category, StringComparison.OrdinalIgnoreCase) >= 0);
            if (!string.IsNullOrWhiteSpace(availableMatch))
            {
                return availableMatch;
            }

            availableMatch = availableCategories.FirstOrDefault(category =>
                category.IndexOf(prompt, StringComparison.OrdinalIgnoreCase) >= 0);
            if (!string.IsNullOrWhiteSpace(availableMatch))
            {
                return availableMatch;
            }

            var knownCategories = new[]
            {
                "Walls",
                "Doors",
                "Windows",
                "Floors",
                "Rooms",
                "Furniture",
                "Curtain Panels",
                "Mechanical Equipment",
                "Pipes",
                "Pipe Fittings",
                "Pipe Accessories",
            };

            var match = knownCategories.FirstOrDefault(category =>
                prompt.IndexOf(category, StringComparison.OrdinalIgnoreCase) >= 0);

            if (!string.IsNullOrWhiteSpace(match))
            {
                return match;
            }

            var promptTerms = prompt
                .Split(new[] { ' ', '\t', '\r', '\n', ',', '.', ':', ';', '-', '_', '/' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(term => term.Length >= 3)
                .ToArray();

            availableMatch = availableCategories.FirstOrDefault(category =>
                promptTerms.Any(term =>
                    category.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    term.IndexOf(category, StringComparison.OrdinalIgnoreCase) >= 0));
            if (!string.IsNullOrWhiteSpace(availableMatch))
            {
                return availableMatch;
            }

            return availableCategories.FirstOrDefault() ?? "Walls";
        }

        private static bool LooksLikeScheduleRequest(string prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt))
            {
                return false;
            }

            var text = prompt;
            return text.IndexOf("schedule", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("boq", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("bill of quantity", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("quantity", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string BuildScheduleSource(string categoryName, string scheduleName)
        {
            return
@"using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitAgenticAICompanion.Runtime;

namespace GeneratedActions
{
    public static class CompanionAction
    {
        public static GeneratedActionResult Preview(UIApplication uiapp)
        {
            if (uiapp == null) throw new ArgumentNullException(""uiapp"");
            var uidoc = uiapp.ActiveUIDocument ?? throw new InvalidOperationException(""No active document."");
            var doc = uidoc.Document;

            var category = ResolveCategory(doc, """ + EscapeString(categoryName) + @""");
            if (category == null)
            {
                throw new InvalidOperationException(""Category not found: " + EscapeString(categoryName) + @""");
            }

            return new GeneratedActionResult(
                ""Preview: create or replace schedule '" + EscapeString(scheduleName) + @"' for category '" + EscapeString(categoryName) + @"'. "",
                System.Array.Empty<long>());
        }

        public static GeneratedActionResult Execute(UIApplication uiapp)
        {
            if (uiapp == null) throw new ArgumentNullException(nameof(uiapp));
            var uidoc = uiapp.ActiveUIDocument ?? throw new InvalidOperationException(""No active document."");
            var doc = uidoc.Document;

            var category = ResolveCategory(doc, """ + EscapeString(categoryName) + @""");
            if (category == null)
            {
                throw new InvalidOperationException(""Category not found: " + EscapeString(categoryName) + @""");
            }

            var schedule = ViewSchedule.CreateSchedule(doc, category.Id);
            schedule.Name = """ + EscapeString(scheduleName) + @""";

            return new GeneratedActionResult(
                ""Created schedule '" + EscapeString(scheduleName) + @"'. "",
                new long[] { schedule.Id.Value });
        }

        private static Category ResolveCategory(Document doc, string categoryName)
        {
            foreach (Category category in doc.Settings.Categories)
            {
                if (category != null && string.Equals(category.Name, categoryName, StringComparison.OrdinalIgnoreCase))
                {
                    return category;
                }
            }

            return null;
        }
    }
}";
        }

        private static string EscapeString(string value)
        {
            return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
