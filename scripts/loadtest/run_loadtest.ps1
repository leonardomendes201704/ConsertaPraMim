param(
    [string]$Scenario = "smoke",
    [string]$BaseUrl = "",
    [string]$Config = "",
    [int]$Vus = 0,
    [int]$Duration = 0,
    [switch]$Insecure,
    [string]$AuthPassword = ""
)

$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$Runner = Join-Path $ScriptDir "loadtest_runner.py"
$ConfigPath = if ([string]::IsNullOrWhiteSpace($Config)) { Join-Path $ScriptDir "loadtest.config.json" } else { $Config }
$OutputDir = Join-Path $ScriptDir "output"

if (-not (Test-Path $Runner)) {
    throw "Runner nao encontrado: $Runner"
}

$python = "python"
try {
    & $python --version | Out-Null
}
catch {
    $python = "py"
}

$args = @($Runner, "--config", $ConfigPath, "--scenario", $Scenario, "--output-dir", $OutputDir)

if (-not [string]::IsNullOrWhiteSpace($BaseUrl)) {
    $args += @("--base-url", $BaseUrl)
}
if ($Vus -gt 0) {
    $args += @("--vus", "$Vus")
}
if ($Duration -gt 0) {
    $args += @("--duration", "$Duration")
}
if ($Insecure) {
    $args += "--insecure"
}
if (-not [string]::IsNullOrWhiteSpace($AuthPassword)) {
    $args += @("--auth-password", $AuthPassword)
}

Write-Host "Executando load test scenario '$Scenario'..."
& $python @args
$exitCode = $LASTEXITCODE

if ($exitCode -ne 0) {
    throw "Load test falhou com codigo $exitCode"
}

Write-Host "Concluido. Relatorios em: $OutputDir"

