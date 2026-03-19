namespace RevitAgenticAICompanion.Runtime
{
    public sealed class ProposalProvenance
    {
        public ProposalProvenance(string plannerName, int repairCount)
        {
            PlannerName = string.IsNullOrWhiteSpace(plannerName) ? "Unknown" : plannerName;
            RepairCount = repairCount < 0 ? 0 : repairCount;
        }

        public string PlannerName { get; }
        public int RepairCount { get; }

        public string Summary
        {
            get
            {
                return RepairCount <= 0
                    ? PlannerName
                    : PlannerName + " (repaired " + RepairCount + "x)";
            }
        }
    }
}
