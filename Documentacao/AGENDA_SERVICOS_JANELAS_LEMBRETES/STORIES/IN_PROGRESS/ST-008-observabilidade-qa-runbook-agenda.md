# ST-008 - Observabilidade, QA, runbook e operacao assistida do fluxo

Status: In Progress  
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
- [ ] Adicionar logs estruturados e correlation id no fluxo.
- [x] Criar painel/consulta administrativa para diagnostico de agendamentos.
- [ ] Escrever plano de testes E2E para cliente e prestador.
- [ ] Criar runbook de suporte e rollback do modulo.
- [ ] Atualizar manual administrativo/QA com novos fluxos.
- [ ] Definir checklist de prontidao para rollout gradual.

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

## Diagramas

- Fluxo: `Documentacao/DIAGRAMAS/AGENDA_SERVICOS_JANELAS_LEMBRETES/ST-008-observabilidade-qa-runbook-agenda/fluxo-kpis-operacionais-agenda.mmd`
- Sequencia: `Documentacao/DIAGRAMAS/AGENDA_SERVICOS_JANELAS_LEMBRETES/ST-008-observabilidade-qa-runbook-agenda/sequencia-kpis-operacionais-agenda.mmd`
