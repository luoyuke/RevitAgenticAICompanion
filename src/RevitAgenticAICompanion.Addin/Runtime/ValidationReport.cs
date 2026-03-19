using System.Collections.Generic;

namespace RevitAgenticAICompanion.Runtime
{
    public sealed class ValidationReport
    {
        public ValidationReport()
        {
            Errors = new List<string>();
            Warnings = new List<string>();
        }

        public List<string> Errors { get; }
        public List<string> Warnings { get; }
        public bool IsUndoHostile { get; set; }
        public bool IsValid
        {
            get { return Errors.Count == 0; }
        }
    }
}
