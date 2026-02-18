# Runbook ST-019 - Monitoramento E2E da API

Data base: 2026-02-18

## Objetivo

Configurar, validar e operar a trilha de monitoramento E2E da API (telemetria de requests, agregacao periodica e dashboard no portal admin).

## Configuracoes principais

Arquivo base: `Backend/src/ConsertaPraMim.API/appsettings*.json`

Secao `Monitoring`:

- `Enabled`: liga/desliga captura no middleware.
- `CaptureSwaggerRequests`: inclui/exclui requests de swagger na telemetria.
- `IpHashSalt`: salt para hash anonimo de IP.
- `TelemetryBuffer:Capacity`: capacidade maxima do buffer em memoria.
- `FlushWorker:Enabled`: liga/desliga worker de flush para eventos brutos.
- `FlushWorker:BatchSize`: tamanho do lote por flush.
- `FlushWorker:IntervalSeconds`: intervalo entre ciclos de flush.
- `AggregationWorker:Enabled`: liga/desliga agregacao periodica.
- `AggregationWorker:IntervalSeconds`: intervalo entre ciclos de agregacao.
- `AggregationWorker:HourlyRecomputeWindowHours`: janela de recomputo horario.
- `AggregationWorker:DailyRecomputeWindowDays`: janela de recomputo diario.
- `Retention:RawDays`: retencao de eventos brutos.
- `Retention:AggregateDays`: retencao de agregados/ocorrencias.

## Validacao local (passo a passo)

1. Aplicar schema:
- `dotnet ef database update --project ConsertaPraMim.Infrastructure --startup-project ConsertaPraMim.API`

2. Subir API:
- `dotnet run --project ConsertaPraMim.API`

3. Validar endpoints de monitoramento (admin):
- `GET /api/admin/monitoring/overview?range=24h`
- `GET /api/admin/monitoring/top-endpoints?range=24h`
- `GET /api/admin/monitoring/latency?range=24h`
- `GET /api/admin/monitoring/errors?range=24h&groupBy=type`
- `GET /api/admin/monitoring/requests?range=24h&page=1&pageSize=20`

4. Abrir portal admin e acessar:
- Menu `Monitoramento`
- Validar cards, graficos, tabelas e drilldown por request/correlationId

## Checklist operacional

- [ ] Captura de request ativa sem impacto perceptivel na latencia.
- [ ] Buffer nao saturado de forma recorrente.
- [ ] Flush persistindo registros em `ApiRequestLogs`.
- [ ] Aggregation atualizando tabelas `ApiEndpointMetricsHourly` e `ApiEndpointMetricsDaily`.
- [ ] Catalogo de erros e ocorrencias por hora atualizando.
- [ ] Politica de retencao removendo dados fora da janela.
- [ ] Dashboard admin com filtros por range, endpoint, status, severidade.

## Troubleshooting rapido

- Dashboard sem dados:
1. Validar `Monitoring:Enabled=true`.
2. Verificar logs de flush worker.
3. Consultar tabela `ApiRequestLogs`.

- Dados brutos existem, mas agregados vazios:
1. Validar `AggregationWorker:Enabled=true`.
2. Verificar intervalo e janelas (`HourlyRecomputeWindowHours`, `DailyRecomputeWindowDays`).
3. Conferir logs de erro no worker de agregacao.

- Queda de performance:
1. Reduzir granularidade de consultas no dashboard.
2. Ajustar `BatchSize`/`IntervalSeconds`.
3. Revisar indices nas tabelas de monitoramento.

## Seguranca e LGPD

- Nao persistir payload bruto de request/response.
- Nao registrar segredos (token, senha, cpf, etc.).
- IP somente em hash anonimizado.
- Acesso aos endpoints de monitoramento restrito por policy `AdminOnly`.
