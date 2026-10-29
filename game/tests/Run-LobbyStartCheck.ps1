param(
    [string]$Godot = 'D:/Util/Godot/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe',
    [int]$Port = 26789,
    [ValidatePattern('^[a-zA-Z0-9-]+$')][string]$Prefix = 'lobby-start',
    [switch]$Windowed,
    [ValidateSet('LobbyStartCheck', 'CampaignProgressionCheck')][string]$Scene = 'LobbyStartCheck',
    [int]$TimeoutSeconds = 120
)
$ErrorActionPreference = 'Stop'
$gameRoot = Split-Path $PSScriptRoot -Parent
$testProcesses = @()
try {
    foreach ($role in @('host', 'client')) {
        $argsForTest = @('--path', "`"$gameRoot`"", '--max-fps', '60')
        if (-not $Windowed) { $argsForTest += '--headless' }
        else { $argsForTest += @('--resolution', '1280x720') }
        $argsForTest += @("res://tests/$Scene.tscn", '--', "--role=$role", "--port=$Port")
        $process = Start-Process -FilePath $Godot -ArgumentList $argsForTest -WindowStyle Hidden -PassThru -RedirectStandardOutput "$gameRoot/output/$Prefix-$role.log" -RedirectStandardError "$gameRoot/output/$Prefix-$role.err"
        # Retain the handle before exit so PowerShell 5.1 preserves the redirected child's ExitCode.
        $null = $process.Handle
        $testProcesses += $process
        if ($role -eq 'host') { Start-Sleep -Seconds 2 }
    }
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while (($testProcesses | Where-Object { -not $_.HasExited }) -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 500 }
    foreach ($process in $testProcesses) {
        if (-not $process.HasExited) { throw 'Lobby start check timed out.' }
        Write-Output "Process $($process.Id) exit=$($process.ExitCode)"
        if ($process.ExitCode -ne 0) { throw 'Lobby start check failed. Inspect the role logs.' }
    }
} finally {
    foreach ($process in $testProcesses) { if (-not $process.HasExited) { $process.Kill() }; $process.Dispose() }
}
