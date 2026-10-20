@echo off
rem Builds a copy of the game for another PC: build\windows (and build\HordeShooter-windows.zip).
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Export-Game.ps1" %*
pause
