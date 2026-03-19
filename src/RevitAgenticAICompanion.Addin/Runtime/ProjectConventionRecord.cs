namespace RevitAgenticAICompanion.Runtime
{
    public sealed class ProjectConventionRecord
    {
        public ProjectConventionRecord(
            string conventionType,
            string name,
            string value,
            string confidenceLevel,
            string rationale,
            string source)
        {
            ConventionType = conventionType ?? string.Empty;
            Name = name ?? string.Empty;
            Value = value ?? string.Empty;
            ConfidenceLevel = confidenceLevel ?? string.Empty;
            Rationale = rationale ?? string.Empty;
            Source = source ?? string.Empty;
        }

        public string ConventionType { get; }
        public string Name { get; }
        public string Value { get; }
        public string ConfidenceLevel { get; }
        public string Rationale { get; }
        public string Source { get; }
    }
}
