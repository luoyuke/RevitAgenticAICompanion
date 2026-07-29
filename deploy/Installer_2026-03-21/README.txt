Revit Agentic AI Companion Installer
===================================

This folder contains a packaged installer for the Revit 2026 demo add-in.
This spike build adds a host-side runtime provider selector so the pane can run against Codex or Claude, depending on what is installed and signed in for the current Windows user.

Spike version: 0.1.3-spike-llm-agnostic-runtime+20260729

Prerequisites
-------------

Required:

- Autodesk Revit 2026 on Windows.
- A local AI runtime signed in for the current Windows user.
- For Codex: Codex desktop app or a working Codex CLI/runtime, plus a ChatGPT/OpenAI account.
- For Claude: Claude Code CLI available locally, plus Claude authentication for the current Windows user. Claude Desktop/Windows app can be detected, but it is not enough by itself.

The installer does not include credentials, API keys, model access, or a bundled AI account. It only installs the Revit add-in files.

Recommended Install
-------------------

Close Revit first, then run:

```cmd
install.cmd
```

The command wrapper launches PowerShell with ExecutionPolicy Bypass. If Windows blocks double-click execution, open Command Prompt in this folder and run the same command.

PowerShell Install
------------------

You can also run the script directly:

```powershell
powershell -ExecutionPolicy Bypass -File .\install.ps1
```

Useful flags:

```powershell
powershell -ExecutionPolicy Bypass -File .\install.ps1 -ForceSeed
powershell -ExecutionPolicy Bypass -File .\install.ps1 -ResetThreads
```

What the installer does:

- Copies the packaged payload into `%LOCALAPPDATA%\RevitAgenticAICompanion\install\UserMemoryMd_2026-03-21`.
- Writes the Revit 2026 manifest to `%APPDATA%\Autodesk\Revit\Addins\2026\RevitAgenticAICompanion.addin`.
- Seeds `memory.md` only if missing, unless `-ForceSeed` is used.
- Seeds `project-threads.json` only if missing, unless `-ResetThreads` is used.

Restart Revit after installing or updating.

Runtime Provider Notes
----------------------

The Revit pane has a provider selector and runtime profile selector.

- Provider `Codex` uses the local Codex runtime.
- Provider `Claude` uses the local Claude CLI/runtime.
- Runtime profile `Provider default` avoids model/reasoning overrides where possible.
- Runtime profiles `Fast`, `Balanced`, and `Deep` apply host-owned effort settings for the next planning run.

Natural-language prompts do not change the selected provider or runtime profile. Change those in the UI before pressing Plan.

Claude Desktop / Windows App
----------------------------

The Claude provider is CLI-backed. The add-in needs the scriptable `claude` command because planning is non-interactive:

```text
prompt in -> JSON/stdout out -> Revit proposal
```

Claude Desktop/Windows app is detected only for diagnostics. If Desktop is installed but Claude Code CLI is missing, the pane should report that Desktop was found but the CLI is still required.

Install and verify Claude Code CLI from a normal terminal:

```powershell
irm https://claude.ai/install.ps1 | iex
claude --version
claude auth status
```

Runtime Resolution
------------------

Codex resolution order:

1. `REVIT_AGENTIC_AI_CODEX_PATH`, if set.
2. Newest Codex app runtime under `%LOCALAPPDATA%\OpenAI\Codex\bin\*\codex.exe`.
3. Working `codex.exe` or `codex` on `PATH`.
4. Legacy fallback `%USERPROFILE%\.codex\.sandbox-bin\codex.exe`.

Claude resolution order:

1. `REVIT_AGENTIC_AI_CLAUDE_PATH`, if set.
2. Working `claude.exe` or `claude` on `PATH`.
3. Common npm/global install locations, if present.
4. Claude Desktop/Windows app detection is diagnostic-only and is not used as the planning executable unless it also behaves like the `claude` CLI.

If the pane reports provider status Warning or Error, check that the selected provider is installed and signed in from a normal terminal first.

