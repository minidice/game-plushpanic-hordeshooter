param(
    [string]$Godot = 'D:/Util/Godot/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe',
    [ValidatePattern('^[a-zA-Z0-9-]+$')][string]$Prefix = 'campaign-gate',
    [switch]$Windowed
)
$ErrorActionPreference = 'Stop'
$gameRoot = Split-Path $PSScriptRoot -Parent
$repository = Split-Path $gameRoot -Parent
$output = Join-Path $gameRoot 'output'
New-Item -ItemType Directory -Force -Path $output | Out-Null
$reportPath = Join-Path $output 'campaign-validation.json'
$sourceFingerprint = & "$PSScriptRoot/Get-CampaignBuildFingerprint.ps1" -Repository $repository
$version = (& $Godot --version | Out-String).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Godot version check failed.' }
$report = [ordered]@{ Passed = $false; Fingerprint = $sourceFingerprint; GodotVersion = $version; Windowed = [bool]$Windowed; FinishedUtc = $null; Results = @() }
$report | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $reportPath -Encoding utf8
function Run-Scene([string]$Name, [string[]]$Extra = @(), [string]$Label = $Name) {
    $log = Join-Path $output "$Prefix-$Label.log"
    $errorLog = Join-Path $output "$Prefix-$Label.err"
    $arguments = @('--headless', '--max-fps', '60', '--path', "`"$gameRoot`"", "res://tests/$Name.tscn") + $Extra
    $process = Start-Process -FilePath $Godot -ArgumentList $arguments -WindowStyle Hidden -PassThru -RedirectStandardOutput $log -RedirectStandardError $errorLog
    try {
        if (-not $process.WaitForExit(300000)) { $process.Kill(); throw "$Label timed out after 300 seconds." }
        $code = $process.ExitCode
    } finally { if (-not $process.HasExited) { $process.Kill() } }
    Get-Content -LiteralPath $errorLog | Add-Content -LiteralPath $log
    $engineErrors = @(Select-String -LiteralPath $log -Pattern '^ERROR:|^SCRIPT ERROR:')
    $networkWarnings = @(Select-String -LiteralPath $log -Pattern 'above the MTU|different data|could not apply|RPC.*not found')
    $warnings = @(Select-String -LiteralPath $log -Pattern '^WARNING:').Count
    $report.Results += [ordered]@{ Name = $Label; ExitCode = $code; EngineErrors = $engineErrors.Count; Warnings = $warnings; Log = $log }
    Write-Output "$Label : exit=$code errors=$($engineErrors.Count) warnings=$warnings"
    if ($code -ne 0 -or $engineErrors.Count -gt 0 -or $networkWarnings.Count -gt 0) { throw "$Label failed. Inspect $log" }
}
function Run-Pair([string]$Scene, [string]$Label, [bool]$ShowWindow = $false) {
    & "$PSScriptRoot/Run-LobbyStartCheck.ps1" -Godot $Godot -Scene $Scene -Prefix "$Prefix-$Label" -Port 26891 -TimeoutSeconds 300 -Windowed:$ShowWindow
    $pairLogs = @("$output/$Prefix-$Label-host.err", "$output/$Prefix-$Label-client.err")
    foreach ($log in $pairLogs) {
        if (Select-String -LiteralPath $log -Pattern '^ERROR:|^SCRIPT ERROR:|above the MTU') { throw "Network process errors: $log" }
    }
    $report.Results += [ordered]@{ Name = $Label; ExitCode = 0; EngineErrors = 0; Warnings = @(Select-String -LiteralPath $pairLogs -Pattern '^WARNING:').Count; Log = $pairLogs }
}
Push-Location $repository
try {
    & $Godot --headless --editor --import --path $gameRoot *> "$output/$Prefix-import.log"
    if ($LASTEXITCODE -ne 0) { throw 'Godot import failed.' }
    & dotnet build "$gameRoot/HordeShooter.csproj" --nologo -v q *> "$output/$Prefix-build.log"
    if ($LASTEXITCODE -ne 0) { throw 'C# build failed.' }
    Run-Scene 'NetCodecCheck'
    Run-Scene 'CampaignDoorNetCheck'
    foreach ($number in 1, 2, 3) { Run-Scene 'CampaignInteractionAudit' @('--', "--audit-stage=$number") "campaign-$number" }
    foreach ($number in 1, 2) { Run-Scene 'CampaignInteractionAudit' @('--', "--audit-stage=$number", '--audit-short') "short-$number" }
    foreach ($scene in @('DestructionNetCheck', 'SurfaceAudioNetCheck', 'PartyNetCheck', 'NetSyncCheck', 'LobbyCheck', 'InteractionUiCheck', 'ElevationCheck', 'StageTransitionCheck', 'Integration')) { Run-Scene $scene }
    Run-Pair 'LobbyStartCheck' 'lobby'
    Run-Pair 'CampaignProgressionCheck' 'progression'
    if ($Windowed) { Run-Pair 'CampaignProgressionCheck' 'render' $true }
    $after = & "$PSScriptRoot/Get-CampaignBuildFingerprint.ps1" -Repository $repository
    if ($after -ne $sourceFingerprint) { throw 'Game files changed during validation; results do not certify this source.' }
    $report.Passed = $true
    Write-Output "CAMPAIGN_VALIDATION_PASS $sourceFingerprint"
} finally {
    $report.FinishedUtc = [DateTime]::UtcNow.ToString('o')
    $report | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $reportPath -Encoding utf8
    Pop-Location
}
