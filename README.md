# Revit Agentic AI Companion

Revit Agentic AI Companion is a Revit 2026 add-in that combines a dockable chat pane with bounded Revit execution. It lets Codex inspect the live model, propose generated C# actions, and execute approved edits through host-owned Revit requests.

The project is past the first proof-of-concept stage. It now supports multi-step inspection-first planning, read-only BIM queries, bounded write proposals, preview and approval before writes, and local audit/artifact review after each run.

## What it can do

- answer conversational prompts inside Revit
- inspect document, view, selection, linked-model, and parameter context
- run up to 3 read-only probes before proposing a write
- generate and compile C# against real Revit references
- preview bounded edits before approval
- execute approved writes inside host-owned Revit transactions
- analyze failed write executions and failed read-only probes automatically

## Current design

The host owns Revit access and execution. Codex owns planning.

Host responsibilities:
- capture context
- run `ExternalEvent` requests
- compile, validate, preview, and execute
- keep approval and confirm gates
- persist artifacts, audit, and user memory

Codex responsibilities:
- interpret prompts
- request more evidence when needed
- decide between reply, read-only query, inspection probe, or action proposal
- generate corrected follow-up plans after failures

## Memory and audit

The current memory model is intentionally small:

- Codex thread continuity for short conversational context
- `memory.md` for cross-project user preferences only
- `audit.db` as a ledger, not retrieval memory

Local runtime state lives under:

- `C:\Users\luoyu\AppData\Local\RevitAgenticAICompanion`

## Build

```powershell
dotnet build C:\Users\luoyu\Documents\Codex\RevitAgenticAICompanion\src\RevitAgenticAICompanion.Addin\RevitAgenticAICompanion.Addin.csproj -c Release -p:Platform=x64
```

## Install

Packaged installer:

- `C:\Users\luoyu\Documents\Codex\RevitAgenticAICompanion\deploy\Installer_2026-03-21`
- `C:\Users\luoyu\Documents\Codex\RevitAgenticAICompanion\deploy\Installer_2026-03-21.zip`

Run:

```powershell
C:\Users\luoyu\Documents\Codex\RevitAgenticAICompanion\deploy\Installer_2026-03-21\install.ps1
```

The installer copies the active payload, writes the Revit manifest, seeds `memory.md`, and can optionally reset project thread continuity.

## Repo layout

- `src/RevitAgenticAICompanion.Addin/` - add-in code
- `docs/` - architecture and workflow notes
- `deploy/` - deploy snapshots and installer packages

## Notes

- project-specific memory is intentionally disabled for now
- failures are expected during exploration; the runtime is built to keep them reviewable
- artifact folders are the best place to inspect a single run in detail

More architecture detail is in:

- `docs/runtime-architecture.md`
- `docs/workflow-notes.md`
