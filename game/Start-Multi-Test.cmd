@echo off
rem Two game windows on this PC: the first hosts, the second joins it (doc/multiplayer-sync-design.md).
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Start-Game.ps1" -NetPair
if errorlevel 1 pause
