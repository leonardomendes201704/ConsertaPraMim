param(
    [int]$Port = 8501,
    [string]$Address = "localhost"
)

$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$Dashboard = Join-Path $ScriptDir "live_dashboard.py"

if (-not (Test-Path $Dashboard)) {
    throw "Dashboard nao encontrado: $Dashboard"
}

$python = "python"
try {
    & $python --version | Out-Null
}
catch {
    $python = "py"
}

Write-Host "Iniciando GUI de load test em http://$Address`:$Port ..."
& $python -m streamlit run $Dashboard --server.port $Port --server.address $Address
$exitCode = $LASTEXITCODE

if ($exitCode -ne 0) {
    throw "Falha ao iniciar streamlit (codigo $exitCode). Instale dependencias com: pip install -r scripts/loadtest/requirements.txt"
}
