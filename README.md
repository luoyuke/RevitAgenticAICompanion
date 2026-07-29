# Revit Agentic AI Companion

Revit Agentic AI Companion is an experimental Revit 2026 add-in that puts an AI planning pane inside Revit. It can inspect the live model, ask for more evidence, generate C# against the Revit API, preview bounded edits, and execute approved changes through host-owned Revit transactions.

Current spike version: `0.1.3-spike-llm-agnostic-runtime+20260729`.

It is past pure proof-of-concept and can already do useful BIM work, but it is still a research/demo project. Expect failures, inspect the artifacts, and test on disposable copies before trusting it near serious production models.

## Prerequisites

- Autodesk Revit 2026 on Windows.
- For Codex: the Codex desktop app, or a working Codex CLI installation, plus a ChatGPT/OpenAI account signed in through the local Codex runtime.
- For Claude: Claude Code CLI installed and authenticated for the current Windows user. Claude Desktop/Windows app can be detected for diagnostics, but it is not enough by itself because the add-in needs the scriptable `claude` command.
- .NET SDK 8+ if you want to build from source.

The add-in does not ship model access or credentials. It uses the local runtime already installed and signed in for the current Windows user.

## Runtime Providers

This spike adds a provider selector:

- `Codex`: uses the local Codex runtime.
- `Claude`: uses Claude Code CLI through the `claude` command.

The add-in does not drive Claude Desktop/Windows app directly. Claude Desktop is GUI-first and does not provide the non-interactive JSON/stdout contract this Revit host needs. If Claude Desktop is found but Claude Code CLI is missing, the pane reports that distinction instead of saying only "not found".

## Codex Runtime Resolution

The add-in resolves Codex at runtime instead of hardcoding a user path. Candidate executables are validated by running `codex --version`; broken WindowsApps aliases or stale binaries are skipped.

Resolution order:

- `REVIT_AGENTIC_AI_CODEX_PATH`, if explicitly set.
- Newest working Codex app runtime under `%LOCALAPPDATA%\OpenAI\Codex\bin\*\codex.exe`.
- Working `codex.exe` or `codex` found on `PATH`, useful for npm/global CLI installs.
- Legacy `%USERPROFILE%\.codex\.sandbox-bin\codex.exe` fallback.

The runtime status shown in the add-in and artifacts records the selected executable, CLI version, resolver source, configured model, configured reasoning effort, known local model catalog, and whether CLI overrides are supported.

## Runtime Profiles

The dockable pane includes compact provider and runtime selectors:

- `Provider default`: inherit the selected provider's config/defaults where possible.
- `Fast`: use the configured/default model with low reasoning.
- `Balanced`: use the configured/default model with medium reasoning.
- `Deep`: use the configured/default model with high reasoning.

The add-in currently does not expose raw model IDs in the UI and does not mutate provider-owned global config files. To change default models, use the provider's own config or app settings. Runtime profiles only apply overrides supported by the active provider binary.

## What It Can Do

- Answer conversational prompts inside Revit.
- Inspect document, view, selection, category, parameter, schedule, and linked-model context.
- Run bounded read-only inspection probes before proposing a write.
- Generate and compile C# against Revit 2026 references.
- Preview bounded edits before approval.
- Execute approved writes inside host-owned Revit transactions.
- Capture compact failure packets and ask Codex for failure analysis or repair proposals.
- Persist artifacts and audit rows for every run.

Recent test runs have successfully created ductwork-only 3D isometric views, generated ductwork BOQ schedules, and created low-density duct tags in locked 3D views. These are demonstrations, not guarantees.

## Current Design

The host owns Revit access and execution. Codex owns planning.

Host responsibilities:

- Capture Revit context.
- Run `ExternalEvent` requests.
- Compile, validate, preview, and execute generated code.
- Enforce approval and confirmation gates.
- Persist artifacts, audit rows, runtime diagnostics, and user memory.

Codex responsibilities:

- Interpret the user prompt.
- Decide between reply, read-only query, inspection probe, or action proposal.
- Request more evidence when model-specific facts are needed.
- Generate corrected follow-up plans after compile or execution failures.

## Memory And Audit

The current memory model is intentionally tiny:

- Codex thread continuity for short conversational context.
- `memory.md` for cross-project user preferences only.
- `audit.db` as a ledger, not retrieval memory.

Memory is read automatically on every prompt and updated only with explicit slash commands:

```text
/memory
/memory <key> <value>
/memory clear <key>
```

Allowed keys:

- `preferred_language`
- `explanation_style`
- `approval_style`
- `inspection_bias`

Project-specific facts should not be stored in memory yet. They belong in the Revit model, artifacts, or a future project-scoped retrieval layer.

## Build

Build from the project root:

```powershell
dotnet build .\src\RevitAgenticAICompanion.Addin\RevitAgenticAICompanion.Addin.csproj -c Release -p:Platform=x64
```

Compiled output lands under:

```text
src\RevitAgenticAICompanion.Addin\bin\Release\
```

The packaged installer payload is copied from that build output into:

```text
deploy\Installer_2026-03-21\payload\
```

## Install

Use the packaged installer snapshot:

```powershell
powershell -ExecutionPolicy Bypass -File .\deploy\Installer_2026-03-21\install.ps1
```

Or use the command wrapper:

```cmd
deploy\Installer_2026-03-21\install.cmd
```

The installer:

- Copies the payload into `%LOCALAPPDATA%\RevitAgenticAICompanion\install\UserMemoryMd_2026-03-21`.
- Writes the Revit 2026 manifest into `%APPDATA%\Autodesk\Revit\Addins\2026`.
- Seeds `memory.md` only if missing, unless `-ForceSeed` is used.
- Seeds `project-threads.json` only if missing, unless `-ResetThreads` is used.

Useful flags:

```powershell
.\deploy\Installer_2026-03-21\install.ps1 -ForceSeed
.\deploy\Installer_2026-03-21\install.ps1 -ResetThreads
```

Close and restart Revit after installing or updating the add-in.

## Uninstall

Use:

```powershell
powershell -ExecutionPolicy Bypass -File .\deploy\Installer_2026-03-21\uninstall.ps1
```

The uninstaller removes the Revit manifest and installed payload. State files under `%LOCALAPPDATA%\RevitAgenticAICompanion\state` are intentionally left untouched.

## Repo Layout

- `src/RevitAgenticAICompanion.Addin/`: Revit add-in source, runtime client, UI, storage, and request handlers.
- `deploy/Installer_2026-03-21/`: packaged installer snapshot and release payload.
- `deploy/`: older milestone snapshots and deploy history.
- `docs/`: screenshots and lightweight notes.
- `docs/test-runs/`: captured screenshots from test sessions.

## Known Caveats

- This is a demo/research add-in, not production BIM automation software.
- The add-in currently inherits the configured Codex model unless a future model-selection UI is added.
- Claude provider support is a spike. It requires Claude Code CLI; Claude Desktop detection is diagnostic-only.
- Some timing comparisons are approximate because artifacts do not yet isolate pure `codex exec` duration.
- Hopping between unsaved documents can still leak conversational context if Revit documents share the same title, because thread continuity falls back to document title when no file path exists.
- User-facing artifact text can still show occasional encoding artifacts in some output paths.
- Bulk annotation can succeed technically while still producing visually noisy results; review previews and changed element IDs.

## License

This project is released under the MIT License.
