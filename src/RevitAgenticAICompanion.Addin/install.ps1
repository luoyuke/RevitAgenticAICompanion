param(
    [switch]$ForceSeed,
    [switch]$ResetThreads
)

$ErrorActionPreference = "Stop"

$installer = Join-Path $PSScriptRoot "..\\..\\deploy\\installer\\install.ps1"
$resolvedInstaller = [System.IO.Path]::GetFullPath($installer)

if (-not (Test-Path $resolvedInstaller)) {
    throw "Installer package not found: $resolvedInstaller"
}

& $resolvedInstaller @PSBoundParameters
