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
            IReadOnlyList<UserPreferenceRecord> userPreferences = null,
            int completedProbeCount = 0,
            int completedVisualProbeCount = 0,
            int maxProbeCount = 3,
            int maxVisualProbeCount = 3,
            DateTimeOffset? startedUtc = null)
        {
            Prompt = prompt;
            ContextSnapshot = contextSnapshot;
            RetrievedEvidence = retrievedEvidence ?? Array.Empty<ProbeEvidence>();
            UserPreferences = userPreferences ?? Array.Empty<UserPreferenceRecord>();
            CompletedProbeCount = completedProbeCount;
            CompletedVisualProbeCount = completedVisualProbeCount;
            MaxProbeCount = maxProbeCount;
            MaxVisualProbeCount = maxVisualProbeCount;
            StartedUtc = startedUtc ?? DateTimeOffset.UtcNow;
        }

        public string Prompt { get; }
        public RevitContextSnapshot ContextSnapshot { get; }
        public IReadOnlyList<ProbeEvidence> RetrievedEvidence { get; }
        public IReadOnlyList<UserPreferenceRecord> UserPreferences { get; }
        public int CompletedProbeCount { get; }
        public int CompletedVisualProbeCount { get; }
        public int MaxProbeCount { get; }
        public int MaxVisualProbeCount { get; }
        public DateTimeOffset StartedUtc { get; }

        public PlanningRequest WithEvidence(
            ProbeEvidence evidence,
            IReadOnlyList<UserPreferenceRecord> userPreferences)
        {
            var evidenceList = new List<ProbeEvidence>(RetrievedEvidence ?? Array.Empty<ProbeEvidence>());
            if (evidence != null)
            {
                evidenceList.Add(evidence);
            }

            var completedSemanticProbeCount = 0;
            var completedVisualProbeCount = 0;
            foreach (var item in evidenceList)
            {
                if (item == null)
                {
                    continue;
                }

                if (item.ProbeMode == ProbeMode.Visual)
                {
                    completedVisualProbeCount++;
                }
                else if (item.ProbeMode == ProbeMode.Semantic)
                {
                    completedSemanticProbeCount++;
                }
            }

            return new PlanningRequest(
                Prompt,
                ContextSnapshot,
                evidenceList,
                userPreferences ?? UserPreferences,
                completedSemanticProbeCount,
                completedVisualProbeCount,
                MaxProbeCount,
                MaxVisualProbeCount,
                StartedUtc);
        }
    }
}
