param(
    [string]$Godot = (Join-Path $env:LOCALAPPDATA 'CodexTools\Godot-4.7.2\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe'),
    [switch]$Test,
    [switch]$Capture
)
$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $Godot)) { throw "Godot .NET not found. Pass -Godot with the path to Godot 4.7.2 .NET." }
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw '.NET SDK is required.' }
Push-Location $PSScriptRoot
try {
    & dotnet build
    if ($LASTEXITCODE -ne 0) { throw 'C# build failed.' }
    & $Godot --headless --editor --import --path $PSScriptRoot --quit
    if ($LASTEXITCODE -ne 0) { throw 'Godot import failed.' }
    if ($Test) {
        & $Godot --headless --path $PSScriptRoot res://tests/Integration.tscn
        if ($LASTEXITCODE -ne 0) { throw 'Integration checks failed.' }
    } elseif ($Capture) {
        New-Item -ItemType Directory -Force -Path (Join-Path $PSScriptRoot 'output') | Out-Null
        $captureProcess = Start-Process -FilePath $Godot -ArgumentList '--path', ('"' + $PSScriptRoot + '"'), '--', '--capture' -WindowStyle Hidden -PassThru -Wait -RedirectStandardOutput (Join-Path $PSScriptRoot 'output/capture.log') -RedirectStandardError (Join-Path $PSScriptRoot 'output/capture-errors.log')
        if ($captureProcess.ExitCode -ne 0) { throw 'Viewport capture failed.' }
    } else {
        # Invoked by the user to open the interactive game window.
        Start-Process -FilePath $Godot -ArgumentList '--path', ('"' + $PSScriptRoot + '"') -WorkingDirectory $PSScriptRoot
    }
} finally { Pop-Location }
