# Runtime Architecture

This document defines the MVP execution contract for the Revit Agentic AI Companion demo.

## Goals

- Keep Revit stable while the user chats in a modeless UI.
- Generate task-specific behavior at runtime.
- Preserve supported Revit execution patterns for modeless interaction.
- Require explicit approval before model changes.
- Add a second typed confirmation for undo-hostile actions.
- Persist project memory and full audit history locally.

## First Demo Workflow

The first thin-slice workflow is schedule-based quantity curation.

User intent:

- Ask for a bill of quantity for a specific category in natural language.

First-slice system behavior:

- Inspect the live document for the requested category.
- Generate a plan to create a new schedule view.
- Create the schedule in Revit with a bounded field/filter/sort set.
- Present the result summary and changed ids after the run.

First-slice non-goals:

- xlsx export
- complex external file generation
- MEP system generation
- clash resolution
- generalized arbitrary creation/manipulation workflows

Why this is first:

- It is a real BIM workflow with visible value.
- It uses normal Revit transactions and avoids undo-hostile API areas.
- It proves the end-to-end architecture without requiring advanced geometry logic.

## Components

- Revit host add-in
  - Owns startup, dockable chat pane, approval UI, and status display.
- ExternalEvent bridge
  - The only entry point for Revit API reads and writes.
- Local agent sidecar
  - Handles Codex interaction, planning, code generation, memory retrieval, and proposal assembly.
- Generated execution unit
  - Runtime-generated C# source compiled only after preflight and approval.
- Host execution coordinator
  - Owns document fingerprinting, transaction scope, staged-run control, result capture, and audit writes.
- Local storage
  - SQLite state database plus artifact files on disk.

## Current Build State

The current implementation is a host-shell review build:

- dockable pane is implemented
- `ExternalEvent` request dispatch is implemented
- Revit context capture is implemented
- document fingerprint invalidation is implemented
- local review-mode proposal generation is implemented for schedule creation
- generated source compilation and diagnostics are implemented
- immediate approved execution is implemented with a host-owned named transaction

Not implemented yet:

- Codex auth integration
- staged transaction-group execution
- SQLite-backed memory/audit persistence

## Request Flow

1. The user submits a task in the modeless chat pane.
2. The host forwards the request to the local agent sidecar.
3. If live model context is required, the sidecar asks the host for a read request.
4. The host queues the request and raises `ExternalEvent`.
5. Inside the `ExternalEvent` handler, the host gathers Revit context and returns a serialized snapshot.
6. The sidecar calls Codex and requests:
   - a human-readable action summary
   - generated C# source
   - declared transaction plan
   - undo-hostile declaration
7. The host stores the proposal as an immutable candidate artifact with a content hash.
8. The host runs preflight validation.
9. The UI shows the action summary and waits for approval.
10. If the proposal is undo-hostile, the user must type `confirm`.
11. Before execution, the host rechecks the document fingerprint. Any model edit invalidates the proposal.
12. On approval, the host compiles the exact approved source and recomputes the hash.
13. The host raises `ExternalEvent` and starts a staged run.
14. Inside the `ExternalEvent` handler, the host opens a `TransactionGroup`, then opens host-owned named transactions around execution steps.
15. The generated code runs with raw Revit API access inside the host-owned execution context.
16. The host records changed element ids, collects result data, and either:
   - allows one tweak/retry inside the open staging group, or
   - assimilates the group to finalize the run
17. The result summary is shown in the chat pane and the audit record is finalized.

## Threading And Revit Context

- The chat pane is modeless WPF and does not call Revit directly.
- The sidecar performs network/model work off the Revit thread.
- The sidecar never receives live Revit objects.
- Any operation that needs Revit API context is converted into a host request and executed through `ExternalEvent`.
- Generated code is never executed from UI callbacks or background threads.
- Generated code executes only from the host-owned `ExternalEvent` path.

## Auth Flow

For MVP, the host uses the Codex app-server auth flow instead of implementing custom desktop OAuth.

1. On startup, the host queries auth state from the local Codex app-server.
2. If the user is not signed in, the host shows a sign-in prompt.
3. Selecting sign-in starts ChatGPT login through the app-server.
4. The app-server returns a browser login URL.
5. The host opens the URL in the default browser.
6. The app-server receives the callback and updates auth state.
7. The host re-queries auth state and enables agent features when authenticated.
8. On later launches, the host reuses the cached authenticated session if it is still valid.

Unknown:

- The exact local persistence details of the Codex app-server credential cache need to be verified during implementation.

## Memory Boundary

Memory is local-only for MVP.

- Scope: per-user, per-machine, per-project
- Not shared across machines by default
- Not stored in the RVT file
- Not written directly by generated code

Recommended storage layout:

- `%LOCALAPPDATA%\\RevitAgenticAICompanion\\state.sqlite`
- `%LOCALAPPDATA%\\RevitAgenticAICompanion\\artifacts\\`

Project identity priority:

1. Stable central/cloud model identifier when available
2. Normalized model path hash

Memory categories:

- project profile
- BIM facts
- decision memory
- session memory

## Validation, Approval, And Execution

Preflight validation must check:

- source compiles
- source hash is stable
- declared transaction names are present
- generated code does not own top-level `Transaction` or `TransactionGroup` lifecycle
- document fingerprint still matches
- undo-hostile classification has been applied

Undo-hostile means at least:

- operations that cannot be rolled back normally
- link load, unload, or reload operations
- operations Autodesk documents as clearing undo history
- operations requiring all transactions to be closed before calling

Approval rules:

- normal reversible runs require one approval
- undo-hostile runs require typed `confirm`
- any source change invalidates prior approval
- any model change invalidates prior approval

For the first demo workflow, the planner should also be constrained to schedule-oriented intent and metadata so the first implementation does not expose unnecessary BIM surface area.

## Staged Run Policy

The host uses a staged run to support local review before final commit.

- A run opens a host-owned `TransactionGroup`.
- The host may allow one review/tweak/retry cycle before final assimilation.
- Retry is only valid while the staging group is still open.
- Once the group is assimilated, any further tweak becomes a new run.
- Partial success may be kept if the user accepts it.
- The host does not auto-rollback committed work unless the user explicitly requests rollback while rollback is still available.

## Audit Requirements

Every run must record:

- prompt
- retrieved memory ids
- action summary
- generated source hash
- generated source artifact path
- compile diagnostics
- validator output
- undo-hostile classification
- approval state
- transaction group name
- per-transaction names
- result summary
- exception details, if any
- changed element ids when available

## Coding Rules

- No Revit API access from UI callbacks, background tasks, or the sidecar.
- Every Revit read and write request must pass through the same host queue and `ExternalEvent` bridge.
- Generated code may use raw Revit API objects, but the host owns top-level transaction lifecycle.
- Generated code must not write persistent memory directly.
- Generated code must not write audit records directly.
- Execution must fail closed if the proposal hash or document fingerprint no longer matches.
- Unknowns must be surfaced explicitly in logs and UI rather than hidden behind fallback behavior.

## Known Risks

- Full-trust arbitrary generated C# cannot be proven safe.
- Static checks can classify risk but cannot guarantee safe behavior.
- Undo-hostile detection will be best-effort unless broad API areas are blocked.
- Allowing arbitrary references expands capability but weakens preflight confidence.
- The staged-run model is more complex than single-pass execution and must be carefully implemented to avoid user confusion.
