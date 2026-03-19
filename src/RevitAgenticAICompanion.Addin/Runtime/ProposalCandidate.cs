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
                provenance);
        }

        public static ProposalCandidate CreateReply(
            string userPrompt,
            string replyText,
            string capabilityBand,
            string riskLevel,
            string scopeSummary,
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
        public ProposalProvenance Provenance { get; }
        public string SourceHash { get; set; }
        public string ArtifactDirectory { get; set; }

        public bool RequiresApproval
        {
            get { return ResponseKind == ProposalResponseKind.ActionProposal; }
        }

        public bool RequiresCompilation
        {
            get { return ResponseKind == ProposalResponseKind.ReadOnlyQuery || ResponseKind == ProposalResponseKind.ActionProposal; }
        }

        public bool ExecutesReadOnly
        {
            get { return ResponseKind == ProposalResponseKind.ReadOnlyQuery; }
        }

        public bool RequiresPreview
        {
            get { return ResponseKind == ProposalResponseKind.ActionProposal; }
        }
    }
}
