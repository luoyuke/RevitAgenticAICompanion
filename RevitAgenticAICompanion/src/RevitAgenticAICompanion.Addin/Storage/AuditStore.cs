using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using RevitAgenticAICompanion.Runtime;

namespace RevitAgenticAICompanion.Storage
{
    public sealed class AuditStore
    {
        private readonly string _connectionString;

        public AuditStore(LocalStoragePaths paths)
        {
            _connectionString = "Data Source=" + paths.AuditDatabasePath;
            EnsureCreated();
        }

        public void WritePlanning(PlanningSession session)
        {
            if (session == null)
            {
                return;
            }

            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText =
@"INSERT INTO audit_runs (
    run_id, created_utc, updated_utc, proposal_kind, planner_name, repair_count,
    user_prompt, reply_text, action_summary, capability_band, risk_level, scope_summary, confidence_level, evidence_summary, source_hash, artifact_directory,
    document_title, document_path, active_view_name, document_fingerprint,
    selected_element_ids_json, selected_category_names_json, available_category_count,
    validation_is_valid, compilation_is_success, undo_hostile, is_approved,
    preview_is_success, preview_summary, preview_target_element_ids_json, preview_error,
    transaction_names_json, assumptions_json, probe_count, probe_evidence_json, project_conventions_json, discovered_conventions_json, diagnostics_json)
VALUES (
    $run_id, COALESCE((SELECT created_utc FROM audit_runs WHERE run_id = $run_id), $now), $now,
    $proposal_kind, $planner_name, $repair_count,
    $user_prompt, $reply_text, $action_summary, $capability_band, $risk_level, $scope_summary, $confidence_level, $evidence_summary, $source_hash, $artifact_directory,
    $document_title, $document_path, $active_view_name, $document_fingerprint,
    $selected_element_ids_json, $selected_category_names_json, $available_category_count,
    $validation_is_valid, $compilation_is_success, $undo_hostile, $is_approved,
    $preview_is_success, $preview_summary, $preview_target_element_ids_json, $preview_error,
    $transaction_names_json, $assumptions_json, $probe_count, $probe_evidence_json, $project_conventions_json, $discovered_conventions_json, $diagnostics_json)
ON CONFLICT(run_id) DO UPDATE SET
    updated_utc = excluded.updated_utc,
    proposal_kind = excluded.proposal_kind,
    planner_name = excluded.planner_name,
    repair_count = excluded.repair_count,
    user_prompt = excluded.user_prompt,
    reply_text = excluded.reply_text,
    action_summary = excluded.action_summary,
    capability_band = excluded.capability_band,
    risk_level = excluded.risk_level,
    scope_summary = excluded.scope_summary,
    confidence_level = excluded.confidence_level,
    evidence_summary = excluded.evidence_summary,
    source_hash = excluded.source_hash,
    artifact_directory = excluded.artifact_directory,
    document_title = excluded.document_title,
    document_path = excluded.document_path,
    active_view_name = excluded.active_view_name,
    document_fingerprint = excluded.document_fingerprint,
    selected_element_ids_json = excluded.selected_element_ids_json,
    selected_category_names_json = excluded.selected_category_names_json,
    available_category_count = excluded.available_category_count,
    validation_is_valid = excluded.validation_is_valid,
    compilation_is_success = excluded.compilation_is_success,
    undo_hostile = excluded.undo_hostile,
    is_approved = excluded.is_approved,
    preview_is_success = excluded.preview_is_success,
    preview_summary = excluded.preview_summary,
    preview_target_element_ids_json = excluded.preview_target_element_ids_json,
    preview_error = excluded.preview_error,
    transaction_names_json = excluded.transaction_names_json,
    assumptions_json = excluded.assumptions_json,
    probe_count = excluded.probe_count,
    probe_evidence_json = excluded.probe_evidence_json,
    project_conventions_json = excluded.project_conventions_json,
    discovered_conventions_json = excluded.discovered_conventions_json,
    diagnostics_json = excluded.diagnostics_json;";

