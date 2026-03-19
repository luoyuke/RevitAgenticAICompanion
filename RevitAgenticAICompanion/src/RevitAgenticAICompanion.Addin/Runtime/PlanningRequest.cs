using System;
using System.Collections.Generic;

namespace RevitAgenticAICompanion.Runtime
{
    public sealed class PlanningRequest
    {
        public PlanningRequest(
            string prompt,
            RevitContextSnapshot contextSnapshot,
            IReadOnlyList<ProbeEvidence> retrievedEvidence = null,
            IReadOnlyList<ProjectConventionRecord> projectConventions = null,
            int completedProbeCount = 0,
            int maxProbeCount = 3,
            DateTimeOffset? startedUtc = null)
        {
            Prompt = prompt;
            ContextSnapshot = contextSnapshot;
            RetrievedEvidence = retrievedEvidence ?? Array.Empty<ProbeEvidence>();
            ProjectConventions = projectConventions ?? Array.Empty<ProjectConventionRecord>();
            CompletedProbeCount = completedProbeCount;
            MaxProbeCount = maxProbeCount;
            StartedUtc = startedUtc ?? DateTimeOffset.UtcNow;
        }

        public string Prompt { get; }
        public RevitContextSnapshot ContextSnapshot { get; }
        public IReadOnlyList<ProbeEvidence> RetrievedEvidence { get; }
        public IReadOnlyList<ProjectConventionRecord> ProjectConventions { get; }
        public int CompletedProbeCount { get; }
        public int MaxProbeCount { get; }
        public DateTimeOffset StartedUtc { get; }

        public PlanningRequest WithEvidence(
            ProbeEvidence evidence,
            IReadOnlyList<ProjectConventionRecord> projectConventions)
        {
            var evidenceList = new List<ProbeEvidence>(RetrievedEvidence ?? Array.Empty<ProbeEvidence>());
            if (evidence != null)
            {
                evidenceList.Add(evidence);
            }

            return new PlanningRequest(
                Prompt,
                ContextSnapshot,
                evidenceList,
                projectConventions ?? ProjectConventions,
                evidenceList.Count,
                MaxProbeCount,
                StartedUtc);
        }
    }
}
