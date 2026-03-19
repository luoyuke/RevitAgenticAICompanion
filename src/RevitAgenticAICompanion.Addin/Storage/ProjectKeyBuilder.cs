using System;
using RevitAgenticAICompanion.Runtime;

namespace RevitAgenticAICompanion.Storage
{
    public static class ProjectKeyBuilder
    {
        public static string FromSnapshot(RevitContextSnapshot snapshot)
        {
            var path = snapshot?.DocumentPath;
            if (!string.IsNullOrWhiteSpace(path))
            {
                return path.Trim().ToLowerInvariant();
            }

            var title = snapshot?.DocumentTitle;
            if (!string.IsNullOrWhiteSpace(title))
            {
                return title.Trim().ToLowerInvariant();
            }

            return "no-document";
        }
    }
}
