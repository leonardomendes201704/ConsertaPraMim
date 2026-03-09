# INDEX - Agenda de Servicos

## Epics ativos

- `EPIC-001` - Agenda de servicos com janelas de horario, confirmacao, reagendamento e lembretes automaticos.
- `EPIC-002` - Cancelamento de pedido em cascata com politica de 48h e notificacao multi-prestador.

## Stories

### In Progress

- Sem historias em progresso nesta trilha.

### Backlog

- Sem historias pendentes no backlog desta trilha no momento.

### Done

- `ST-001` - Modelagem de agenda, disponibilidade e janelas de horario.
- `ST-002` - API de consulta de slots e criacao de agendamentos.
- `ST-003` - Confirmacao do prestador, recusa e expiracao automatica.
- `ST-004` - Reagendamento e cancelamento com politicas de prazo.
- `ST-005` - Lembretes automaticos, retries e rastreabilidade de envio.
- `ST-006` - UI do cliente para agendar, acompanhar e reagendar servicos.
- `ST-007` - UI do prestador para gerir agenda e responder solicitacoes.
- `ST-008` - Observabilidade, QA, runbook e operacao assistida do fluxo.
- `ST-009` - Cancelamento de pedido em cascata com politica de 48h.

## Novos artefatos da trilha

- `EPICS/EPIC-002-cancelamento-pedido-cascata-multi-prestador.md` - objetivo, guardrails e entregaveis do cancelamento de pedido.
- `STORIES/DONE/ST-009-cancelamento-pedido-cascata-48h.md` - story concluida do cancelamento E2E.
- `../DIAGRAMAS/AGENDA_SERVICOS_JANELAS_LEMBRETES/ST-008-observabilidade-qa-runbook-agenda/sequencia-consulta-publica-slots-15-dias.mmd` - sequencia tecnica da consulta anonima de disponibilidade agregada dos prestadores.

## Artefatos de apoio

- `PLANO_TESTES_E2E_ST-008.md` - plano de validacao ponta a ponta para cliente, prestador e admin.
- `RUNBOOK_SUPORTE_ROLLBACK_ST-008.md` - procedimentos operacionais para incidente e rollback controlado da agenda.
- `MANUAL_ADMIN_QA_AGENDA_ST-008.md` - manual operacional para validacao e auditoria admin/qa.
- `CHECKLIST_ROLLOUT_GRADUAL_ST-008.md` - gates de prontidao para rollout gradual da agenda.
