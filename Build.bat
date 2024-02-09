@echo off
SET scriptPath=%~dp0Build.ps1
powershell.exe -ExecutionPolicy Bypass -File "%scriptPath%"
SET PS_EXIT_CODE=%errorlevel%
exit /b %PS_EXIT_CODE%

