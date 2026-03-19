$source = Join-Path $PSScriptRoot "RevitAgenticAICompanion.addin"
$targetDir = Join-Path $env:APPDATA "Autodesk\Revit\Addins\2026"
$target = Join-Path $targetDir "RevitAgenticAICompanion.addin"

if (-not (Test-Path $targetDir)) {
    New-Item -ItemType Directory -Path $targetDir | Out-Null
}

Copy-Item -Path $source -Destination $target -Force
Write-Host "Installed add-in manifest to $target"
