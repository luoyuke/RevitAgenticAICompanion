$ErrorActionPreference = "Stop"

$uninstaller = Join-Path $PSScriptRoot "..\\..\\deploy\\Installer_2026-03-21\\uninstall.ps1"
$resolvedUninstaller = [System.IO.Path]::GetFullPath($uninstaller)

if (-not (Test-Path $resolvedUninstaller)) {
    throw "Installer package not found: $resolvedUninstaller"
}

& $resolvedUninstaller
