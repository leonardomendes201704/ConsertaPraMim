# RUNBOOK - Incidentes e recuperacao da sincronizacao Google Calendar (ST-013)

## 1. Objetivo

Padronizar resposta operacional para incidentes da sincronizacao de agendamentos com Google Calendar, cobrindo triagem, mitigacao, reprocessamento e rollback.

## 2. Escopo

- Integracao `IGoogleCalendarService`
- Worker `GoogleCalendarSyncRetryWorker`
- Endpoints admin:
  - `GET /api/admin/google-calendar-sync/overview`
  - `POST /api/admin/google-calendar-sync/reprocess`
- Tabela `ServiceAppointmentCalendarSyncs`

## 3. Severidade

- `P0`: backlog crescendo rapidamente + perda de sincronizacao em massa.
- `P1`: aumento sustentado de `DeadLetter` ou falha total de retry.
- `P2`: falhas pontuais, com recuperacao automatica por retry.

## 4. Triagem inicial (primeiros 10 minutos)

1. Consultar `overview` e confirmar:
   - `FailedRetryableCount`
   - `DeadLetterCount`
   - `RetryQueueCount`
   - `AverageLatencyMs` e `P95LatencyMs`
2. Verificar logs estruturados por:
   - `appointmentId`
   - `googleEventId`
   - `providerId`
   - `traceId`
3. Validar configuracao runtime:
   - `GoogleCalendarSync:Enabled`
   - `GoogleCalendarSync:RetryEnabled`
   - `GoogleCalendarSync:RetryWorkerEnabled`
4. Classificar erro dominante:
   - autenticacao/permissao
   - indisponibilidade/timeout
   - payload invalido/regra.

## 5. Mitigacao imediata

## 5.1 Dependencia Google instavel

- Manter `RetryEnabled=true`.
- Aumentar temporariamente:
  - `RetryMaxDelaySeconds`
  - `RetryWorkerBatchSize` (com cuidado para nao saturar API externa).

## 5.2 Credencial invalida/expirada

1. Corrigir credencial da service account.
2. Confirmar health funcional com um agendamento de teste.
3. Reprocessar dead-letter por lote usando `POST /reprocess`.

## 5.3 Falha de payload/regra

1. Corrigir causa de negocio/codigo.
2. Validar no ambiente alvo.
3. Reprocessar somente recorte afetado (`appointmentId` ou intervalo curto).

## 6. Procedimento de reprocessamento

Exemplo por `appointmentId`:

```http
POST /api/admin/google-calendar-sync/reprocess
Content-Type: application/json

{
  "appointmentId": "GUID",
  "maxItems": 1,
  "forceResetRetry": true
}
```

Exemplo por intervalo:

```http
POST /api/admin/google-calendar-sync/reprocess
Content-Type: application/json

{
  "fromUtc": "2026-03-04T00:00:00Z",
  "toUtc": "2026-03-04T23:59:59Z",
  "statuses": [3, 5],
  "maxItems": 500,
  "forceResetRetry": true
}
```

## 7. Rollback rapido

Se houver impacto alto:

1. Desabilitar sincronizacao:
   - `GoogleCalendarSync:Enabled=false`
2. Manter API operacional para fluxo principal de agendamento.
3. Registrar incidente e iniciar plano de correcao.
4. Reativar somente apos smoke test completo.

## 8. Criterio de encerramento

- `DeadLetterCount` estabilizado ou em queda.
- `RetryQueueCount` sob controle.
- Sem novos erros criticos por janela de 30 minutos.
- Reprocessamento concluido para itens prioritarios.

## 9. Pos-incidente

Checklist obrigatorio:

1. Causa raiz documentada.
2. Lista de `appointmentId` impactados.
3. Evidencia de reprocessamento executado.
4. Acao preventiva registrada (configuracao, codigo ou monitoramento).
