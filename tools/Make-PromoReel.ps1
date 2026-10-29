param(
    [string]$GodotDir = 'D:\Util\Godot\Godot_v4.7.2-stable_mono_win64',
    # Video size, WxH (16:9; the camera framing is made for 16:9).
    [string]$Resolution = '1920x1080',
    # Stills every 30 m along each stage's main route instead of the reel (output/promo/scout/), for picking new shots.
    [switch]$Scout,
    # Also make output/promo/promo.mp4 (H.264 + AAC) with ffmpeg, if ffmpeg is on PATH.
    [switch]$Mp4
)
# Promo reel (game/tests/PromoReel.cs): four AI survivors fighting through the better-looking parts of the large
# campaign, recorded with Godot's Movie Maker at a fixed 60 fps with the game's sound -> game/output/promo/promo.avi.
$ErrorActionPreference = 'Stop'
$game = Join-Path (Split-Path $PSScriptRoot -Parent) 'game'
$console = Join-Path $GodotDir 'Godot_v4.7.2-stable_mono_win64_console.exe'
if (-not (Test-Path -LiteralPath $console)) { throw "Godot 4.7.2 .NET not found in $GodotDir. Pass -GodotDir." }
$out = Join-Path $game 'output/promo'
New-Item -ItemType Directory -Force -Path $out | Out-Null

Push-Location $game
try {
    & dotnet build HordeShooter.csproj --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw 'C# build failed.' }
    & $console --headless --editor --import --path $game --quit *> (Join-Path $game 'output/import.log')
    if ($Scout) {
        & $console --path $game res://tests/PromoReel.tscn -- --promo-scout
        if ($LASTEXITCODE -ne 0) { throw 'Scout stills failed.' }
        Write-Host "Stills: $out\scout"
        return
    }
    $avi = Join-Path $out 'promo.avi'
    # Movie Maker records at the project's 1600x900 even with --resolution; a window size override while recording
    # gives the full size (3D renders at the window size, the overlay scales with canvas_items stretch).
    $override = Join-Path $game 'override.cfg'
    if (Test-Path -LiteralPath $override) { throw "$override already exists; remove it first." }
    $w, $h = $Resolution -split 'x'
    [IO.File]::WriteAllText($override, "[display]`n`nwindow/size/window_width_override=$w`nwindow/size/window_height_override=$h`n")
    try {
        & $console --path $game --fixed-fps 60 --write-movie $avi res://tests/PromoReel.tscn
        if ($LASTEXITCODE -ne 0) { throw 'Recording failed.' }
    } finally {
        Remove-Item -LiteralPath $override
    }
    Write-Host "Recorded: $avi"
    if ($Mp4) {
        if (-not (Get-Command ffmpeg -ErrorAction SilentlyContinue)) { throw 'ffmpeg is not on PATH (winget install Gyan.FFmpeg), or open promo.avi in Clipchamp.' }
        $mp4 = Join-Path $out 'promo.mp4'
        & ffmpeg -y -loglevel error -i $avi -c:v libx264 -preset slow -crf 18 -pix_fmt yuv420p -c:a aac -b:a 192k -movflags +faststart $mp4
        if ($LASTEXITCODE -ne 0) { throw 'ffmpeg failed.' }
        Write-Host "MP4: $mp4"
    }
} finally {
    Pop-Location
}
