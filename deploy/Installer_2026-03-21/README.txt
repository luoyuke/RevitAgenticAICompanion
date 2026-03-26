Revit Agentic AI Companion Installer
===================================

This folder contains a packaged installer for the Revit 2026 demo add-in.

Recommended
-----------

Use the command wrappers:

- install.cmd
- uninstall.cmd

These wrappers launch PowerShell with ExecutionPolicy Bypass, so you can usually
double-click them instead of typing the command manually.

PowerShell Scripts
------------------

If you prefer running the scripts directly, use:

  powershell -ExecutionPolicy Bypass -File .\install.ps1

and:

  powershell -ExecutionPolicy Bypass -File .\uninstall.ps1

Notes:

- Close Revit before uninstalling.
- The installer seeds memory.md and project-threads.json only when needed,
  unless you explicitly force/reset them.
- After install, user memory is edited explicitly with:
  - /memory
  - /memory <key> <value>
  - /memory clear <key>

Useful install flags:

- install.ps1 -ForceSeed
- install.ps1 -ResetThreads

Manual Install
--------------

If you do not want to run scripts, you can install manually:

1. Copy everything from the payload folder into:

   %LOCALAPPDATA%\RevitAgenticAICompanion\install\UserMemoryMd_2026-03-21

2. Create this Revit manifest file:

   %APPDATA%\Autodesk\Revit\Addins\2026\RevitAgenticAICompanion.addin

3. Use this manifest content, replacing the Assembly path if needed:

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

4. Optionally copy the seed files into:

   %LOCALAPPDATA%\RevitAgenticAICompanion\state

   Files:
   - seed\memory.md
   - seed\project-threads.json

Manual Uninstall
----------------

1. Close Revit.
2. Delete:

   %APPDATA%\Autodesk\Revit\Addins\2026\RevitAgenticAICompanion.addin

3. Delete:

   %LOCALAPPDATA%\RevitAgenticAICompanion\install\UserMemoryMd_2026-03-21

4. Optional:
   Keep or delete state files under:

   %LOCALAPPDATA%\RevitAgenticAICompanion\state

