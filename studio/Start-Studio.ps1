param(
    [string]$GodotDir = 'D:\Util\Godot\Godot_v4.7.2-stable_mono_win64',
    [ValidateRange(1024, 65535)][int]$Port = 4317
)
$ErrorActionPreference = 'Stop'
$url = "http://127.0.0.1:$Port"
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..')).TrimEnd('\', '/')

function Get-StudioState {
    try { $state = Invoke-RestMethod "$url/api/health" -TimeoutSec 2 -UseBasicParsing }
    catch { return $null }
    if ($state.app -ne 'plush-panic-studio' -or !$state.workspace -or
        [IO.Path]::GetFullPath($state.workspace).TrimEnd('\', '/') -ne $workspace) {
        throw "Port $Port is used by another application or workspace. Use -Port with a different port."
    }
    return $state
}

# Hold the port lock through build/import and the server lifetime, including rapid button clicks.
$mutex = New-Object System.Threading.Mutex($false, "Local\PlushPanicStudio-$Port")
$ownsMutex = $false
try {
    try { $ownsMutex = $mutex.WaitOne(0) }
    catch [System.Threading.AbandonedMutexException] { $ownsMutex = $true }
    $state = Get-StudioState
    if (!$ownsMutex -or $null -ne $state) {
        Write-Host "Waiting for existing Studio: $url"
        $deadline = (Get-Date).AddMinutes(3)
        $restarted = $false
        while (!$state.ready) {
            # A running Studio whose Godot renderer stopped never gets ready by itself: ask it once to restart the renderer.
            if ($null -ne $state -and $state.renderer -eq 'stopped' -and !$restarted) {
                Write-Host "Studio renderer stopped ($($state.error)). Restarting it: build, import, renderer."
                try { Invoke-RestMethod -Method Post "$url/api/reload" -TimeoutSec 2 -UseBasicParsing | Out-Null } catch { }
                $restarted = $true
                $deadline = (Get-Date).AddMinutes(5)
            }
            if ((Get-Date) -gt $deadline) {
                if ($null -eq $state) { throw "Studio did not become ready within the wait: $url" }
                # The web server is up: md and planning pages work without the renderer, previews retry from the page.
                Write-Warning "Studio renderer is not ready ($($state.renderer) $($state.error)). Opening the page anyway; previews may fail until it is."
                break
            }
            if (!$ownsMutex) {
                try { $released = $mutex.WaitOne(0) }
                catch [System.Threading.AbandonedMutexException] { $released = $true }
                if ($released) {
                    $mutex.ReleaseMutex()
                    throw 'The other Studio launcher stopped before it became ready. Run this script again.'
                }
            }
            Start-Sleep -Milliseconds 500
            $state = Get-StudioState
        }
        Start-Process $url
        return
    }

    # A previous Studio version has no health endpoint; do not start a competing renderer.
    $probe = New-Object System.Net.Sockets.TcpClient
    try {
        try { $probe.Connect('127.0.0.1', $Port) } catch { }
        if ($probe.Connected) { throw "Port $Port is already occupied. Stop the old Studio once and retry, or use -Port." }
    } finally { $probe.Dispose() }

    $godot = Join-Path $GodotDir 'Godot_v4.7.2-stable_mono_win64_console.exe'
    if (-not (Test-Path -LiteralPath $godot)) { throw "Godot .NET not found: $godot" }
    if (-not (Get-Command node -ErrorAction SilentlyContinue)) { throw 'Node.js is required.' }
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw '.NET SDK is required.' }
    $opened = $false
    & node (Join-Path $PSScriptRoot 'server.mjs') --godot $godot --port $Port | ForEach-Object {
        Write-Host $_
        if (!$opened -and $_ -eq "Studio ready: $url") {
            $opened = $true
            try { Start-Process $url }
            catch { Write-Warning "Could not open the browser. Open $url manually. $($_.Exception.Message)" }
        }
    }
    if ($LASTEXITCODE -ne 0) { throw 'Studio stopped with an error.' }
} finally {
    if ($ownsMutex) { $mutex.ReleaseMutex() }
    $mutex.Dispose()
}
