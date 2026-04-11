using System;
using System.Collections.Generic;

namespace RevitAgenticAICompanion.Runtime
{
    public sealed class ProposalCandidate
    {
        private ProposalCandidate(
            string userPrompt,
            ProposalResponseKind responseKind,
            string replyText,
            string actionSummary,
            string generatedSource,
            IReadOnlyList<string> transactionNames,
            bool isUndoHostile,
            string entryPointTypeName,
            string entryPointMethodName,
            string previewMethodName,
            string capabilityBand,
            string riskLevel,
            string scopeSummary,
            string confidenceLevel,
            string evidenceSummary,
            ProbeMode probeMode,
            string probePurpose,
            string probeQuestion,
            string whySemanticIsInsufficient,
            IReadOnlyList<string> assumptions,
            ProposalProvenance provenance)
        {
            ProposalId = Guid.NewGuid().ToString("N");
            UserPrompt = userPrompt;
            ResponseKind = responseKind;
            ReplyText = replyText ?? string.Empty;
            ActionSummary = actionSummary;
            GeneratedSource = generatedSource;
            TransactionNames = transactionNames ?? new string[0];
            IsUndoHostile = isUndoHostile;
            EntryPointTypeName = entryPointTypeName ?? string.Empty;
            EntryPointMethodName = entryPointMethodName ?? string.Empty;
            PreviewMethodName = previewMethodName ?? string.Empty;
            CapabilityBand = capabilityBand ?? string.Empty;
            RiskLevel = riskLevel ?? string.Empty;
            ScopeSummary = scopeSummary ?? string.Empty;
            ConfidenceLevel = confidenceLevel ?? string.Empty;
            EvidenceSummary = evidenceSummary ?? string.Empty;
            ProbeMode = probeMode;
            ProbePurpose = probePurpose ?? string.Empty;
            ProbeQuestion = probeQuestion ?? string.Empty;
            WhySemanticIsInsufficient = whySemanticIsInsufficient ?? string.Empty;
            Assumptions = assumptions ?? new string[0];
            Provenance = provenance ?? new ProposalProvenance("Unknown", 0);
        }

        public static ProposalCandidate CreateAction(
            string userPrompt,
            string actionSummary,
            string generatedSource,
            IReadOnlyList<string> transactionNames,
            bool isUndoHostile,
            string entryPointTypeName,
            string entryPointMethodName,
            string previewMethodName,
            string capabilityBand,
            string riskLevel,
            string scopeSummary,
            string confidenceLevel,
            string evidenceSummary,
            IReadOnlyList<string> assumptions,
            ProposalProvenance provenance)
        {
            return new ProposalCandidate(
                userPrompt,
                ProposalResponseKind.ActionProposal,
                string.Empty,
                actionSummary,
                generatedSource,
                transactionNames,
                isUndoHostile,
                entryPointTypeName,
                entryPointMethodName,
                previewMethodName,
                capabilityBand,
                riskLevel,
                scopeSummary,
                confidenceLevel,
                evidenceSummary,
                ProbeMode.None,
                string.Empty,
                string.Empty,
                string.Empty,
                assumptions,
                provenance);
        }

        public static ProposalCandidate CreateInspectionProbe(
            string userPrompt,
            string actionSummary,
            string generatedSource,
            string entryPointTypeName,
            string entryPointMethodName,
            string capabilityBand,
            string riskLevel,
            string scopeSummary,
            string confidenceLevel,
            string evidenceSummary,
            ProbeMode probeMode,
            string probePurpose,
            string probeQuestion,
            string whySemanticIsInsufficient,
            IReadOnlyList<string> assumptions,
            ProposalProvenance provenance)
        {
            return new ProposalCandidate(
                userPrompt,
                ProposalResponseKind.InspectionProbe,
                string.Empty,
                actionSummary,
                generatedSource,
                new string[0],
                false,
                entryPointTypeName,
                entryPointMethodName,
                string.Empty,
                capabilityBand,
                riskLevel,
                scopeSummary,
                confidenceLevel,
                evidenceSummary,
                probeMode,
                probePurpose,
                probeQuestion,
                whySemanticIsInsufficient,
                assumptions,
                provenance);
        }

