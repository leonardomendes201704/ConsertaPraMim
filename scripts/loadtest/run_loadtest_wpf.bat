@echo off
setlocal

set "ROOT_DIR=%~dp0..\.."
for %%I in ("%ROOT_DIR%") do set "ROOT_DIR=%%~fI"
set "WPF_PROJECT=%ROOT_DIR%\Backend\src\ConsertaPraMim.LoadTest.Wpf\ConsertaPraMim.LoadTest.Wpf.csproj"
set "WPF_PROJECT_DIR=%ROOT_DIR%\Backend\src\ConsertaPraMim.LoadTest.Wpf"
set "CONFIG=Debug"

if not exist "%WPF_PROJECT%" (
  echo [ERRO] Projeto WPF nao encontrado: "%WPF_PROJECT%"
  exit /b 1
)

echo Restaurando dependencias do projeto WPF...
dotnet restore "%WPF_PROJECT%"
if errorlevel 1 (
  echo [ERRO] Falha no restore do projeto WPF.
  exit /b 1
)

echo Compilando projeto WPF...
dotnet build "%WPF_PROJECT%" -c %CONFIG% --nologo
if errorlevel 1 (
  echo [ERRO] Falha no build do projeto WPF.
  exit /b 1
)

set "TFM="
for /f "usebackq delims=" %%F in (`dotnet msbuild "%WPF_PROJECT%" -nologo -getproperty:TargetFramework`) do (
  if not defined TFM set "TFM=%%F"
)

if not defined TFM (
  echo [ERRO] Nao foi possivel resolver TargetFramework do projeto WPF.
  exit /b 1
)

set "WPF_EXE=%WPF_PROJECT_DIR%\bin\%CONFIG%\%TFM%\ConsertaPraMim.LoadTest.Wpf.exe"
if not exist "%WPF_EXE%" (
  echo [ERRO] Binario WPF nao encontrado: "%WPF_EXE%"
  exit /b 1
)

echo Abrindo ConsertaPraMim Load Test GUI (WPF)...
start "" "%WPF_EXE%" %*
exit /b %ERRORLEVEL%
