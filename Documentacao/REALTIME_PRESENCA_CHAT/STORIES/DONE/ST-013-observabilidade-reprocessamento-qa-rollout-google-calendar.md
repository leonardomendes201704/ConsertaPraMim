# ST-013 - Observabilidade, reprocessamento, QA e rollout da sincronizacao Google Calendar

Status: Done  
Epic: EPIC-003

## Objetivo

Fechar a operacao da sincronizacao com monitoramento, retentativas controladas, ferramentas de reprocessamento e plano de rollout seguro.

## Criterios de aceite

- Dashboard operacional mostra sucesso/erro/latencia da sincronizacao.
- Falhas transientes entram em retry com backoff e limite configuravel.
- Falhas permanentes ficam em fila de reprocessamento manual.
- Runbook cobre diagnostico, correcoes comuns e rollback.
- Feature flag permite ativar/desativar sync por ambiente.

## Tasks

- [x] Instrumentar metricas de sincronizacao (`created`, `updated`, `deleted`, `failed`, `retry_count`, `latency_ms`).
- [x] Implementar politicas de retry com backoff exponencial e jitter.
- [x] Implementar dead-letter logica para falhas permanentes.
- [x] Criar endpoint/admin job para reprocessar sincronizacao pendente por intervalo ou `appointmentId`.
- [x] Adicionar logs estruturados com `appointmentId`, `googleEventId`, `providerId`, `traceId`.
- [x] Implementar guardrail para nao confirmar sincronizacao quando API Google retornar erro.
- [x] Criar plano QA com cenarios happy path, falhas de autenticacao, timeout e cancelamento.
- [x] Criar runbook operacional de incidentes e recuperacao da integracao.
- [x] Definir rollout progressivo por ambiente (dev -> homolog -> prod) com criterio de go/no-go.

## Entregas realizadas

1. Dominio e persistencia da trilha de observabilidade/retry:
   - `ServiceAppointmentCalendarSync` ampliado com `LastOperation`, `RetryCount`, `MaxRetryAttempts`, `NextRetryAtUtc`, `DeadLetterAtUtc`, `LastLatencyMs`, `LastErrorCode`.
   - Novo status `DeadLetter` em `ServiceAppointmentCalendarSyncStatus`.
   - Migration: `20260304194940_AddGoogleCalendarSyncRetryDeadLetterObservability`.
2. Operacao de retry/reprocessamento:
   - Novo servico `GoogleCalendarSyncOperationsService` com:
     - processamento de retries due (`ProcessDueRetriesAsync`),
     - reprocessamento manual por `appointmentId` ou intervalo (`ReprocessAsync`),
     - resumo operacional (`GetOverviewAsync`).
   - Worker dedicado `GoogleCalendarSyncRetryWorker` para varrer fila de retry com batch/intervalo configuraveis.
3. API administrativa:
   - `GET /api/admin/google-calendar-sync/overview`
   - `POST /api/admin/google-calendar-sync/reprocess`
4. Guardrails e rastreabilidade:
   - Sincronizacao so transita para `Synced/Deleted` quando a chamada Google retorna sucesso.
   - Falha retryable mantem `Failed` + agenda `NextRetryAtUtc`.
   - Falha permanente vai para `DeadLetter` com `LastErrorCode` e `Error` truncado.
   - Logs estruturados com `appointmentId`, `googleEventId`, `providerId`, `traceId`, `trigger`, `operation`.
5. Documentacao operacional:
   - Plano QA atualizado no manual de Google Calendar.
   - Runbook novo para incidentes e recuperacao da integracao.
   - Rollout progressivo documentado com criterio de go/no-go.

## Evidencias tecnicas

- API:
  - `Backend/src/ConsertaPraMim.API/Controllers/AdminGoogleCalendarSyncController.cs`
  - `Backend/src/ConsertaPraMim.API/BackgroundJobs/GoogleCalendarSyncRetryWorker.cs`
- Application:
  - `Backend/src/ConsertaPraMim.Application/Services/GoogleCalendarSyncOperationsService.cs`
  - `Backend/src/ConsertaPraMim.Application/Services/GoogleCalendarSyncTelemetry.cs`
  - `Backend/src/ConsertaPraMim.Application/DTOs/GoogleCalendarSyncDTOs.cs`
- Domain/Infra:
  - `Backend/src/ConsertaPraMim.Domain/Entities/ServiceAppointmentCalendarSync.cs`
  - `Backend/src/ConsertaPraMim.Domain/Enums/Enums.cs`
  - `Backend/src/ConsertaPraMim.Infrastructure/Migrations/20260304194940_AddGoogleCalendarSyncRetryDeadLetterObservability.cs`

## Observacoes

- Persistencia em UTC mantida; exibicao em fuso de negocio deve ocorrer na camada de apresentacao (`America/Sao_Paulo`).
- Fluxo legado de sync no `ServiceAppointmentService` foi mantido como fallback para cenarios de teste que ainda nao injetam o novo servico de operacoes.

