using System.Collections.Generic;
using Autodesk.Revit.DB;
using RevitAgenticAICompanion.Infrastructure;
using RevitAgenticAICompanion.Runtime;

namespace RevitAgenticAICompanion.Revit.Requests
{
    public sealed class CaptureContextSnapshotRequest : RevitRequest<RevitContextSnapshot>
    {
        private readonly DocumentStateTracker _documentStateTracker;

        public CaptureContextSnapshotRequest(DocumentStateTracker documentStateTracker)
        {
            _documentStateTracker = documentStateTracker;
        }

        protected override RevitContextSnapshot ExecuteCore(RevitRequestContext context)
        {
            if (context.Document == null || context.UIDocument == null)
            {
                return RevitContextSnapshot.Empty("No active document.");
            }

            var selectedElementIds = new List<int>();
            var selectedCategories = new List<string>();
            foreach (var elementId in context.UIDocument.Selection.GetElementIds())
            {
                selectedElementIds.Add((int)elementId.Value);
                var element = context.Document.GetElement(elementId);
                var categoryName = element?.Category?.Name;
                if (!string.IsNullOrWhiteSpace(categoryName) && !selectedCategories.Contains(categoryName))
                {
                    selectedCategories.Add(categoryName);
                }
            }

            var modelCategories = new List<string>();
            foreach (Category category in context.Document.Settings.Categories)
            {
                if (category == null || category.CategoryType != CategoryType.Model || string.IsNullOrWhiteSpace(category.Name))
                {
                    continue;
                }

                modelCategories.Add(category.Name);
            }

            modelCategories.Sort();
            var fingerprint = _documentStateTracker.Capture(context.Document);

            return new RevitContextSnapshot(
                context.Document.Title,
                context.Document.PathName,
                context.Document.ActiveView?.Name ?? string.Empty,
                selectedElementIds,
                selectedCategories,
                modelCategories,
                fingerprint,
                null);
        }
    }
}
