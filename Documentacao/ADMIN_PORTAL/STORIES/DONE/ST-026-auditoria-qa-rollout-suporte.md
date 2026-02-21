# ST-026 - Auditoria, QA e rollout do modulo de suporte

Status: Done  
Epic: EPIC-008

## Objetivo

Como time de plataforma, quero concluir com qualidade e governanca o modulo de suporte para publicar com risco controlado.

## Criterios de aceite

- Trilha de auditoria cobre as operacoes administrativas sensiveis do modulo.
- Existe cobertura de testes para fluxos principais e permissoes.
- Runbook de deploy/rollback do modulo documentado.
- Changelog e documentacao operacional atualizados.
- Go-live realizado sem regressao critica nos portais.

## Tasks

- [x] Revisar e completar auditoria de acoes sensiveis (atribuir/status/fechar/reabrir).
- [x] Criar suite de testes E2E do fluxo ponta a ponta prestador <-> admin.
- [x] Executar bateria de regressao focada em autorizacao e isolamento de dados.
- [x] Criar runbook de deploy/rollback especifico do modulo de suporte.
- [x] Atualizar `CHANGELOG/CHANGELOG.md` e documentacao funcional.
- [x] Definir checklist de monitoramento pos-deploy (erros, tempo de resposta, fila).
- [x] Criar/atualizar diagrama de fluxo Mermaid da funcionalidade.
- [x] Criar/atualizar diagrama de sequencia Mermaid da funcionalidade.

## Entregaveis tecnicos

- Auditoria e regra de reabertura controlada:
  - `ConsertaPraMim.Application/Services/AdminSupportTicketService.cs`
- Suite E2E/integracao de suporte:
  - `tests/ConsertaPraMim.Tests.Unit/Integration/E2E/SupportTicketsProviderAdminE2EInMemoryIntegrationTests.cs`
  - `tests/ConsertaPraMim.Tests.Unit/Integration/Services/AdminSupportTicketServiceInMemoryIntegrationTests.cs`
- Runbook e checklist operacional:
  - `Documentacao/ADMIN_PORTAL/RUNBOOKS/DEPLOY_ROLLBACK_ST-026_SUPORTE.md`
  - `Documentacao/ADMIN_PORTAL/RUNBOOKS/CHECKLIST_MONITORAMENTO_ST-026_SUPORTE.md`
- Atualizacoes de governanca:
  - `Documentacao/ADMIN_PORTAL/CHANGELOG/CHANGELOG.md`
  - `Documentacao/ADMIN_PORTAL/EPICS/EPIC-008-central-atendimento-prestador-admin.md`

## Testes

- Fluxo E2E prestador <-> admin cobrindo:
  - abertura de chamado;
  - atribuicao de responsavel;
  - resposta administrativa;
  - fechamento e reabertura;
  - encerramento final pelo prestador.
- Regressao de isolamento:
  - bloqueio de acesso de prestador estrangeiro em detalhe/resposta/fechamento.
- Regressao de permissao:
  - bloqueio de atribuicao para usuario nao-admin.
- Regressao de auditoria:
  - registro de `support_ticket_assignment_changed`, `support_ticket_status_changed`, `support_ticket_closed` e `support_ticket_reopened`.

## Diagramas Mermaid

- Fluxo: `Documentacao/DIAGRAMAS/ADMIN_PORTAL/ST-026-auditoria-qa-rollout-suporte/fluxo-auditoria-qa-rollout-suporte.mmd`
- Sequencia: `Documentacao/DIAGRAMAS/ADMIN_PORTAL/ST-026-auditoria-qa-rollout-suporte/sequencia-auditoria-qa-rollout-suporte.mmd`
