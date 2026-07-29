using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using RevitAgenticAICompanion.Storage;

namespace RevitAgenticAICompanion.Runtime
{
    [SupportedOSPlatform("windows")]
    public sealed class ClaudeAgentRuntimeClient : IAgentRuntimeClient
    {
        private static readonly TimeSpan StatusTimeout = TimeSpan.FromSeconds(15);
        private static readonly TimeSpan ExecTimeout = TimeSpan.FromMinutes(8);
        private readonly LocalStoragePaths _paths;
        private readonly ProjectThreadStore _threadStore;
        private readonly ClaudeExecutableResolver _resolver;
        private readonly JsonSerializerOptions _jsonOptions;

        public ClaudeAgentRuntimeClient(LocalStoragePaths paths, ProjectThreadStore threadStore)
        {
            _paths = paths;
            _threadStore = threadStore;
            _resolver = new ClaudeExecutableResolver();
            _jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web) { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        }

        public async Task<AgentRuntimeStatus> GetStatusAsync(CancellationToken cancellationToken)
        {
            try
            {
                var resolution = await _resolver.ResolveAsync(cancellationToken);
                var auth = await CliProcessRunner.RunAsync(resolution.ExecutablePath, new[] { "auth", "status" }, null, StatusTimeout, _paths.RootPath, cancellationToken);
                var isAuthenticated = auth.ExitCode == 0;
                var detail = isAuthenticated ? "Signed in. CLI: " + resolution.Version + " | Executable: " + resolution.ExecutablePath + " | Resolver: " + resolution.Source : FirstNonEmpty(auth.StandardError.Trim(), auth.StandardOutput.Trim(), "Claude is not signed in.");
                var health = new AgentRuntimeHealthReport(true, isAuthenticated, false, detail, resolution.ExecutablePath, resolution.Version, string.Empty, string.Empty, string.Empty, string.Empty, Array.Empty<string>(), supportsConfigOverride: true, executableSource: resolution.Source, executableResolutionDiagnostics: resolution.Diagnostics);
                return new AgentRuntimeStatus("Claude", true, isAuthenticated, isAuthenticated, true, detail, health);
            }
            catch (Exception ex)
            {
                return new AgentRuntimeStatus("Claude", false, false, false, true, ex.Message);
            }
        }

        public async Task<LoginStartResult> StartLoginAsync(CancellationToken cancellationToken)
        {
            var resolution = await _resolver.ResolveAsync(cancellationToken);
            try
            {
                using (var process = new Process())
                {
                    process.StartInfo = new ProcessStartInfo { FileName = resolution.ExecutablePath, Arguments = "auth login", UseShellExecute = true, CreateNoWindow = false, WorkingDirectory = _paths.RootPath };
                    process.Start();
                }
                return new LoginStartResult(true, string.Empty, "Claude sign-in started. Complete it, then refresh auth.");
            }
            catch (Exception ex)
            {
                return new LoginStartResult(false, string.Empty, "Failed to start Claude sign-in: " + ex.Message);
            }
        }

        public async Task<ProposalCandidate> CreateProposalAsync(PlanningRequest request, RuntimeInvocationOptions runtimeOptions, CancellationToken cancellationToken)
        {
            runtimeOptions = runtimeOptions ?? RuntimeInvocationOptions.Default;
            var json = await RunPlanningTurnAsync(request.ContextSnapshot, BuildPlanningPrompt(request), runtimeOptions, cancellationToken);
            var payload = JsonSerializer.Deserialize<ClaudePlanningPayload>(json, _jsonOptions) ?? throw new InvalidOperationException("Claude returned an empty planning payload.");
            return BuildProposalCandidate(request.Prompt, payload, 0);
        }

        public async Task<ProposalCandidate> RepairProposalAsync(PlanningRequest request, ProposalCandidate failedProposal, GeneratedActionCompilationResult compilation, RuntimeInvocationOptions runtimeOptions, CancellationToken cancellationToken)
        {
            if (failedProposal == null || compilation == null || compilation.IsSuccess) return failedProposal;
            runtimeOptions = runtimeOptions ?? RuntimeInvocationOptions.Default;
            var json = await RunPlanningTurnAsync(request.ContextSnapshot, BuildRepairPrompt(request, failedProposal, compilation), runtimeOptions, cancellationToken);
            var payload = JsonSerializer.Deserialize<ClaudePlanningPayload>(json, _jsonOptions);
            return payload == null || !RequiresGeneratedCode(payload) || string.IsNullOrWhiteSpace(payload.GeneratedSource) ? failedProposal : BuildProposalCandidate(request.Prompt, payload, 1);
        }

