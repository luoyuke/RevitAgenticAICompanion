namespace RevitAgenticAICompanion.Infrastructure
{
    public sealed class DocumentFingerprint
    {
        public DocumentFingerprint(string documentKey, int revision)
        {
            DocumentKey = documentKey;
            Revision = revision;
        }

        public string DocumentKey { get; }
        public int Revision { get; }

        public bool Matches(DocumentFingerprint other)
        {
            return other != null
                && string.Equals(DocumentKey, other.DocumentKey, System.StringComparison.Ordinal)
                && Revision == other.Revision;
        }

        public override string ToString()
        {
            return DocumentKey + "@" + Revision;
        }
    }
}
