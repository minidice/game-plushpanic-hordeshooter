param(
    [string]$GodotDir = 'D:\Util\Godot\Godot_v4.7.2-stable_mono_win64',
    [switch]$Test,
    [switch]$InputCheck,
    [switch]$Capture,
    # Onslaught card lab (doc/onslaught-card-ideas.md): switch the lab cards on and off while playing.
    [switch]$CardLab,
    # Every weapon and dual variant, direct picker and F11/F12 cycling, actual ammo/reload feedback.
    [switch]$ReticleLab,
    # Two-player test (doc/multiplayer-sync-design.md): one window hosts, another joins.
    [switch]$NetHost,
    [string]$NetJoin,
    # One PC, two windows: builds once, opens a host window, then a window that joins it.
    [switch]$NetPair,
    # Play the large campaign drafts (decisions.md L-30): urban Stage 1 -> forest Stage 2 -> Stage 3, whatever the menu's
    # scenario card says (the menu's "City to River" card does the same, M-39).
    [switch]$LargeStages,
    # Start a large-campaign run on Stage 3, the size draft (doc/stage2-floorplan.md 11-12장). Single player only.
    [Alias('Stage2Draft')]
    [switch]$Stage3Draft,
    # Level test (doc/features/level/guide-map.md 9장): open the large campaign straight at a zone's entry, e.g. -Zone D or -Zone 2:D.
    [string]$Zone,
    # AI squadmates for this run (0, 1 or 3), over the menu's saved choice: -Squad 0 tests alone.
    [int]$Squad = -1,
    # Solo test: F8 frees a restrained survivor (it left the menu's test options, M-39).
    [switch]$DebugEscape
)
$ErrorActionPreference = 'Stop'
$squadArg = if ($Squad -ge 0) { @('--squad=' + $Squad) } else { @() }
if ($DebugEscape) { $squadArg += '--debug-escape' }
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
    } elseif ($ReticleLab) {
        & $console --path $PSScriptRoot res://tests/CardLab.tscn -- --reticle-lab
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
    } elseif ($Zone) {
        # Straight into the stage (no menu): Scenario, the default character and squad.
        Start-Process -FilePath $windowed -ArgumentList (@('--path', ('"' + $PSScriptRoot + '"'), 'res://scenes/Stage1.tscn', '--', '--large-stages', ('--zone=' + $Zone)) + $squadArg) -WorkingDirectory $PSScriptRoot
    } elseif ($LargeStages) {
        Start-Process -FilePath $windowed -ArgumentList (@('--path', ('"' + $PSScriptRoot + '"'), '--', '--large-stages') + $squadArg) -WorkingDirectory $PSScriptRoot
    } elseif ($Stage3Draft) {
        Start-Process -FilePath $windowed -ArgumentList '--path', ('"' + $PSScriptRoot + '"'), '--', '--stage3-draft' -WorkingDirectory $PSScriptRoot
    } elseif ($squadArg.Count -gt 0) {
        Start-Process -FilePath $windowed -ArgumentList (@('--path', ('"' + $PSScriptRoot + '"'), '--') + $squadArg) -WorkingDirectory $PSScriptRoot
    } else {
        Start-Process -FilePath $windowed -ArgumentList '--path', ('"' + $PSScriptRoot + '"') -WorkingDirectory $PSScriptRoot
    }
} finally { Pop-Location }
