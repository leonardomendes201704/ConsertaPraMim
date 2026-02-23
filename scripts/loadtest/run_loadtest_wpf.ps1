param()

$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootDir = (Resolve-Path (Join-Path $scriptDir "..\..")).Path
$projectPath = Join-Path $rootDir "Backend\src\ConsertaPraMim.LoadTest.Wpf\ConsertaPraMim.LoadTest.Wpf.csproj"

if (-not (Test-Path $projectPath)) {
    Write-Error "Projeto WPF nao encontrado: $projectPath"
}

Write-Host "Abrindo ConsertaPraMim Load Test GUI (WPF)..."
dotnet run --project $projectPath
