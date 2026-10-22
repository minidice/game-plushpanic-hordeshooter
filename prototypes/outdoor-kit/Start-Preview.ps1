param(
    [string]$GodotExe = 'D:/Util/Godot/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe',
    [switch]$Capture,
    [switch]$Validate
)
$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $GodotExe)) { throw 'Pass -GodotExe with a Godot 4.7 executable.' }
New-Item -ItemType Directory -Force -Path (Join-Path $PSScriptRoot 'output') | Out-Null
$importLog = Join-Path $PSScriptRoot 'output/import.log'
& $GodotExe --headless --editor --import --path $PSScriptRoot --quit *> $importLog
if ($LASTEXITCODE -ne 0 -or (Select-String -LiteralPath $importLog -Pattern 'ERROR:' -Quiet)) {
    throw "Godot import failed. See $importLog"
}
if ($Validate) {
    & $GodotExe --headless --path $PSScriptRoot --script res://preview/validate_import.gd
    if ($LASTEXITCODE -ne 0) { throw 'Import contract check failed.' }
    & $GodotExe --headless --path $PSScriptRoot -- --validate-preview
    if ($LASTEXITCODE -ne 0) { throw 'Material contract check failed.' }
} elseif ($Capture) {
    & $GodotExe --path $PSScriptRoot -- --capture
    if ($LASTEXITCODE -ne 0) { throw 'Render capture failed.' }
} else {
    & $GodotExe --path $PSScriptRoot
}
