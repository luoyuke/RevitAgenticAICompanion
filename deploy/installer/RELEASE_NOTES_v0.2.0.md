# v0.2.0 Release Notes

## Highlights

- Added a host-owned provider selector for Codex and Claude.
- Added Claude Code CLI planning with provider-specific session continuity and repair handling.
- Added provider-neutral runtime status and diagnostics in the Revit pane and artifacts.
- Added Claude runtime profile mapping: `Fast` and `Balanced` use `sonnet`, `Deep` uses `opus`, `Experimental` uses `fable`, and `Provider default` passes no model override.
- Kept Codex model selection on the local Codex default while applying profile-specific reasoning effort.
- Invalidated pending unexecuted proposals when the runtime provider changes.

## Runtime Requirements

- Codex requires a working local Codex runtime signed in for the current Windows user.
- Claude requires Claude Code CLI installed and authenticated for the current Windows user.
- Claude Desktop detection is diagnostic only and does not replace Claude Code CLI.

## Version

Assembly informational version: `0.2.0+20260814`.
