[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$RepoRoot,
    [switch]$Apply
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path -LiteralPath $RepoRoot).Path
if (-not (Test-Path -LiteralPath (Join-Path $repo 'game/project.godot') -PathType Leaf)) {
    throw 'RepoRoot must contain game/project.godot.'
}
$destination = [IO.Path]::GetFullPath((Join-Path $repo 'game/assets/outdoor-kit'))
if (Test-Path -LiteralPath $destination) {
    throw "Destination already exists. Nothing overwritten: $destination"
}
$source = Join-Path $PSScriptRoot 'pack'
$files = Get-ChildItem -LiteralPath $source -Recurse -File | Where-Object { $_.Extension -in '.glb', '.json', '.gdshader' }
$manifest = Get-Content -LiteralPath (Join-Path $source 'manifest.json') -Raw | ConvertFrom-Json
if (($files | Where-Object Extension -eq '.glb').Count -ne $manifest.assets.Count) { throw 'GLB count does not match manifest.' }
Write-Output "Source: $source"
Write-Output "Destination: $destination"
Write-Output "Files: $($files.Count)"
if (-not $Apply) {
    Write-Output 'DRY RUN. Add -Apply to copy new asset files. No existing code or docs will be modified.'
    return
}
# Fail-on-exists creation claims a new destination without deleting/merging any folder.
New-Item -ItemType Directory -Path $destination -ErrorAction Stop | Out-Null
foreach ($file in $files) {
    $relative = [IO.Path]::GetRelativePath($source, $file.FullName)
    $target = Join-Path $destination $relative
    $parent = Split-Path -Parent $target
    if (-not (Test-Path -LiteralPath $parent)) { New-Item -ItemType Directory -Path $parent | Out-Null }
    [IO.File]::Copy($file.FullName, $target, $false)
    if ((Get-FileHash -LiteralPath $file.FullName).Hash -ne (Get-FileHash -LiteralPath $target).Hash) {
        throw "Hash mismatch: $relative"
    }
}
Write-Output 'COPIED and SHA256 verified. Maps, collision, occlusion detection and gameplay are not connected.'
