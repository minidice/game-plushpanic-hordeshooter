# Builds a Windows copy of the game that runs on another PC without Godot or the .NET SDK.
# Output: <repo>/build/windows/ (HordeShooter.exe + .pck + the .NET data folder) and build/HordeShooter-windows.zip.
# Copy the whole folder (or unzip the zip) to the other PC and run HordeShooter.exe.
# Needs the Godot 4.7.2 .NET export templates (Editor → Manage Export Templates, or %APPDATA%\Godot\export_templates\4.7.2.stable.mono).
param(
    # Explicit -GodotDir overrides the machine's GODOT_DIR; keep the original default as fallback.
    [string]$GodotDir = $(if ($env:GODOT_DIR) {
        $env:GODOT_DIR
    } else {
        'D:\Util\Godot\Godot_v4.7.2-stable_mono_win64'
    }),
    [switch]$NoZip
)
$ErrorActionPreference = 'Stop'
$console = Join-Path $GodotDir 'Godot_v4.7.2-stable_mono_win64_console.exe'
if (-not (Test-Path -LiteralPath $console)) { throw "Godot 4.7.2 .NET not found in $GodotDir. Pass -GodotDir." }
$templates = Join-Path $env:APPDATA 'Godot\export_templates\4.7.2.stable.mono\windows_release_x86_64.exe'
if (-not (Test-Path -LiteralPath $templates)) { throw "Export templates missing: $templates" }

Push-Location $PSScriptRoot
$previousGodotDir = $env:GODOT_DIR
try {
    # Validation, NuGet and export must all use the selected engine installation.
    $env:GODOT_DIR = $GodotDir
    # A green result belongs to exact source/assets/tests, not just a branch name or an old successful run.
    $reportPath = Join-Path $PSScriptRoot 'output/campaign-validation.json'
    $fingerprint = & "$PSScriptRoot/tests/Get-CampaignBuildFingerprint.ps1"
    $version = (& $console --version | Out-String).Trim()
    $report = if (Test-Path -LiteralPath $reportPath) { Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json } else { $null }
    if (-not ($report -and $report.Passed -and $report.Windowed -and $report.Fingerprint -eq $fingerprint -and $report.GodotVersion -eq $version)) {
        & "$PSScriptRoot/tests/Run-CampaignMultiplayerChecks.ps1" -Godot $console -Windowed
        $report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
    }
    $fingerprint = & "$PSScriptRoot/tests/Get-CampaignBuildFingerprint.ps1"
    if (-not ($report.Passed -and $report.Windowed -and $report.Fingerprint -eq $fingerprint -and $report.GodotVersion -eq $version)) { throw 'Campaign multiplayer validation failed or source changed; export blocked.' }
    $out = Join-Path (Split-Path $PSScriptRoot -Parent) 'build\windows'
    $expected = [IO.Path]::GetFullPath((Join-Path (Split-Path $PSScriptRoot -Parent) 'build/windows'))
    if ([IO.Path]::GetFullPath($out) -ne $expected) { throw 'Unexpected export cleanup path.' }
    if (Test-Path -LiteralPath $out) {
        if ((Get-Item -LiteralPath $out).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Refusing to clean an export junction.' }
        Remove-Item -LiteralPath $out -Recurse -Force
    }
    New-Item -ItemType Directory -Force $out | Out-Null
    & dotnet build HordeShooter.csproj --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw 'C# build failed.' }
    & $console --headless --editor --import --path $PSScriptRoot --quit *> (Join-Path $PSScriptRoot 'output/import.log')
    if ($LASTEXITCODE -ne 0) { throw 'Godot import failed.' }
    & $console --headless --path $PSScriptRoot --export-release 'Windows Desktop' (Join-Path $out 'HordeShooter.exe') *> (Join-Path $PSScriptRoot 'output/export.log')
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path (Join-Path $out 'HordeShooter.exe'))) { throw 'Export failed. See game/output/export.log' }
    # Without the .NET data folder the game starts with no scripts at all: treat that as a failed export.
    if (-not (Get-ChildItem $out -Directory -Filter 'data_HordeShooter_*')) { throw 'Export has no .NET data folder (C# not exported). See game/output/export.log' }
    if ((& "$PSScriptRoot/tests/Get-CampaignBuildFingerprint.ps1") -ne $fingerprint) { throw 'Source changed during export; packaging blocked.' }
    Copy-Item (Join-Path $PSScriptRoot 'PLAY-README.txt') $out
    Copy-Item -LiteralPath $reportPath -Destination (Join-Path $out 'VALIDATION.json')
    if (-not $NoZip) {
        $zip = Join-Path (Split-Path $out -Parent) 'HordeShooter-windows.zip'
        if (Test-Path $zip) { Remove-Item -Force $zip }
        Compress-Archive -Path (Join-Path $out '*') -DestinationPath $zip
        Write-Host "Zip: $zip"
    }
    Write-Host "Exported to $out"
} finally {
    $env:GODOT_DIR = $previousGodotDir
    Pop-Location
}
