@echo off
setlocal

set "ROOT_DIR=%~dp0..\.."
for %%I in ("%ROOT_DIR%") do set "ROOT_DIR=%%~fI"
set "WPF_PROJECT=%ROOT_DIR%\Backend\src\ConsertaPraMim.LoadTest.Wpf\ConsertaPraMim.LoadTest.Wpf.csproj"

if not exist "%WPF_PROJECT%" (
  echo [ERRO] Projeto WPF nao encontrado: "%WPF_PROJECT%"
  exit /b 1
)

echo Abrindo ConsertaPraMim Load Test GUI (WPF)...
dotnet run --project "%WPF_PROJECT%"
exit /b %ERRORLEVEL%
