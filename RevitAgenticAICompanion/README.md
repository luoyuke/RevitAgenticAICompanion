# Revit Agentic AI Companion

Working project space for the new Revit app.

## Status

Architecture is locked and the first host-shell implementation is in progress.

Current code state:

- Revit 2026 add-in project scaffolded
- native dockable pane host in place
- `ExternalEvent` request dispatcher implemented
- document fingerprint tracking implemented
- local review-mode proposal generation implemented for the first schedule workflow
- generated C# compilation and diagnostics implemented for proposal review
- immediate approved execution implemented through the host-owned Revit request path

Still pending:

- Codex auth integration
- staged transaction execution
- persistent SQLite-backed memory and audit storage

## Immediate goals

- Wire the host shell to real Codex auth and runtime services.
- Add approved-code compile and execution through the host-owned Revit boundary.
- Implement staged transaction execution and audit persistence.
- Keep the first thin slice constrained to schedule-based BOQ creation.

## Working structure

- `docs/` for product, architecture, and workflow notes