Manual Install
--------------

Use this only if you do not want to run the scripts.

1. Close Revit.
2. Create this folder:

```text
%LOCALAPPDATA%\RevitAgenticAICompanion\install\UserMemoryMd_2026-03-21
```

3. Copy everything from this package's `payload` folder into that install folder.
4. Create this folder if it does not exist:

```text
%APPDATA%\Autodesk\Revit\Addins\2026
```

5. Create this file:

```text
%APPDATA%\Autodesk\Revit\Addins\2026\RevitAgenticAICompanion.addin
```

6. Put this XML inside the `.addin` file. Replace `%LOCALAPPDATA%` with the full real path, for example `C:\Users\YourName\AppData\Local`, if Revit does not expand the environment variable on your machine.

```xml
<?xml version="1.0" encoding="utf-8"?>
<RevitAddIns>
  <AddIn Type="Application">
    <Name>Revit Agentic AI Companion</Name>
    <Assembly>%LOCALAPPDATA%\RevitAgenticAICompanion\install\UserMemoryMd_2026-03-21\RevitAgenticAICompanion.Addin.dll</Assembly>
    <AddInId>8B40A927-3228-40D4-A51A-5CD14E6A1001</AddInId>
    <FullClassName>RevitAgenticAICompanion.App</FullClassName>
    <VendorId>CODEX</VendorId>
    <VendorDescription>Revit Agentic AI Companion demo add-in.</VendorDescription>
  </AddIn>
  <AddIn Type="Command">
    <Name>Show Revit Agentic AI Companion</Name>
    <Assembly>%LOCALAPPDATA%\RevitAgenticAICompanion\install\UserMemoryMd_2026-03-21\RevitAgenticAICompanion.Addin.dll</Assembly>
    <AddInId>8B40A927-3228-40D4-A51A-5CD14E6A1002</AddInId>
    <FullClassName>RevitAgenticAICompanion.Commands.ShowChatCommand</FullClassName>
    <Text>AI Companion</Text>
    <Description>Open the Revit Agentic AI Companion chat pane.</Description>
    <VendorId>CODEX</VendorId>
    <VendorDescription>Revit Agentic AI Companion demo add-in.</VendorDescription>
  </AddIn>
</RevitAddIns>
```

7. Optional seed files: copy `seed\memory.md` and `seed\project-threads.json` into:

```text
%LOCALAPPDATA%\RevitAgenticAICompanion\state
```

Manual Uninstall
----------------

1. Close Revit.
2. Delete `%APPDATA%\Autodesk\Revit\Addins\2026\RevitAgenticAICompanion.addin`.
3. Delete `%LOCALAPPDATA%\RevitAgenticAICompanion\install\UserMemoryMd_2026-03-21`.
4. Optionally keep or delete `%LOCALAPPDATA%\RevitAgenticAICompanion\state`.

Scripted Uninstall
------------------

Recommended:

```cmd
uninstall.cmd
```

Or directly:

```powershell
powershell -ExecutionPolicy Bypass -File .\uninstall.ps1
```

The uninstaller removes the Revit manifest and installed payload. State files under `%LOCALAPPDATA%\RevitAgenticAICompanion\state` are left untouched.

Troubleshooting
---------------

- If PowerShell blocks the script, use `powershell -ExecutionPolicy Bypass -File .\install.ps1` from Command Prompt.
- If Revit still loads an older build, check the `.addin` file path under `%APPDATA%\Autodesk\Revit\Addins\2026`.
- If Codex is not found, set `REVIT_AGENTIC_AI_CODEX_PATH` to the full path of `codex.exe`.
- If Claude is not found, set `REVIT_AGENTIC_AI_CLAUDE_PATH` to the full path of `claude.exe`.
- If Claude Desktop is found but Claude CLI is missing, install Claude Code CLI; the desktop app alone cannot return the structured JSON the add-in expects.
- If the provider is found but planning fails, run the provider once in a normal terminal to confirm it is signed in.
