# EPIC-003 - Sincronizacao de agendamentos com Google Calendar

## Objetivo

Sincronizar automaticamente os agendamentos confirmados do ConsertaPraMim com um calendario unico do Google Calendar, mantendo ciclo completo de criacao, atualizacao e cancelamento.

## Problema atual

- O agendamento confirmado no sistema nao gera evento no Google Calendar.
- Operacao perde visibilidade consolidada em calendario externo.
- Cancelamentos no sistema nao removem eventos externos automaticamente.
- Nao existe trilha de sincronizacao para auditoria e reprocessamento.

## Resultado esperado

- Todo agendamento confirmado gera evento no Google Calendar automaticamente.
- Cada evento inclui contexto operacional (`requestId`, `appointmentId`, `providerId`).
- Cancelamento no sistema remove o evento correspondente do Google Calendar.
- Falhas de sincronizacao ficam auditaveis e reprocessaveis com seguranca.

## Metricas de sucesso

- >= 99% dos agendamentos confirmados sincronizados em ate 60s.
- >= 99% dos cancelamentos sincronizados em ate 60s.
- 0 duplicacao de evento para o mesmo `appointmentId`.
- 100% dos erros de sincronizacao com log estruturado e rastreabilidade.

## Escopo

### Inclui

- Integracao .NET com Google Calendar API via Service Account.
- Calendario unico compartilhado para operacao.
- Sincronizacao automatica create/update/delete por eventos de agendamento.
- Persistencia de vinculo local `appointmentId <-> googleEventId`.
- Observabilidade, retries, dead-letter e runbook operacional.

### Nao inclui

- Multi-calendario por prestador nesta fase.
- OAuth individual por prestador nesta fase.
- Sincronizacao bidirecional (Google -> sistema) nesta fase.

## Historias vinculadas

- ST-011 - Fundacao da integracao Google Calendar (Service Account e calendario unico).
- ST-012 - Sincronizacao automatica de agendamento (create/update/delete) com idempotencia.
- ST-013 - Observabilidade, reprocessamento, QA e rollout da sincronizacao Google Calendar.

