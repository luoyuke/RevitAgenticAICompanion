# Workflow Notes

Use this document to capture the decisions that need to be stable before implementation starts.

## Locked decisions

- Product direction: modeless agentic AI companion for Revit with persistent project memory.
- Host shape: native Revit add-in with a modeless chat UI.
- Current target: Revit 2026 on `.NET 8`.
- Runtime strategy for MVP: dynamic C# generation and in-process execution.
- Execution boundary: all Revit API work, including reads needed for planning, must enter Revit through a host-owned `ExternalEvent`.
- Transaction policy: the host owns top-level transaction lifecycle and human-readable transaction names.
- Auth strategy: ChatGPT sign-in for Codex, persisted and reused through the Codex auth/app-server path.
- Memory boundary: local per-user, per-machine, per-project storage; not embedded into the Revit model.
- Audit policy: every run must log prompt, plan, code hash, transaction names, result, and changed element ids when available.
- Approval policy: one approval for normal runs, typed `confirm` for undo-hostile runs.
- Execution mode: staged run with a host-owned `TransactionGroup` that allows one review/tweak cycle before final assimilation.

## Remaining decisions

- Initial solution/repo layout once implementation starts
- Exact compile/load strategy for generated code artifacts
- Static-analysis rule set for generated C#
- Thin-slice demo scope beyond the first locked workflow

## First milestone

Build one thin slice that proves:

- modeless chat
- Codex sign-in reuse
- planning against live Revit context
- approval gating
- staged transaction execution
- audit logging
- project memory read/write

## First locked demo workflow

The first demo workflow is:

- Curate a bill of quantity for a user-selected category by creating a new Revit schedule.

The default outcome is:

- Create a schedule inside Revit for the requested category.
- Populate a bounded set of fields.
- Apply simple sorting, filtering, and naming rules.

Out of scope for the first slice:

- multi-category schedules
- xlsx export
- complex formatting rules
- cross-discipline clash resolution
- MEP routing or radiator/pipe placement
- arbitrary element creation workflows beyond what is needed for schedule creation

Why this workflow is first:

- It is credible as a BIM task.
- It exercises live Revit reads and writes.
- It is transaction-friendly and normally rollback-friendly.
- It avoids the highest-risk geometry and systems logic in the first pass.
- It gives the model a constrained but useful planning target.

## Execution notes

- Generated code may use raw Revit API objects during execution.
- Generated code must not own top-level `Transaction` or `TransactionGroup` lifecycle.
- If the model changes after proposal generation and before approval, execution is invalidated.
- Partial success is acceptable; committed work is not auto-rolled back unless the user explicitly requests it.
