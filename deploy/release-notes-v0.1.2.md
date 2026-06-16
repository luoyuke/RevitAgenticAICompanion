# v0.1.2 Release Notes

## Highlights

- Added a validated Codex executable resolver.
- Prefer the current Codex desktop app runtime under `%LOCALAPPDATA%\OpenAI\Codex\bin\*\codex.exe`.
- Keep support for npm/global Codex CLI installs through `PATH`.
- Keep legacy `%USERPROFILE%\.codex\.sandbox-bin\codex.exe` only as a fallback.
- Added optional `REVIT_AGENTIC_AI_CODEX_PATH` override for deterministic admin/user setups.
- Runtime status and artifacts now show selected executable, resolver source, CLI version, config model, and config reasoning effort.
- Added compact runtime profile selector: Codex default, Fast, Balanced, Deep.
- Improved Codex runtime failure surfacing and audit diagnostics.
- Updated README and installer instructions for Codex app users.

## Notes

- The add-in still inherits Codex's configured/default model. It does not expose model IDs in the Revit UI and does not mutate `%USERPROFILE%\.codex\config.toml`.
- Runtime profiles override reasoning effort only when the active Codex binary supports `--config`.
- Restart Revit after installing this release.

## Installer

Use `Installer_v0.1.2_2026-06-16.zip`.

Recommended install:

```powershell
powershell -ExecutionPolicy Bypass -File .\install.ps1
```

or double-click:

```text
install.cmd
```
