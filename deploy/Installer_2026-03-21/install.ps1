param(
    [switch]$ForceSeed,
    [switch]$ResetThreads
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$packageRoot = [System.IO.Path]::GetFullPath((Split-Path -Parent $MyInvocation.MyCommand.Path))
$payloadSource = Join-Path $packageRoot "payload"
$seedSource = Join-Path $packageRoot "seed"

$localAppRoot = Join-Path $env:LOCALAPPDATA "RevitAgenticAICompanion"
$installRoot = Join-Path (Join-Path $localAppRoot "install") "UserMemoryMd_2026-03-21"
$stateRoot = Join-Path $localAppRoot "state"
$revitAddinsRoot = Join-Path (Join-Path (Join-Path (Join-Path $env:APPDATA "Autodesk") "Revit") "Addins") "2026"
$manifestPath = Join-Path $revitAddinsRoot "RevitAgenticAICompanion.addin"
$assemblyPath = Join-Path $installRoot "RevitAgenticAICompanion.Addin.dll"
$memoryPath = Join-Path $stateRoot "memory.md"
$threadsPath = Join-Path $stateRoot "project-threads.json"

if (-not (Test-Path $payloadSource)) {
    throw "Installer payload folder not found: $payloadSource"
}

$seedMemorySource = Join-Path $seedSource "memory.md"
$seedThreadsSource = Join-Path $seedSource "project-threads.json"

if (-not (Test-Path $seedMemorySource)) {
    throw "Seed memory file not found: $seedMemorySource"
}

if (-not (Test-Path $seedThreadsSource)) {
    throw "Seed thread file not found: $seedThreadsSource"
}

New-Item -ItemType Directory -Force -Path $installRoot | Out-Null
New-Item -ItemType Directory -Force -Path $stateRoot | Out-Null
New-Item -ItemType Directory -Force -Path $revitAddinsRoot | Out-Null

Get-ChildItem $installRoot -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction Stop
Copy-Item -Path (Join-Path $payloadSource "*") -Destination $installRoot -Recurse -Force

if (-not (Test-Path $assemblyPath)) {
    throw "Installed assembly not found after copy: $assemblyPath"
}

$manifestContent = @"
<?xml version="1.0" encoding="utf-8"?>
<RevitAddIns>
  <AddIn Type="Application">
    <Name>Revit Agentic AI Companion</Name>
    <Assembly>$assemblyPath</Assembly>
    <AddInId>8B40A927-3228-40D4-A51A-5CD14E6A1001</AddInId>
    <FullClassName>RevitAgenticAICompanion.App</FullClassName>
    <VendorId>CODEX</VendorId>
    <VendorDescription>Revit Agentic AI Companion demo add-in.</VendorDescription>
  </AddIn>
  <AddIn Type="Command">
    <Name>Show Revit Agentic AI Companion</Name>
    <Assembly>$assemblyPath</Assembly>
    <AddInId>8B40A927-3228-40D4-A51A-5CD14E6A1002</AddInId>
    <FullClassName>RevitAgenticAICompanion.Commands.ShowChatCommand</FullClassName>
    <Text>AI Companion</Text>
    <Description>Open the Revit Agentic AI Companion chat pane.</Description>
    <VendorId>CODEX</VendorId>
    <VendorDescription>Revit Agentic AI Companion demo add-in.</VendorDescription>
  </AddIn>
</RevitAddIns>
"@

[System.IO.File]::WriteAllText($manifestPath, $manifestContent, [System.Text.UTF8Encoding]::new($false))

$seededMemory = $false
$seededThreads = $false

if ($ForceSeed -or -not (Test-Path $memoryPath)) {
    Copy-Item $seedMemorySource $memoryPath -Force
    $seededMemory = $true
}

if ($ResetThreads -or -not (Test-Path $threadsPath)) {
    Copy-Item $seedThreadsSource $threadsPath -Force
    $seededThreads = $true
}

Write-Host "Installed payload to $installRoot"
Write-Host "Installed manifest to $manifestPath"
if ($seededMemory) {
    Write-Host "Seeded memory file: $memoryPath"
}
else {
    Write-Host "Kept existing memory file: $memoryPath"
}

if ($ResetThreads) {
    Write-Host "Reset project threads: $threadsPath"
}
elseif ($seededThreads) {
    Write-Host "Seeded empty project threads file: $threadsPath"
}
else {
    Write-Host "Kept existing project threads file: $threadsPath"
}
