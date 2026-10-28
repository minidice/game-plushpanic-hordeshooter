param([string]$Repository = (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent))
$ErrorActionPreference = 'Stop'
$repositoryPath = (Resolve-Path -LiteralPath $Repository).Path
$files = & git -C $repositoryPath -c core.quotePath=false ls-files --cached --others --exclude-standard -- game
if ($LASTEXITCODE -ne 0) { throw 'Cannot enumerate game source for validation fingerprint.' }
$lines = foreach ($file in ($files | Sort-Object -Unique)) {
    $path = Join-Path $repositoryPath $file
    if (Test-Path -LiteralPath $path -PathType Leaf) { "$file $((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash)" }
    else { "$file MISSING" }
}
$bytes = [Text.Encoding]::UTF8.GetBytes(($lines -join "`n"))
$sha = [Security.Cryptography.SHA256]::Create()
try { ([BitConverter]::ToString($sha.ComputeHash($bytes))).Replace('-', '').ToLowerInvariant() }
finally { $sha.Dispose() }
