@echo off
setlocal

set SCRIPT_DIR=%~dp0
set RUNNER_PS1=%SCRIPT_DIR%run_loadtest.ps1

if "%~1"=="" (
  set SCENARIO=smoke
) else (
  set SCENARIO=%~1
)

powershell -NoProfile -ExecutionPolicy Bypass -File "%RUNNER_PS1%" -Scenario "%SCENARIO%"
if errorlevel 1 (
  exit /b %errorlevel%
)

endlocal

