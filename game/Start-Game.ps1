param(
    [string]$GodotDir = 'D:\Util\Godot\Godot_v4.7.2-stable_mono_win64',
    [switch]$Test,
    [switch]$InputCheck,
    [switch]$Capture,
    # Onslaught card lab (doc/onslaught-card-ideas.md): switch the lab cards on and off while playing.
    [switch]$CardLab,
    # Two-player test (doc/multiplayer-sync-design.md): one window hosts, another joins.
    [switch]$NetHost,
    [string]$NetJoin,
    # One PC, two windows: builds once, opens a host window, then a window that joins it.
    [switch]$NetPair,
    # Play the Stage 2 size draft (doc/stage2-floorplan.md 11-12장): a new scenario run starts on it. Single player only.
    [switch]$Stage2Draft
)
$ErrorActionPreference = 'Stop'
$console = Join-Path $GodotDir 'Godot_v4.7.2-stable_mono_win64_console.exe'
$windowed = Join-Path $GodotDir 'Godot_v4.7.2-stable_mono_win64.exe'
if (-not (Test-Path -LiteralPath $console)) { throw "Godot 4.7.2 .NET not found in $GodotDir. Pass -GodotDir." }
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw '.NET SDK 8+ is required.' }

Push-Location $PSScriptRoot
try {
    & dotnet build HordeShooter.csproj --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw 'C# build failed.' }
    # Imports new or changed assets (GLB models) before running.
    & $console --headless --editor --import --path $PSScriptRoot --quit *> (Join-Path $PSScriptRoot 'output/import.log')
    if ($Test) {
        & $console --headless --path $PSScriptRoot res://tests/Integration.tscn
        if ($LASTEXITCODE -ne 0) { throw 'Integration checks failed. See output/integration-report.json' }
    } elseif ($InputCheck) {
        & $console --path $PSScriptRoot res://tests/InputCheck.tscn
        if ($LASTEXITCODE -ne 0) { throw 'Input check failed (exit 2 = window had no focus).' }
    } elseif ($CardLab) {
        & $console --path $PSScriptRoot res://tests/CardLab.tscn
    } elseif ($Capture) {
        & $console --path $PSScriptRoot res://tests/Capture.tscn
        if ($LASTEXITCODE -ne 0) { throw 'Capture failed.' }
    } elseif ($NetPair) {
        Start-Process -FilePath $windowed -ArgumentList '--path', ('"' + $PSScriptRoot + '"'), '--', '--host' -WorkingDirectory $PSScriptRoot
        Start-Sleep -Seconds 4
        Start-Process -FilePath $windowed -ArgumentList '--path', ('"' + $PSScriptRoot + '"'), '--', '--join=127.0.0.1' -WorkingDirectory $PSScriptRoot
    } elseif ($NetHost) {
        Start-Process -FilePath $windowed -ArgumentList '--path', ('"' + $PSScriptRoot + '"'), '--', '--host' -WorkingDirectory $PSScriptRoot
    } elseif ($NetJoin) {
        Start-Process -FilePath $windowed -ArgumentList '--path', ('"' + $PSScriptRoot + '"'), '--', ('--join=' + $NetJoin) -WorkingDirectory $PSScriptRoot
    } elseif ($Stage2Draft) {
        Start-Process -FilePath $windowed -ArgumentList '--path', ('"' + $PSScriptRoot + '"'), '--', '--stage2-draft' -WorkingDirectory $PSScriptRoot
    } else {
        Start-Process -FilePath $windowed -ArgumentList '--path', ('"' + $PSScriptRoot + '"') -WorkingDirectory $PSScriptRoot
    }
} finally { Pop-Location }
