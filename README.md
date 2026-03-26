# Revit Agentic AI Companion

Revit Agentic AI Companion is a hobby Revit 2026 add-in that lets you talk to your model like it’s a coworker… except this coworker can write code, run transactions, and occasionally make questionable life choices.It is a dockable chat pane with bounded Revit execution. It lets Codex inspect the live model, propose generated C# actions, and execute approved edits through host-owned Revit requests.

It’s past proof-of-concept and already does real work — but it’s still very much an experiment. It now supports multi-step inspection-first planning, read-only BIM queries, bounded write proposals, preview and approval before writes, and local audit/artifact review after each run.

If your model gets nuked, tell me so I can also laugh about it.

## Prerequisites

Before the add-in can do anything useful, you need:

- Autodesk Revit 2026
- Codex CLI installed on the machine
- a ChatGPT or OpenAI account that can sign in through `codex login`

The add-in does not ship its own model access. It checks the local Codex CLI session with `codex login status`, and if needed starts the browser sign-in flow with `codex login`.

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

Memory is read automatically on every prompt and updated only with explicit commands:

- `/memory`
- `/memory <key> <value>`
- `/memory clear <key>`

Allowed keys:

- `preferred_language`
- `explanation_style`
- `approval_style`
- `inspection_bias`

## Build

Build the add-in from the project root:

```powershell
dotnet build .\src\RevitAgenticAICompanion.Addin\RevitAgenticAICompanion.Addin.csproj -c Release -p:Platform=x64
```

The compiled output lands under:

- `src/RevitAgenticAICompanion.Addin/bin/Release/`

The active packaged payload used for local testing currently lives under:

- `deploy/UserMemoryMd_2026-03-20/`

## Install

The repo includes a packaged installer snapshot:

- `deploy/Installer_2026-03-21/`

Run the installer script:

```powershell
.\deploy\Installer_2026-03-21\install.ps1
```

What it does:

- copies the packaged payload into `%LOCALAPPDATA%\RevitAgenticAICompanion\install\...`
- writes the Revit 2026 manifest into `%APPDATA%\Autodesk\Revit\Addins\2026\`
- seeds `memory.md`
- seeds an empty `project-threads.json` if missing

Useful options:

- `-ForceSeed` overwrites the seeded `memory.md`
- `-ResetThreads` clears stored project thread continuity

## Repo layout

- `src/RevitAgenticAICompanion.Addin/` - Revit add-in source, runtime, UI, storage, and request handlers
- `deploy/` - frozen deploy snapshots, installers, and milestone payloads
- `docs/` - screenshots and lightweight project notes
- `docs/test-runs/` - captured screenshots from test sessions

## Notes

- project-specific memory is intentionally disabled for now
- hopping between unsaved documents can still leak conversational context if Revit documents share the same title, because thread continuity falls back to the document title when no file path exists
- failures are expected during exploration; the runtime is built to keep them reviewable
- artifact folders are the best place to inspect a single run in detail

Why this exists
Originally this started as:
- “How badly can an agentic AI mess up a Revit model?”
Turns out…  
it can do some genuinely useful BIM work and might refuse your task due to your bad engineering practices.  
So now it’s both:
a chaos experiment and a glimpse of what future BIM workflows might look like
