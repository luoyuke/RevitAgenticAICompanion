Revit Agentic AI Companion Installer
===================================

This folder contains a packaged installer for the Revit 2026 demo add-in.

Prerequisites
-------------

- Autodesk Revit 2026 on Windows.
- Codex desktop app, or a working Codex CLI installation.
- A ChatGPT/OpenAI account signed in through the local Codex runtime.

The add-in does not include credentials or model access. It uses the local
Codex runtime for the current Windows user.

Codex Runtime Resolution
------------------------

The add-in validates candidate Codex executables with "codex --version" and
then uses the first working runtime in this order:

1. REVIT_AGENTIC_AI_CODEX_PATH, if explicitly set.
2. Newest Codex app runtime under:

   %LOCALAPPDATA%\OpenAI\Codex\bin\*\codex.exe

3. Working codex.exe or codex found on PATH.
4. Legacy fallback:

   %USERPROFILE%\.codex\.sandbox-bin\codex.exe

Runtime status in the Revit pane and artifacts shows the selected executable,
CLI version, resolver source, configured model, and configured reasoning effort.

Recommended Install
-------------------

Use the command wrapper:

  install.cmd

The wrapper launches PowerShell with ExecutionPolicy Bypass, so you can usually
double-click it instead of typing the command manually.

PowerShell Install
------------------

If you prefer running the script directly:

  powershell -ExecutionPolicy Bypass -File .\install.ps1

Useful install flags:

  powershell -ExecutionPolicy Bypass -File .\install.ps1 -ForceSeed
  powershell -ExecutionPolicy Bypass -File .\install.ps1 -ResetThreads

What the installer does:

- Copies the packaged payload into:

  %LOCALAPPDATA%\RevitAgenticAICompanion\install\UserMemoryMd_2026-03-21

- Writes the Revit 2026 manifest into:

  %APPDATA%\Autodesk\Revit\Addins\2026

- Seeds memory.md only if missing, unless -ForceSeed is used.
- Seeds project-threads.json only if missing, unless -ResetThreads is used.

Restart Revit after installing or updating.

Uninstall
---------

Recommended:

  uninstall.cmd

Or directly:

  powershell -ExecutionPolicy Bypass -File .\uninstall.ps1

Close Revit before uninstalling. The uninstaller removes the Revit manifest and
installed payload. State files under:

  %LOCALAPPDATA%\RevitAgenticAICompanion\state

are left untouched.

Memory Commands
---------------

User memory is intentionally small and edited explicitly:

  /memory
  /memory <key> <value>
  /memory clear <key>

Allowed keys:

- preferred_language
- explanation_style
- approval_style
- inspection_bias

Manual Install
--------------

If you do not want to run scripts:

1. Copy everything from the payload folder into:

   %LOCALAPPDATA%\RevitAgenticAICompanion\install\UserMemoryMd_2026-03-21

2. Create this manifest file:

   %APPDATA%\Autodesk\Revit\Addins\2026\RevitAgenticAICompanion.addin

3. Use this manifest content, replacing %LOCALAPPDATA% with the real path if
   Revit does not expand environment variables:

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

4. Optionally copy seed\memory.md and seed\project-threads.json into:

   %LOCALAPPDATA%\RevitAgenticAICompanion\state

Manual Uninstall
----------------

1. Close Revit.
2. Delete:

   %APPDATA%\Autodesk\Revit\Addins\2026\RevitAgenticAICompanion.addin

3. Delete:

   %LOCALAPPDATA%\RevitAgenticAICompanion\install\UserMemoryMd_2026-03-21

4. Optionally keep or delete state files under:

   %LOCALAPPDATA%\RevitAgenticAICompanion\state
