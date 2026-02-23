@echo off
setlocal

set SCRIPT_DIR=%~dp0
set RUNNER_PS1=%SCRIPT_DIR%run_loadtest_gui.ps1

powershell -NoProfile -ExecutionPolicy Bypass -File "%RUNNER_PS1%" %*
if errorlevel 1 (
  exit /b %errorlevel%
)

endlocal