                    BindPlanningParameters(command, session);
                    command.ExecuteNonQuery();
                }
            }
        }

        public void WriteExecution(PlanningSession session, ProposalExecutionResult execution)
        {
            if (session == null || execution == null)
            {
                return;
            }

            WritePlanning(session);

            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText =
@"UPDATE audit_runs
SET updated_utc = $now,
    execution_is_success = $execution_is_success,
    execution_transaction_name = $execution_transaction_name,
    changed_element_ids_json = $changed_element_ids_json,
    execution_summary = $execution_summary,
    execution_error = $execution_error
WHERE run_id = $run_id;";
                    command.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
                    command.Parameters.AddWithValue("$run_id", session.Proposal.ProposalId);
                    command.Parameters.AddWithValue("$execution_is_success", execution.IsSuccess ? 1 : 0);
                    command.Parameters.AddWithValue("$execution_transaction_name", execution.TransactionName ?? string.Empty);
                    command.Parameters.AddWithValue("$changed_element_ids_json", JsonSerializer.Serialize(execution.ChangedElementIds ?? Array.Empty<long>()));
                    command.Parameters.AddWithValue("$execution_summary", execution.Summary ?? string.Empty);
                    command.Parameters.AddWithValue("$execution_error", execution.Error ?? string.Empty);
                    command.ExecuteNonQuery();
                }
            }
        }

        private void EnsureCreated()
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText =
@"CREATE TABLE IF NOT EXISTS audit_runs (
    run_id TEXT PRIMARY KEY,
    created_utc TEXT NOT NULL,
    updated_utc TEXT NOT NULL,
    proposal_kind TEXT NOT NULL,
    planner_name TEXT NOT NULL,
    repair_count INTEGER NOT NULL,
    user_prompt TEXT NOT NULL,
    reply_text TEXT NOT NULL,
    action_summary TEXT NOT NULL,
    capability_band TEXT NOT NULL DEFAULT '',
    risk_level TEXT NOT NULL DEFAULT '',
    scope_summary TEXT NOT NULL DEFAULT '',
    confidence_level TEXT NOT NULL DEFAULT '',
    evidence_summary TEXT NOT NULL DEFAULT '',
    source_hash TEXT NOT NULL,
    artifact_directory TEXT NOT NULL,
    document_title TEXT NOT NULL,
    document_path TEXT NOT NULL,
    active_view_name TEXT NOT NULL DEFAULT '',
    document_fingerprint TEXT NOT NULL,
    selected_element_ids_json TEXT NOT NULL DEFAULT '[]',
    selected_category_names_json TEXT NOT NULL DEFAULT '[]',
    available_category_count INTEGER NOT NULL DEFAULT 0,
    validation_is_valid INTEGER NOT NULL,
    compilation_is_success INTEGER NOT NULL,
    undo_hostile INTEGER NOT NULL,
    is_approved INTEGER NOT NULL,
    preview_is_success INTEGER NULL,
    preview_summary TEXT NULL,
    preview_target_element_ids_json TEXT NULL,
    preview_error TEXT NULL,
    execution_is_success INTEGER NULL,
    execution_transaction_name TEXT NULL,
    transaction_names_json TEXT NOT NULL,
    assumptions_json TEXT NOT NULL DEFAULT '[]',
    probe_count INTEGER NOT NULL DEFAULT 0,
    probe_evidence_json TEXT NOT NULL DEFAULT '[]',
    project_conventions_json TEXT NOT NULL DEFAULT '[]',
    discovered_conventions_json TEXT NOT NULL DEFAULT '[]',
    changed_element_ids_json TEXT NULL,
    execution_summary TEXT NULL,
    execution_error TEXT NULL,
    diagnostics_json TEXT NOT NULL
);";
                    command.ExecuteNonQuery();
                }

                EnsureColumn(connection, "audit_runs", "active_view_name", "TEXT NOT NULL DEFAULT ''");
                EnsureColumn(connection, "audit_runs", "selected_element_ids_json", "TEXT NOT NULL DEFAULT '[]'");
                EnsureColumn(connection, "audit_runs", "selected_category_names_json", "TEXT NOT NULL DEFAULT '[]'");
                EnsureColumn(connection, "audit_runs", "available_category_count", "INTEGER NOT NULL DEFAULT 0");
                EnsureColumn(connection, "audit_runs", "capability_band", "TEXT NOT NULL DEFAULT ''");
                EnsureColumn(connection, "audit_runs", "risk_level", "TEXT NOT NULL DEFAULT ''");
                EnsureColumn(connection, "audit_runs", "scope_summary", "TEXT NOT NULL DEFAULT ''");
                EnsureColumn(connection, "audit_runs", "confidence_level", "TEXT NOT NULL DEFAULT ''");
                EnsureColumn(connection, "audit_runs", "evidence_summary", "TEXT NOT NULL DEFAULT ''");
                EnsureColumn(connection, "audit_runs", "preview_is_success", "INTEGER NULL");
                EnsureColumn(connection, "audit_runs", "preview_summary", "TEXT NULL");
                EnsureColumn(connection, "audit_runs", "preview_target_element_ids_json", "TEXT NULL");
                EnsureColumn(connection, "audit_runs", "preview_error", "TEXT NULL");
                EnsureColumn(connection, "audit_runs", "execution_transaction_name", "TEXT NULL");
                EnsureColumn(connection, "audit_runs", "assumptions_json", "TEXT NOT NULL DEFAULT '[]'");
                EnsureColumn(connection, "audit_runs", "probe_count", "INTEGER NOT NULL DEFAULT 0");
                EnsureColumn(connection, "audit_runs", "probe_evidence_json", "TEXT NOT NULL DEFAULT '[]'");
                EnsureColumn(connection, "audit_runs", "project_conventions_json", "TEXT NOT NULL DEFAULT '[]'");
                EnsureColumn(connection, "audit_runs", "discovered_conventions_json", "TEXT NOT NULL DEFAULT '[]'");
            }
        }

        private static void BindPlanningParameters(SqliteCommand command, PlanningSession session)
        {
            var proposal = session.Proposal;
            var now = DateTime.UtcNow.ToString("O");
            command.Parameters.AddWithValue("$run_id", proposal.ProposalId);
            command.Parameters.AddWithValue("$now", now);
            command.Parameters.AddWithValue("$proposal_kind", proposal.ResponseKind.ToString());
            command.Parameters.AddWithValue("$planner_name", proposal.Provenance?.PlannerName ?? "Unknown");
            command.Parameters.AddWithValue("$repair_count", proposal.Provenance?.RepairCount ?? 0);
            command.Parameters.AddWithValue("$user_prompt", proposal.UserPrompt ?? string.Empty);
            command.Parameters.AddWithValue("$reply_text", proposal.ReplyText ?? string.Empty);
            command.Parameters.AddWithValue("$action_summary", proposal.ActionSummary ?? string.Empty);
            command.Parameters.AddWithValue("$capability_band", proposal.CapabilityBand ?? string.Empty);
            command.Parameters.AddWithValue("$risk_level", proposal.RiskLevel ?? string.Empty);
            command.Parameters.AddWithValue("$scope_summary", proposal.ScopeSummary ?? string.Empty);
            command.Parameters.AddWithValue("$confidence_level", proposal.ConfidenceLevel ?? string.Empty);
            command.Parameters.AddWithValue("$evidence_summary", proposal.EvidenceSummary ?? string.Empty);
            command.Parameters.AddWithValue("$source_hash", proposal.SourceHash ?? string.Empty);
            command.Parameters.AddWithValue("$artifact_directory", proposal.ArtifactDirectory ?? string.Empty);
            command.Parameters.AddWithValue("$document_title", session.ContextSnapshot?.DocumentTitle ?? string.Empty);
            command.Parameters.AddWithValue("$document_path", session.ContextSnapshot?.DocumentPath ?? string.Empty);
            command.Parameters.AddWithValue("$active_view_name", session.ContextSnapshot?.ActiveViewName ?? string.Empty);
            command.Parameters.AddWithValue("$document_fingerprint", session.ContextSnapshot?.Fingerprint?.ToString() ?? string.Empty);
            command.Parameters.AddWithValue("$selected_element_ids_json", JsonSerializer.Serialize(session.ContextSnapshot?.SelectedElementIds ?? Array.Empty<int>()));
            command.Parameters.AddWithValue("$selected_category_names_json", JsonSerializer.Serialize(session.ContextSnapshot?.SelectedCategoryNames ?? Array.Empty<string>()));
            command.Parameters.AddWithValue("$available_category_count", session.ContextSnapshot?.AvailableModelCategories?.Count ?? 0);
            command.Parameters.AddWithValue("$validation_is_valid", session.ValidationReport != null && session.ValidationReport.IsValid ? 1 : 0);
            command.Parameters.AddWithValue("$compilation_is_success", session.CompilationResult != null && session.CompilationResult.IsSuccess ? 1 : 0);
            command.Parameters.AddWithValue("$undo_hostile", session.ValidationReport != null && session.ValidationReport.IsUndoHostile ? 1 : 0);
            command.Parameters.AddWithValue("$is_approved", session.IsApproved ? 1 : 0);
            command.Parameters.AddWithValue("$preview_is_success", session.PreviewResult == null ? (object)DBNull.Value : (session.PreviewResult.IsSuccess ? 1 : 0));
            command.Parameters.AddWithValue("$preview_summary", session.PreviewResult?.Summary ?? string.Empty);
            command.Parameters.AddWithValue("$preview_target_element_ids_json", JsonSerializer.Serialize(session.PreviewResult?.TargetElementIds ?? Array.Empty<long>()));
            command.Parameters.AddWithValue("$preview_error", session.PreviewResult?.Error ?? string.Empty);
            command.Parameters.AddWithValue("$transaction_names_json", JsonSerializer.Serialize(proposal.TransactionNames ?? Array.Empty<string>()));
            command.Parameters.AddWithValue("$assumptions_json", JsonSerializer.Serialize(proposal.Assumptions ?? Array.Empty<string>()));
            command.Parameters.AddWithValue("$probe_count", session.RetrievedEvidence?.Count ?? 0);
            command.Parameters.AddWithValue("$probe_evidence_json", JsonSerializer.Serialize(session.RetrievedEvidence ?? Array.Empty<ProbeEvidence>()));
            command.Parameters.AddWithValue("$project_conventions_json", "[]");
            command.Parameters.AddWithValue("$discovered_conventions_json", "[]");
            command.Parameters.AddWithValue("$diagnostics_json", JsonSerializer.Serialize(session.CompilationResult?.Diagnostics ?? Array.Empty<string>()));
        }

        private static void EnsureColumn(SqliteConnection connection, string tableName, string columnName, string columnDefinition)
        {
            var existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var pragma = connection.CreateCommand())
            {
                pragma.CommandText = "PRAGMA table_info(" + tableName + ");";
                using (var reader = pragma.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        existingColumns.Add(reader.GetString(1));
                    }
                }
            }

            if (existingColumns.Contains(columnName))
            {
                return;
            }

            using (var alter = connection.CreateCommand())
            {
                alter.CommandText = "ALTER TABLE " + tableName + " ADD COLUMN " + columnName + " " + columnDefinition + ";";
                alter.ExecuteNonQuery();
            }
        }
    }
}
