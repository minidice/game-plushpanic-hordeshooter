param(
    [string]$GodotDir = 'D:\Util\Godot\Godot_v4.7.2-stable_mono_win64',
    [int]$Port = 4317
)
$ErrorActionPreference = 'Stop'
$godot = Join-Path $GodotDir 'Godot_v4.7.2-stable_mono_win64_console.exe'
if (-not (Test-Path -LiteralPath $godot)) { throw "Godot .NET not found: $godot" }
if (-not (Get-Command node -ErrorAction SilentlyContinue)) { throw 'Node.js is required.' }
& node (Join-Path $PSScriptRoot 'server.mjs') --godot $godot --port $Port
if ($LASTEXITCODE -ne 0) { throw 'Studio stopped with an error.' }