        public async Task<ProposalCandidate> AnalyzeFailureAsync(PlanningRequest request, ProposalCandidate failedProposal, ExecutionFailurePacket failurePacket, RuntimeInvocationOptions runtimeOptions, CancellationToken cancellationToken)
        {
            runtimeOptions = runtimeOptions ?? RuntimeInvocationOptions.Default;
            var json = await RunPlanningTurnAsync(request.ContextSnapshot, BuildFailurePrompt(request, failedProposal, failurePacket), runtimeOptions, cancellationToken);
            var payload = JsonSerializer.Deserialize<ClaudePlanningPayload>(json, _jsonOptions) ?? throw new InvalidOperationException("Claude returned an empty failure-analysis payload.");
            return BuildProposalCandidate(request.Prompt, payload, failedProposal?.Provenance?.RepairCount ?? 0);
        }

        private async Task<string> RunPlanningTurnAsync(RevitContextSnapshot snapshot, string prompt, RuntimeInvocationOptions runtimeOptions, CancellationToken cancellationToken)
        {
            var status = await GetStatusAsync(cancellationToken);
            var health = status.RuntimeHealth;
            if (health == null || !health.IsAvailable || !health.IsAuthenticated) throw CreateRuntimeException("preflight", status.Detail, health, runtimeSummary: runtimeOptions.CreateSummary(status.Detail));
            var projectKey = ProjectKeyBuilder.FromSnapshot(snapshot);
            var sessionId = _threadStore.GetThreadId(AgentRuntimeProvider.Claude, projectKey);
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                sessionId = Guid.NewGuid().ToString();
                _threadStore.SetThreadId(AgentRuntimeProvider.Claude, projectKey, sessionId);
            }
            var args = new List<string> { "-p", "--output-format", "json", "--json-schema", BuildOutputSchema().ToJsonString(_jsonOptions), "--tools", string.Empty, "--disallowedTools", "mcp__*", "--permission-mode", "plan", "--session-id", sessionId };
            if (!runtimeOptions.UsesProviderDefaultReasoning)
            {
                args.Add("--effort");
                args.Add(runtimeOptions.RequestedReasoningEffort);
            }
            var result = await CliProcessRunner.RunAsync(health.ExecutablePath, args, prompt ?? string.Empty, ExecTimeout, _paths.RootPath, cancellationToken);
            if (result.ExitCode != 0) throw CreateRuntimeException("planning-exec", "Claude CLI failed during planning.", health, result.Arguments, result.ExitCode, result.StandardOutput, result.StandardError, runtimeOptions.CreateSummary(status.Detail));
            var structured = ExtractStructuredPayload(result.StandardOutput);
            if (!string.IsNullOrWhiteSpace(structured)) return structured;
            throw CreateRuntimeException("planning-parse", "Claude completed without returning a structured payload.", health, result.Arguments, result.ExitCode, result.StandardOutput, result.StandardError, runtimeOptions.CreateSummary(status.Detail));
        }

