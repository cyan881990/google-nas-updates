@echo off
if exist "%~dp0GoogleNasSetup.exe" (
    start "" "%~dp0GoogleNasSetup.exe"
    exit /b
)
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1"
if errorlevel 1 pause
