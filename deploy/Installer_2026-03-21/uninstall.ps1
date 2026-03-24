$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$localAppRoot = Join-Path $env:LOCALAPPDATA "RevitAgenticAICompanion"
$installRoot = Join-Path (Join-Path $localAppRoot "install") "UserMemoryMd_2026-03-21"
$manifestPath = Join-Path (Join-Path (Join-Path (Join-Path (Join-Path $env:APPDATA "Autodesk") "Revit") "Addins") "2026") "RevitAgenticAICompanion.addin"

$revitProcesses = @(Get-Process Revit -ErrorAction SilentlyContinue)
if ($revitProcesses.Count -gt 0) {
    $processList = ($revitProcesses | Select-Object -ExpandProperty Id) -join ", "
    throw "Revit is still running (process id(s): $processList). Close Revit before uninstalling the add-in."
}

if (Test-Path $manifestPath) {
    Remove-Item $manifestPath -Force
    Write-Host "Removed manifest: $manifestPath"
}
else {
    Write-Host "Manifest already absent: $manifestPath"
}

if (Test-Path $installRoot) {
    try {
        Get-ChildItem $installRoot -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction Stop
        Remove-Item $installRoot -Force -ErrorAction Stop
        Write-Host "Removed installed payload: $installRoot"
    }
    catch {
        throw "Failed to remove installed payload at '$installRoot'. Make sure Revit and any file viewers are closed, then try again. Original error: $($_.Exception.Message)"
    }
}
else {
    Write-Host "Installed payload already absent: $installRoot"
}

Write-Host "State files under %LOCALAPPDATA%\\RevitAgenticAICompanion\\state were left untouched."
