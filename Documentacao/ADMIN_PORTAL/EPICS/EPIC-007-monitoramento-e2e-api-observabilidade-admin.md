# EPIC-007 - Monitoramento E2E da API com dashboard operacional no Admin

Status: In Progress
Trilha: ADMIN_PORTAL

## Objetivo

Implantar observabilidade ponta a ponta da API, com captura global de telemetria de requests, agregacao de metricas operacionais e visualizacao em dashboard no portal admin.

## Problema de negocio

- Nao existe visao consolidada de saude operacional por endpoint (volume, latencia, erros e warnings).
- Diagnosticos hoje dependem de investigacao manual e logs dispersos.
- Falta trilha de correlacao por request para troubleshooting rapido e governanca de incidente.

## Resultado esperado

- Middleware global de telemetria em todos os requests.
- Persistencia de eventos brutos com retencao curta e agregados por janela de tempo.
- APIs admin dedicadas para analytics e drilldown operacional.
- Dashboard no portal admin com filtros, graficos, tabelas e detalhe por correlationId.
- Solucao resiliente, com mascaramento/sanitizacao e baixo impacto de performance.

## Historias vinculadas

- ST-019 - Monitoramento E2E da API com telemetria, agregacao e dashboard admin.
