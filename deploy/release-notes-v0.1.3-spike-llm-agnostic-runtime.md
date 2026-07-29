# v0.1.3 Spike Release Notes

## Scope

This is a spike build for the LLM-agnostic runtime path. It is intended for local testing, not a mainline stable release.

## Highlights

- Added a provider selector for Codex and Claude.
- Added Claude Code CLI runtime support using non-interactive JSON/stdout planning.
- Added Claude Desktop/Windows app detection as diagnostics only.
- Improved Claude missing-runtime messages so Desktop-only installs are not confused with a working CLI runtime.
- Fixed Claude follow-up turns by using `--resume <session_id>` instead of reusing `--session-id`.
- Tightened Claude's Revit source contract and added a one-turn repair when Claude asks for a probe/query/action but omits generated C# source.
- Added provider-neutral runtime labels in the Revit pane.
- Version stamp: `0.1.3-spike-llm-agnostic-runtime+20260729`.

## Claude Notes

- Claude provider requires Claude Code CLI and a suitable Claude account/plan.
- Claude Desktop/Windows app alone is not used as the planning runtime.
- The add-in may detect Claude Desktop and explain that Claude Code CLI is still required.

Verify Claude Code CLI from a normal terminal:

```powershell
claude --version
claude auth status
```

## Installer

Use `Installer_v0.1.3-spike-llm-agnostic-runtime_2026-07-29.zip`.

Recommended install:

```powershell
powershell -ExecutionPolicy Bypass -File .\install.ps1
```

or:

```cmd
install.cmd
```
