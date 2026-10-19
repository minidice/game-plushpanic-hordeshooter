param(
    [string]$GodotDir = 'D:\Util\Godot\Godot_v4.7.2-stable_mono_win64',
    [switch]$Test,
    [switch]$InputCheck,
    [switch]$Capture
)
$ErrorActionPreference = 'Stop'
$console = Join-Path $GodotDir 'Godot_v4.7.2-stable_mono_win64_console.exe'
$windowed = Join-Path $GodotDir 'Godot_v4.7.2-stable_mono_win64.exe'
if (-not (Test-Path -LiteralPath $console)) { throw "Godot 4.7.2 .NET not found in $GodotDir. Pass -GodotDir." }
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw '.NET SDK 8+ is required.' }

Push-Location $PSScriptRoot
try {
    & dotnet build --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw 'C# build failed.' }
    # Imports new or changed assets (GLB models) before running.
    & $console --headless --editor --import --path $PSScriptRoot --quit *> (Join-Path $PSScriptRoot 'output/import.log')
    if ($Test) {
        & $console --headless --path $PSScriptRoot res://tests/Integration.tscn
        if ($LASTEXITCODE -ne 0) { throw 'Integration checks failed. See output/integration-report.json' }
    } elseif ($InputCheck) {
        & $console --path $PSScriptRoot res://tests/InputCheck.tscn
        if ($LASTEXITCODE -ne 0) { throw 'Input check failed (exit 2 = window had no focus).' }
    } elseif ($Capture) {
        & $console --path $PSScriptRoot res://tests/Capture.tscn
        if ($LASTEXITCODE -ne 0) { throw 'Capture failed.' }
    } else {
        Start-Process -FilePath $windowed -ArgumentList '--path', ('"' + $PSScriptRoot + '"') -WorkingDirectory $PSScriptRoot
    }
} finally { Pop-Location }
