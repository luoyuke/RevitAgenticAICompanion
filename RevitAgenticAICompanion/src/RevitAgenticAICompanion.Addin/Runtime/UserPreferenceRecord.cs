using System;

namespace RevitAgenticAICompanion.Runtime
{
    public sealed class UserPreferenceRecord
    {
        public UserPreferenceRecord(
            string key,
            string value,
            string confidenceLevel,
            string source,
            DateTimeOffset lastUpdatedUtc)
        {
            Key = key ?? string.Empty;
            Value = value ?? string.Empty;
            ConfidenceLevel = confidenceLevel ?? string.Empty;
            Source = source ?? string.Empty;
            LastUpdatedUtc = lastUpdatedUtc;
        }

        public string Key { get; }
        public string Value { get; }
        public string ConfidenceLevel { get; }
        public string Source { get; }
        public DateTimeOffset LastUpdatedUtc { get; }
    }
}
