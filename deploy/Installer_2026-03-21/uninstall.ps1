$ErrorActionPreference = "Stop"

$installRoot = Join-Path $env:LOCALAPPDATA "RevitAgenticAICompanion\\install\\UserMemoryMd_2026-03-21"
$manifestPath = Join-Path $env:APPDATA "Autodesk\\Revit\\Addins\\2026\\RevitAgenticAICompanion.addin"

if (Test-Path $manifestPath) {
    Remove-Item $manifestPath -Force
}

if (Test-Path $installRoot) {
    Remove-Item $installRoot -Recurse -Force
}

Write-Host "Removed manifest: $manifestPath"
Write-Host "Removed installed payload: $installRoot"
Write-Host "State files under %LOCALAPPDATA%\\RevitAgenticAICompanion\\state were left untouched."
