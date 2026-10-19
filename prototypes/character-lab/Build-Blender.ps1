param([string]$GlbPath, [string]$Blender = 'C:\Util\Blender Foundation\Blender 5.2\blender.exe')
$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $Blender)) { throw "Blender not found: $Blender" }
$builder = Join-Path $PSScriptRoot 'tools\build_blender.py'
if ($GlbPath) {
    $source = (Resolve-Path -LiteralPath $GlbPath).Path
    & $Blender --background --python $builder -- $source
} else {
    & $Blender --background --python $builder
}
if ($LASTEXITCODE -ne 0) { throw 'Blender build failed' }
