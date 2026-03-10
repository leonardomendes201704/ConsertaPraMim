# Manual QA/Operacao - Observabilidade e Reprocessamento Google Calendar Sync (ST-013)

## 1. Objetivo

Padronizar validacao funcional e operacional da trilha de observabilidade, retry, dead-letter e reprocessamento manual da sincronizacao de agendamentos com Google Calendar.

## 2. Escopo

- API `ConsertaPraMim.API`:
  - `GET /api/admin/google-calendar-sync/overview`
  - `POST /api/admin/google-calendar-sync/reprocess`
- Worker:
  - `GoogleCalendarSyncRetryWorker`
- Dominio de sync:
  - `ServiceAppointmentCalendarSyncs`

## 3. Pre-requisitos

1. Integracao Google Calendar ativa e valida em runtime:
   - `GoogleCalendarSync:Enabled=true`
   - `GoogleCalendarSync:ProjectId`
   - `GoogleCalendarSync:ServiceAccountEmail`
   - `GoogleCalendarSync:PrivateKey`
   - `GoogleCalendarSync:CalendarId`
2. Credencial admin valida para chamadas em `/api/admin/google-calendar-sync/*`.
3. Base com ao menos um agendamento confirmado e registro em `ServiceAppointmentCalendarSyncs`.

## 4. Configuracao de retry

Chaves usadas na API:

- `GoogleCalendarSync:RetryEnabled`
- `GoogleCalendarSync:RetryMaxAttempts`
- `GoogleCalendarSync:RetryBaseDelaySeconds`
- `GoogleCalendarSync:RetryMaxDelaySeconds`
- `GoogleCalendarSync:RetryJitterMaxSeconds`
- `GoogleCalendarSync:RetryWorkerEnabled`
- `GoogleCalendarSync:RetryWorkerIntervalSeconds`
- `GoogleCalendarSync:RetryWorkerBatchSize`

## 5. Matriz de cenarios QA

## 5.1 Happy path (sync ok)

1. Criar/agendar visita no fluxo normal.
2. Verificar em `ServiceAppointmentCalendarSyncs`:
   - `SyncStatus = Synced` (ou `Deleted` em cancelamento)
   - `LastLatencyMs` preenchido
   - `LastErrorCode` nulo
3. Verificar `overview`:
   - incremento em `SyncedCount` ou `DeletedCount`
   - `AverageLatencyMs` > 0.

## 5.2 Falha de autenticacao Google (erro permanente)

1. Invalidar credencial (ex.: private key invalida) em ambiente de teste.
2. Disparar sincronizacao.
3. Validar:
   - `SyncStatus = DeadLetter`
   - `LastErrorCode` preenchido (`google_calendar_*`)
   - `DeadLetterAtUtc` preenchido
   - sem `NextRetryAtUtc`.
4. Corrigir credencial e executar `POST /reprocess` para o `appointmentId`.
5. Validar retorno de sucesso e transicao para `Synced/Deleted`.

## 5.3 Falha transiente/timeout

1. Simular indisponibilidade temporaria da API Google.
2. Disparar sincronizacao.
3. Validar:
   - `SyncStatus = Failed`
   - `RetryCount` incrementado
   - `NextRetryAtUtc` preenchido com backoff + jitter.
4. Aguardar execucao do worker.
5. Quando dependencia estabilizar, validar transicao para `Synced/Deleted`.

## 5.4 Cancelamento de agendamento

1. Cancelar agendamento com sync previo no Google.
2. Validar:
   - operacao registrada como `Delete`
   - `SyncStatus = Deleted` quando delete ok
   - em erro de delete: `Failed` ou `DeadLetter` conforme politica.

## 5.5 Reprocessamento por intervalo

1. Chamar `POST /api/admin/google-calendar-sync/reprocess` com `fromUtc` e `toUtc`.
2. Validar:
   - `ProcessedCount` > 0 quando houver itens.
   - `Items[]` com status final por item.
   - logs com `traceId`, `appointmentId`, `googleEventId`, `providerId`.

## 6. Consultas SQL uteis

```sql
SELECT TOP 100
    AppointmentId,
    SyncStatus,
    LastOperation,
    RetryCount,
    MaxRetryAttempts,
    NextRetryAtUtc,
    DeadLetterAtUtc,
    LastLatencyMs,
    LastErrorCode,
    Error,
    LastSyncAtUtc
FROM ServiceAppointmentCalendarSyncs
ORDER BY LastSyncAtUtc DESC, CreatedAt DESC;
```

```sql
SELECT
    SyncStatus,
    COUNT(*) AS Total
FROM ServiceAppointmentCalendarSyncs
GROUP BY SyncStatus
ORDER BY SyncStatus;
```

## 7. Criterios de aceite para release

- Worker ativo processando fila de retry sem erro recorrente.
- `overview` retornando dados coerentes com a base.
- Reprocessamento manual por `appointmentId` funcionando ponta a ponta.
- Falhas permanentes indo para `DeadLetter`.
- Nao existe transicao para `Synced/Deleted` quando API Google retorna erro.

## 8. Troubleshooting rapido

- `overview` vazio:
  - confirmar dados em `ServiceAppointmentCalendarSyncs`.
- retry nao processa:
  - validar `GoogleCalendarSync:RetryWorkerEnabled=true`.
  - validar `NextRetryAtUtc <= UTCNOW`.
- itens presos em dead-letter:
  - corrigir causa raiz (credencial/payload) antes de reprocessar.
