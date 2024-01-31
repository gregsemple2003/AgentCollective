@echo off
SET scriptPath=%~dp0Build.ps1
powershell.exe -ExecutionPolicy Bypass -File "%scriptPath%"
pause
