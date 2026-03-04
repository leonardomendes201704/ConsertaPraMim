# Plano de Rollout Progressivo - Google Calendar Sync Observabilidade (ST-013)

## 1. Objetivo

Liberar retry/dead-letter/reprocessamento da integracao Google Calendar com risco controlado e criterio objetivo de go/no-go.

## 2. Dependencias

- Migration aplicada: `20260304194940_AddGoogleCalendarSyncRetryDeadLetterObservability`
- Endpoint admin disponivel:
  - `GET /api/admin/google-calendar-sync/overview`
  - `POST /api/admin/google-calendar-sync/reprocess`
- Worker ativo: `GoogleCalendarSyncRetryWorker`

## 3. Estrategia por ambiente

## 3.1 Dev

- Habilitar:
  - `GoogleCalendarSync:Enabled=true`
  - `GoogleCalendarSync:RetryEnabled=true`
  - `GoogleCalendarSync:RetryWorkerEnabled=true`
- Executar QA completo (manual ST-013).
- Validar cenarios:
  - sucesso create/update/delete
  - falha retryable
  - dead-letter + reprocess.

## 3.2 Homolog

- Janela inicial com volume controlado (amostra de pedidos internos).
- Monitorar por 24h:
  - `DeadLetterCount`
  - `RetryQueueCount`
  - `P95LatencyMs`
- Executar ao menos 1 reprocessamento manual por intervalo.

## 3.3 Producao

- Habilitacao em horario de baixa carga.
- Monitoramento intensivo nas primeiras 2h e depois em janelas de 6h.
- Reprocessar dead-letter somente apos causa raiz corrigida.

## 4. Criterios go/no-go

## Go

- Build e migration aplicados sem erro.
- `overview` respondendo com dados coerentes.
- `DeadLetterCount` sem crescimento anormal.
- `P95LatencyMs` em faixa operacional aceitavel.

## No-go

- Falha de autenticacao/permissao sem resolucao.
- Crescimento acelerado de dead-letter.
- Worker sem consumo de fila de retry por mais de 2 ciclos.

## 5. Plano de rollback

1. `GoogleCalendarSync:Enabled=false`.
2. Validar que fluxo principal de agendamento permanece operacional.
3. Registrar incidente e abrir acao corretiva.
4. Reativar somente apos novo smoke test.

## 6. Operacao recorrente

- Revisao diaria de `overview` no inicio do turno.
- Reprocessamento manual em lotes pequenos para evitar efeito cascata.
- Revisao semanal de erros mais frequentes (`LastErrorCode`).
