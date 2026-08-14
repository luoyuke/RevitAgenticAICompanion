# Revit Agentic AI Companion v0.2.0

This release merges the tested LLM-agnostic runtime spike into the main branch.

## Highlights

- Select Codex or Claude as the planning provider from the Revit pane.
- Use provider-aware runtime profiles without exposing raw model IDs in the main UI.
- Run Claude through Claude Code CLI with session resume, source repair, and clearer runtime diagnostics.
- Keep Codex on its local default model with host-selected reasoning effort.
- Automatically invalidate pending unexecuted proposals when the provider changes.
- Preserve host-owned Revit transactions, approval gates, audit records, artifacts, and memory behavior across providers.

## Requirements

- Autodesk Revit 2026 on Windows.
- For Codex: a working local Codex runtime and authenticated ChatGPT/OpenAI account.
- For Claude: Claude Code CLI installed and authenticated for the current Windows user.

Claude Desktop alone is not a scriptable planning runtime. The add-in may detect it for diagnostics, but Claude planning still requires Claude Code CLI.
