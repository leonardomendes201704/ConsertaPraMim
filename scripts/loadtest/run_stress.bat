@echo off
setlocal
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0run_loadtest.ps1" -Scenario "stress"
exit /b %errorlevel%