        private static string ExtractStructuredPayload(string stdout)
        {
            if (string.IsNullOrWhiteSpace(stdout)) return string.Empty;
            try
            {
                using (var doc = JsonDocument.Parse(stdout))
                {
                    var root = doc.RootElement;
                    var direct = TryExtractPayloadElement(root);
                    if (!string.IsNullOrWhiteSpace(direct)) return direct;
                    if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("result", out var result))
                    {
                        if (result.ValueKind == JsonValueKind.String)
                        {
                            var text = result.GetString() ?? string.Empty;
                            if (LooksLikeJson(text)) return text.Trim();
                        }
                        return TryExtractPayloadElement(result);
                    }
                }
            }
            catch { }
            return string.Empty;
        }

        private static string TryExtractPayloadElement(JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.Object) return string.Empty;
            if (element.TryGetProperty("structured_output", out var structured)) return structured.GetRawText();
            if (element.TryGetProperty("structuredOutput", out var camel)) return camel.GetRawText();
            return element.TryGetProperty("responseKind", out _) ? element.GetRawText() : string.Empty;
        }

        private static AgentRuntimeException CreateRuntimeException(string stage, string message, AgentRuntimeHealthReport health, string command = null, int? exitCode = null, string stdout = null, string stderr = null, RuntimeInvocationSummary runtimeSummary = null)
        {
            var stderrSummary = SummarizeText(stderr);
            var stdoutSummary = SummarizeText(stdout);
            var detail = string.Join(" | ", new[] { health?.BuildDiagnosticSummary(), exitCode.HasValue ? "Exit code: " + exitCode.Value : string.Empty, !string.IsNullOrWhiteSpace(stderrSummary) ? "stderr: " + stderrSummary : (!string.IsNullOrWhiteSpace(stdoutSummary) ? "stdout: " + stdoutSummary : string.Empty) }.Where(part => !string.IsNullOrWhiteSpace(part)));
            var combined = string.IsNullOrWhiteSpace(detail) ? message : message + " " + detail;
            return new AgentRuntimeException(combined, new AgentRuntimeFailureRecord(stage, combined, detail, health?.ExecutablePath ?? string.Empty, health?.CliVersion ?? string.Empty, string.Empty, string.Empty, string.Empty, command ?? string.Empty, exitCode, stdoutSummary, stderrSummary, runtimeSummary));
        }

        private static string BuildPlanningPrompt(PlanningRequest request)
        {
            var s = request.ContextSnapshot;
            var b = new StringBuilder();
            b.AppendLine("You are the planning runtime for a Revit add-in. Return JSON only and obey the provided output schema.");
            b.AppendLine("Use responseKind reply_only, inspection_probe, read_only_query, or action_proposal. The host owns Revit transactions.");
            b.AppendLine("Do not create Transaction or TransactionGroup objects. Use elementId.Value, not IntegerValue. Always populate every schema field.");
            b.AppendLine("Prompt:"); b.AppendLine(request.Prompt ?? string.Empty);
            b.AppendLine("DocumentTitle: " + s.DocumentTitle); b.AppendLine("DocumentPath: " + s.DocumentPath); b.AppendLine("ActiveView: " + s.ActiveViewName);
            b.AppendLine("SelectedElementIds: " + string.Join(", ", s.SelectedElementIds)); b.AppendLine("SelectedCategories: " + string.Join(", ", s.SelectedCategoryNames));
            b.AppendLine("AvailableCategories: " + string.Join(", ", s.AvailableModelCategories.Take(150)));
            b.AppendLine("User Preferences:");
            if (request.UserPreferences == null || request.UserPreferences.Count == 0) b.AppendLine("- none"); else foreach (var p in request.UserPreferences.Take(20)) b.AppendLine("- [" + p.ConfidenceLevel + "] " + p.Key + " = " + p.Value);
            b.AppendLine("Retrieved evidence:");
            if (request.RetrievedEvidence == null || request.RetrievedEvidence.Count == 0) b.AppendLine("- none yet"); else foreach (var e in request.RetrievedEvidence) b.AppendLine("- Probe " + e.ProbeOrdinal + ": " + e.Summary);
            return b.ToString();
        }

        private static string BuildRepairPrompt(PlanningRequest request, ProposalCandidate failedProposal, GeneratedActionCompilationResult compilation)
        {
            var b = new StringBuilder(BuildPlanningPrompt(request)); b.AppendLine("Repair previous generated source:"); b.AppendLine(failedProposal.GeneratedSource ?? string.Empty); foreach (var d in compilation.Diagnostics ?? Array.Empty<string>()) b.AppendLine("- " + d); return b.ToString();
        }

        private static string BuildFailurePrompt(PlanningRequest request, ProposalCandidate failedProposal, ExecutionFailurePacket failurePacket)
        {
            var b = new StringBuilder(BuildPlanningPrompt(request)); b.AppendLine("Analyze failed execution and propose one safe next step."); b.AppendLine("Exception: " + (failurePacket?.ExceptionMessage ?? string.Empty)); b.AppendLine("RawError: " + (failurePacket?.RawError ?? string.Empty)); return b.ToString();
        }

        private static JsonObject BuildOutputSchema()
        {
            return new JsonObject { ["type"] = "object", ["additionalProperties"] = false, ["required"] = new JsonArray("responseKind", "messageText", "actionSummary", "transactionName", "generatedSource", "isUndoHostile", "capabilityBand", "riskLevel", "scopeSummary", "confidenceLevel", "evidenceSummary", "probePurpose", "probeQuestion", "assumptions"), ["properties"] = new JsonObject { ["responseKind"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray("reply_only", "inspection_probe", "read_only_query", "action_proposal") }, ["messageText"] = new JsonObject { ["type"] = "string" }, ["actionSummary"] = new JsonObject { ["type"] = new JsonArray("string", "null") }, ["transactionName"] = new JsonObject { ["type"] = new JsonArray("string", "null") }, ["generatedSource"] = new JsonObject { ["type"] = new JsonArray("string", "null") }, ["isUndoHostile"] = new JsonObject { ["type"] = "boolean" }, ["capabilityBand"] = new JsonObject { ["type"] = "string" }, ["riskLevel"] = new JsonObject { ["type"] = "string" }, ["scopeSummary"] = new JsonObject { ["type"] = "string" }, ["confidenceLevel"] = new JsonObject { ["type"] = "string" }, ["evidenceSummary"] = new JsonObject { ["type"] = "string" }, ["probePurpose"] = new JsonObject { ["type"] = "string" }, ["probeQuestion"] = new JsonObject { ["type"] = "string" }, ["assumptions"] = new JsonObject { ["type"] = "array", ["items"] = new JsonObject { ["type"] = "string" } } } };
        }

        private static ProposalCandidate BuildProposalCandidate(string userPrompt, ClaudePlanningPayload p, int repairCount)
        {
            var provenance = new ProposalProvenance("Claude", repairCount);
            if (p == null || string.Equals(p.ResponseKind, "reply_only", StringComparison.OrdinalIgnoreCase)) return ProposalCandidate.CreateReply(userPrompt, p?.MessageText ?? string.Empty, p?.CapabilityBand ?? "reply", p?.RiskLevel ?? "low", p?.ScopeSummary ?? string.Empty, p?.ConfidenceLevel ?? "low", p?.Assumptions ?? Array.Empty<string>(), provenance);
            if (string.IsNullOrWhiteSpace(p.GeneratedSource)) throw new InvalidOperationException("Claude returned generated-code response without source.");
            if (string.Equals(p.ResponseKind, "inspection_probe", StringComparison.OrdinalIgnoreCase)) return ProposalCandidate.CreateInspectionProbe(userPrompt, FirstNonEmpty(p.ActionSummary, p.MessageText), p.GeneratedSource, "GeneratedActions.CompanionAction", "Execute", p.CapabilityBand, p.RiskLevel, p.ScopeSummary, p.ConfidenceLevel, p.EvidenceSummary, p.ProbePurpose, p.ProbeQuestion, p.Assumptions ?? Array.Empty<string>(), provenance);
            if (string.Equals(p.ResponseKind, "read_only_query", StringComparison.OrdinalIgnoreCase)) return ProposalCandidate.CreateReadOnlyQuery(userPrompt, FirstNonEmpty(p.ActionSummary, p.MessageText), p.GeneratedSource, "GeneratedActions.CompanionAction", "Execute", p.CapabilityBand, p.RiskLevel, p.ScopeSummary, p.ConfidenceLevel, p.EvidenceSummary, p.Assumptions ?? Array.Empty<string>(), provenance);
            return ProposalCandidate.CreateAction(userPrompt, FirstNonEmpty(p.ActionSummary, p.MessageText), p.GeneratedSource, new[] { p.TransactionName ?? string.Empty }.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray(), p.IsUndoHostile, "GeneratedActions.CompanionAction", "Execute", "Preview", p.CapabilityBand, p.RiskLevel, p.ScopeSummary, p.ConfidenceLevel, p.EvidenceSummary, p.Assumptions ?? Array.Empty<string>(), provenance);
        }

        private static bool RequiresGeneratedCode(ClaudePlanningPayload p) { return p != null && !string.Equals(p.ResponseKind, "reply_only", StringComparison.OrdinalIgnoreCase); }
        private static bool LooksLikeJson(string text) { return !string.IsNullOrWhiteSpace(text) && text.TrimStart().StartsWith("{", StringComparison.Ordinal); }
        private static string FirstNonEmpty(params string[] values) { return values == null ? string.Empty : values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty; }
        private static string SummarizeText(string text) { if (string.IsNullOrWhiteSpace(text)) return string.Empty; var line = text.Replace("\r", " ").Replace("\n", " ").Trim(); return line.Length <= 400 ? line : line.Substring(0, 400) + "..."; }

        private sealed class ClaudePlanningPayload
        {
            public string ResponseKind { get; set; }
            public string MessageText { get; set; }
            public string ActionSummary { get; set; }
            public string TransactionName { get; set; }
            public string GeneratedSource { get; set; }
            public bool IsUndoHostile { get; set; }
            public string CapabilityBand { get; set; }
            public string RiskLevel { get; set; }
            public string ScopeSummary { get; set; }
            public string ConfidenceLevel { get; set; }
            public string EvidenceSummary { get; set; }
            public string ProbePurpose { get; set; }
            public string ProbeQuestion { get; set; }
            public string[] Assumptions { get; set; }
        }
    }
}
