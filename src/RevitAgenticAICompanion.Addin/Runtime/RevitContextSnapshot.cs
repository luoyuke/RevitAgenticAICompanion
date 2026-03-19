using System.Collections.Generic;
using RevitAgenticAICompanion.Infrastructure;

namespace RevitAgenticAICompanion.Runtime
{
    public sealed class RevitContextSnapshot
    {
        public RevitContextSnapshot(
            string documentTitle,
            string documentPath,
            string activeViewName,
            IReadOnlyList<int> selectedElementIds,
            IReadOnlyList<string> selectedCategoryNames,
            IReadOnlyList<string> availableModelCategories,
            DocumentFingerprint fingerprint,
            string error)
        {
            DocumentTitle = documentTitle ?? string.Empty;
            DocumentPath = documentPath ?? string.Empty;
            ActiveViewName = activeViewName ?? string.Empty;
            SelectedElementIds = selectedElementIds ?? new int[0];
            SelectedCategoryNames = selectedCategoryNames ?? new string[0];
            AvailableModelCategories = availableModelCategories ?? new string[0];
            Fingerprint = fingerprint ?? new DocumentFingerprint("no-document", 0);
            Error = error;
        }

        public string DocumentTitle { get; }
        public string DocumentPath { get; }
        public string ActiveViewName { get; }
        public IReadOnlyList<int> SelectedElementIds { get; }
        public IReadOnlyList<string> SelectedCategoryNames { get; }
        public IReadOnlyList<string> AvailableModelCategories { get; }
        public DocumentFingerprint Fingerprint { get; }
        public string Error { get; }
        public bool HasDocument
        {
            get { return string.IsNullOrWhiteSpace(Error); }
        }

        public static RevitContextSnapshot Empty(string error)
        {
            return new RevitContextSnapshot(string.Empty, string.Empty, string.Empty, null, null, null, null, error);
        }
    }
}
