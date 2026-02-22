# ST-030 - Dashboard executivo e monitoramento compacto

Status: In Progress
Epic: EPIC-010

## Objetivo

Entregar visao compacta de operacao para admin no mobile, com KPIs de dashboard e dados essenciais de monitoramento da API.

## Criterios de aceite

- Dashboard mobile consome `/api/admin/dashboard`.
- Tela de monitoramento consome `/api/admin/monitoring/overview` e `/api/admin/monitoring/top-endpoints`.
- Usuario consegue alternar faixa temporal de monitoramento (`1h`, `24h`, `7d`).
- UI compacta com foco em leitura rapida e refresh manual.

## Tasks

- [ ] Implementar servico `mobileAdmin.ts` para dashboard e monitoramento.
- [ ] Criar componentes `Dashboard` e `MonitoringPanel`.
- [ ] Integrar navegacao entre home e monitoramento.
- [ ] Implementar estados de loading, erro e vazio.
