$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
if (-not (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'node_modules\three')) -or -not (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'node_modules\@dimforge\rapier3d-compat'))) {
    & npm install --ignore-scripts --no-audit --no-fund
    if ($LASTEXITCODE -ne 0) { throw 'Dependency installation failed' }
}
$alreadyRunning = $false
try { $alreadyRunning = (Invoke-WebRequest -Uri 'http://127.0.0.1:4173/' -TimeoutSec 2).Content -match '캐릭터 비교실' } catch {}
if (-not $alreadyRunning) {
    $node = (Get-Command node -ErrorAction Stop).Source
    Start-Process -FilePath $node -ArgumentList 'tools/serve.mjs' -WorkingDirectory $PSScriptRoot -WindowStyle Hidden
    Start-Sleep -Milliseconds 700
}
Start-Process 'http://127.0.0.1:4173/'