        public static ProposalCandidate CreateReadOnlyQuery(
            string userPrompt,
            string actionSummary,
            string generatedSource,
            string entryPointTypeName,
            string entryPointMethodName,
            string capabilityBand,
            string riskLevel,
            string scopeSummary,
            string confidenceLevel,
            string evidenceSummary,
            IReadOnlyList<string> assumptions,
            ProposalProvenance provenance)
        {
            return new ProposalCandidate(
                userPrompt,
                ProposalResponseKind.ReadOnlyQuery,
                string.Empty,
                actionSummary,
                generatedSource,
                new string[0],
                false,
                entryPointTypeName,
                entryPointMethodName,
                string.Empty,
                capabilityBand,
                riskLevel,
                scopeSummary,
                confidenceLevel,
                evidenceSummary,
                ProbeMode.None,
                string.Empty,
                string.Empty,
                string.Empty,
                assumptions,
                provenance);
        }

        public static ProposalCandidate CreateReply(
            string userPrompt,
            string replyText,
            string capabilityBand,
            string riskLevel,
            string scopeSummary,
            string confidenceLevel,
            IReadOnlyList<string> assumptions,
            ProposalProvenance provenance)
        {
            return new ProposalCandidate(
                userPrompt,
                ProposalResponseKind.ReplyOnly,
                replyText,
                replyText,
                string.Empty,
                new string[0],
                false,
                string.Empty,
                string.Empty,
                string.Empty,
                capabilityBand,
                riskLevel,
                scopeSummary,
                confidenceLevel,
                string.Empty,
                ProbeMode.None,
                string.Empty,
                string.Empty,
                string.Empty,
                assumptions,
                provenance);
        }

        public string ProposalId { get; }
        public string UserPrompt { get; }
        public ProposalResponseKind ResponseKind { get; }
        public string ReplyText { get; }
        public string ActionSummary { get; }
        public string GeneratedSource { get; }
        public IReadOnlyList<string> TransactionNames { get; }
        public bool IsUndoHostile { get; }
        public string EntryPointTypeName { get; }
        public string EntryPointMethodName { get; }
        public string PreviewMethodName { get; }
        public string CapabilityBand { get; }
        public string RiskLevel { get; }
        public string ScopeSummary { get; }
        public string ConfidenceLevel { get; }
        public string EvidenceSummary { get; }
        public ProbeMode ProbeMode { get; }
        public string ProbePurpose { get; }
        public string ProbeQuestion { get; }
        public string WhySemanticIsInsufficient { get; }
        public IReadOnlyList<string> Assumptions { get; }
        public ProposalProvenance Provenance { get; }
        public string SourceHash { get; set; }
        public string ArtifactDirectory { get; set; }

        public bool RequiresApproval
        {
            get { return ResponseKind == ProposalResponseKind.ActionProposal; }
        }

        public bool RequiresCompilation
        {
            get
            {
                return ResponseKind == ProposalResponseKind.InspectionProbe
                    || ResponseKind == ProposalResponseKind.ReadOnlyQuery
                    || ResponseKind == ProposalResponseKind.ActionProposal;
            }
        }

        public bool ExecutesReadOnly
        {
            get { return ResponseKind == ProposalResponseKind.InspectionProbe || ResponseKind == ProposalResponseKind.ReadOnlyQuery; }
        }

        public bool ContinuesPlanning
        {
            get { return ResponseKind == ProposalResponseKind.InspectionProbe; }
        }

        public bool RequiresPreview
        {
            get { return ResponseKind == ProposalResponseKind.ActionProposal; }
        }
    }
}
