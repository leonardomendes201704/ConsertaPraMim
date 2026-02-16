# ST-008 - Observabilidade, QA, runbook e operacao assistida do fluxo

Status: Done  
Epic: EPIC-001

## Objetivo

Garantir governanca operacional do modulo de agenda com metricas, alertas, roteiro de testes e procedimentos de suporte.

## Criterios de aceite

- Dashboard operacional exibe metricas chave da agenda.
- Logs de eventos de agenda possuem correlacao por `AppointmentId`.
- Existe runbook para incidentes de expirar sem notificar e falha de lembrete.
- Suite de testes cobre cenarios criticos ponta a ponta.
- Manual funcional para QA e operacao esta atualizado.

## Tasks

- [x] Definir metricas operacionais:
- [x] taxa de confirmacao no SLA.
- [x] volume de reagendamento.
- [x] taxa de cancelamento.
- [x] taxa de falha de lembretes.
- [x] Adicionar logs estruturados e correlation id no fluxo.
- [x] Criar painel/consulta administrativa para diagnostico de agendamentos.
- [x] Escrever plano de testes E2E para cliente e prestador.
- [x] Criar runbook de suporte e rollback do modulo.
- [x] Atualizar manual administrativo/QA com novos fluxos.
- [x] Definir checklist de prontidao para rollout gradual.

## Entregas parciais

- API/Admin Dashboard passou a expor KPIs operacionais de agenda:
  - `AppointmentConfirmationInSlaRatePercent`
  - `AppointmentRescheduleRatePercent`
  - `AppointmentCancellationRatePercent`
  - `ReminderFailureRatePercent`
  - `ReminderAttemptsInPeriod`
  - `ReminderFailuresInPeriod`
- Portal Admin (`AdminHome`) passou a renderizar card de `Operacao da Agenda` com os indicadores acima.
- Cobertura automatizada adicionada em `AdminDashboardServiceTests.GetDashboardAsync_ShouldComputeAgendaOperationalAndReminderKpis`.
- API passou a propagar `X-Correlation-ID` por middleware global e a incluir logs estruturados no `ServiceAppointmentsController` para operacoes criticas da agenda.
- Testes E2E adicionados para validar eco de correlation id informado e geracao automatica quando ausente.
- Plano de testes E2E de agenda criado em `Documentacao/AGENDA_SERVICOS_JANELAS_LEMBRETES/PLANO_TESTES_E2E_ST-008.md`.
- Runbook de suporte e rollback criado em `Documentacao/AGENDA_SERVICOS_JANELAS_LEMBRETES/RUNBOOK_SUPORTE_ROLLBACK_ST-008.md`.
- Manual Admin/QA atualizado em `Documentacao/AGENDA_SERVICOS_JANELAS_LEMBRETES/MANUAL_ADMIN_QA_AGENDA_ST-008.md`.
- Checklist de rollout gradual publicado em `Documentacao/AGENDA_SERVICOS_JANELAS_LEMBRETES/CHECKLIST_ROLLOUT_GRADUAL_ST-008.md`.

## Diagramas

- Fluxo: `Documentacao/DIAGRAMAS/AGENDA_SERVICOS_JANELAS_LEMBRETES/ST-008-observabilidade-qa-runbook-agenda/fluxo-kpis-operacionais-agenda.mmd`
- Sequencia: `Documentacao/DIAGRAMAS/AGENDA_SERVICOS_JANELAS_LEMBRETES/ST-008-observabilidade-qa-runbook-agenda/sequencia-kpis-operacionais-agenda.mmd`
- Fluxo (correlation/logs): `Documentacao/DIAGRAMAS/AGENDA_SERVICOS_JANELAS_LEMBRETES/ST-008-observabilidade-qa-runbook-agenda/fluxo-correlation-logs-agenda.mmd`
- Sequencia (correlation/logs): `Documentacao/DIAGRAMAS/AGENDA_SERVICOS_JANELAS_LEMBRETES/ST-008-observabilidade-qa-runbook-agenda/sequencia-correlation-logs-agenda.mmd`
- Fluxo (plano E2E): `Documentacao/DIAGRAMAS/AGENDA_SERVICOS_JANELAS_LEMBRETES/ST-008-observabilidade-qa-runbook-agenda/fluxo-plano-testes-e2e-agenda.mmd`
- Sequencia (plano E2E): `Documentacao/DIAGRAMAS/AGENDA_SERVICOS_JANELAS_LEMBRETES/ST-008-observabilidade-qa-runbook-agenda/sequencia-plano-testes-e2e-agenda.mmd`
- Fluxo (runbook suporte/rollback): `Documentacao/DIAGRAMAS/AGENDA_SERVICOS_JANELAS_LEMBRETES/ST-008-observabilidade-qa-runbook-agenda/fluxo-runbook-suporte-rollback-agenda.mmd`
- Sequencia (runbook suporte/rollback): `Documentacao/DIAGRAMAS/AGENDA_SERVICOS_JANELAS_LEMBRETES/ST-008-observabilidade-qa-runbook-agenda/sequencia-runbook-suporte-rollback-agenda.mmd`
- Fluxo (manual admin/qa): `Documentacao/DIAGRAMAS/AGENDA_SERVICOS_JANELAS_LEMBRETES/ST-008-observabilidade-qa-runbook-agenda/fluxo-manual-admin-qa-agenda.mmd`
- Sequencia (rollout gradual): `Documentacao/DIAGRAMAS/AGENDA_SERVICOS_JANELAS_LEMBRETES/ST-008-observabilidade-qa-runbook-agenda/sequencia-rollout-gradual-agenda.mmd`
