param(
    [switch]$ForceSeed,
    [switch]$ResetThreads
)

$ErrorActionPreference = "Stop"

$installer = Join-Path $PSScriptRoot "..\\..\\deploy\\Installer_2026-03-21\\install.ps1"
$resolvedInstaller = [System.IO.Path]::GetFullPath($installer)

if (-not (Test-Path $resolvedInstaller)) {
    throw "Installer package not found: $resolvedInstaller"
}

& $resolvedInstaller @PSBoundParameters
