# Load Test - ConsertaPraMim API

Gerador de carga/stress da API com clientes virtuais paralelos, headers de identificacao/correlacao e relatorios de performance/erros.

## Arquivos

- `loadtest_runner.py`: engine principal de carga (`asyncio + httpx`)
- `loadtest.config.json`: configuracao de baseUrl, auth, cenarios e mix ponderado
- `run_loadtest.ps1`: script principal no Windows
- `run_loadtest.bat`: atalho para execucao rapida
- `run_smoke.bat` / `run_baseline.bat` / `run_stress.bat`: atalhos por cenario
- `run_loadtest.sh`: atalho bash (Linux/macOS)
- `requirements.txt`: dependencias Python
- `output/`: relatorios gerados

## Recursos implementados

- Clientes virtuais paralelos (`vus`) com **X-Client-Id fixo por VU**
- **X-Correlation-Id unico por request**
- Suporte a `X-Tenant-Id` (opcional no config)
- Mix de endpoints por peso (`weight`)
- Ramp-up e think time aleatorio
- Injeção de erro controlada (`errorInjectionRatePercent`) usando variantes invalidas de endpoint
- Login com token bearer (quando configurado)
- Saida completa no terminal com:
  - total requests
  - sucesso/falha
  - RPS medio e pico
  - latencia min/media/max + p50/p95/p99
  - erros por status code
  - exceptions/timeouts
  - top endpoints por hits e por p95
  - top erros normalizados
  - 10 amostras de falhas com correlationId
- Relatorios em arquivo:
  - `loadtest-report-<runId>.json`
  - `loadtest-summary-<runId>.txt`
  - `loadtest-report-<runId>.html`
  - `loadtest-report-latest.json`
  - `loadtest-summary-latest.txt`
  - `loadtest-report-latest.html`

## Pre-requisitos

- Python 3.10+
- API em execucao (exemplo: `http://localhost:5193`)

Instalar dependencias:

```powershell
pip install -r scripts/loadtest/requirements.txt
```

## Como executar

> Recomendacao: use `durationSeconds` maior que `rampUpSeconds` para garantir volume util de requests.

### PowerShell

```powershell
powershell -ExecutionPolicy Bypass -File scripts/loadtest/run_loadtest.ps1 -Scenario smoke
powershell -ExecutionPolicy Bypass -File scripts/loadtest/run_loadtest.ps1 -Scenario baseline
powershell -ExecutionPolicy Bypass -File scripts/loadtest/run_loadtest.ps1 -Scenario stress
```

### CMD

```cmd
scripts\loadtest\run_smoke.bat
scripts\loadtest\run_baseline.bat
scripts\loadtest\run_stress.bat
```

### Execucao direta

```powershell
python scripts/loadtest/loadtest_runner.py --scenario smoke
python scripts/loadtest/loadtest_runner.py --scenario baseline --base-url http://192.168.0.196:5193
python scripts/loadtest/loadtest_runner.py --scenario stress --insecure
```

### Bash (Linux/macOS)

```bash
chmod +x scripts/loadtest/run_loadtest.sh
./scripts/loadtest/run_loadtest.sh smoke
```

## Overrides uteis

```powershell
python scripts/loadtest/loadtest_runner.py --scenario smoke --vus 50 --duration 120
python scripts/loadtest/loadtest_runner.py --scenario baseline --base-url http://localhost:5193 --auth-password "SUA_SENHA"
```

Parametros suportados:

- `--config`
- `--scenario`
- `--base-url`
- `--vus`
- `--duration`
- `--ramp-up`
- `--think-min`
- `--think-max`
- `--timeout`
- `--insecure`
- `--seed`
- `--output-dir`
- `--auth-password`

## Configuracao (`loadtest.config.json`)

- `baseUrl`: URL da API
- `auth`: login e contas de teste
- `scenarios`: `smoke`, `baseline`, `stress`
- `endpoints`: lista de rotas com `method`, `path`, `weight`, `auth` e opcoes de captura/erro

Exemplo de endpoint com captura de IDs para drilldown:

- `capture: "client_order_ids"` no endpoint de listagem
- endpoint de detalhe usa `path` com `{orderId}`

## Staging/ambiente remoto

Basta trocar `baseUrl` no config ou via argumento `--base-url`.

Exemplo:

```powershell
python scripts/loadtest/loadtest_runner.py --scenario baseline --base-url https://sua-api-staging
```

## Observacoes de seguranca

- Evite credenciais reais no arquivo de configuracao.
- Use `--auth-password` em runtime para sobrescrever senha.
- Nao rode cenarios de stress em producao sem janela controlada.

## Integracao com dashboard admin

Este pacote implementa o gerador + relatorios locais completos.
A publicacao persistente de runs no admin pode ser adicionada em etapa seguinte com endpoints dedicados (`/api/admin/loadtests/*`).

