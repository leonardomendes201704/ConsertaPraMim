# ST-022 - API de atendimento para portal admin

Status: Done  
Epic: EPIC-008

## Objetivo

Como admin, quero operar uma fila de chamados com status e atribuicao para atender prestadores com rastreabilidade.

## Criterios de aceite

- Admin consegue listar fila de chamados com filtros por status, prioridade e atribuicao.
- Admin consegue visualizar detalhe do chamado com historico completo.
- Admin consegue responder chamado.
- Admin consegue alterar status e atribuir responsavel administrativo.
- Todas as rotas protegidas por autorizacao Admin.

## Tasks

- [x] Criar endpoint `GET /api/admin/support/tickets` com filtros/paginacao/ordenacao.
- [x] Criar endpoint `GET /api/admin/support/tickets/{ticketId}`.
- [x] Criar endpoint `POST /api/admin/support/tickets/{ticketId}/messages`.
- [x] Criar endpoint `PATCH /api/admin/support/tickets/{ticketId}/status`.
- [x] Criar endpoint `PATCH /api/admin/support/tickets/{ticketId}/assign`.
- [x] Aplicar regras de transicao de status e validar combinacoes invalidas.
- [x] Registrar eventos de auditoria (alteracao de status, atribuicao e fechamento).
- [x] Criar testes de integracao cobrindo fluxo operacional admin.
- [x] Criar/atualizar diagrama de fluxo Mermaid da funcionalidade.
- [x] Criar/atualizar diagrama de sequencia Mermaid da funcionalidade.

## Entregaveis tecnicos

- Controller API admin:
  - `ConsertaPraMim.API/Controllers/AdminSupportTicketsController.cs`
- Service e contratos:
  - `ConsertaPraMim.Application/Interfaces/IAdminSupportTicketService.cs`
  - `ConsertaPraMim.Application/Services/AdminSupportTicketService.cs`
  - `ConsertaPraMim.Application/DTOs/AdminSupportTicketDTOs.cs`
- Repositorio de suporte atualizado com consultas admin:
  - `ConsertaPraMim.Domain/Repositories/ISupportTicketRepository.cs`
  - `ConsertaPraMim.Infrastructure/Repositories/SupportTicketRepository.cs`
- Validadores de payload admin:
  - `ConsertaPraMim.Application/Validators/SupportTicketValidators.cs`

## Testes

- `AdminSupportTicketServiceInMemoryIntegrationTests`:
  - fila com indicadores;
  - resposta admin com alteracao de status;
  - atribuicao com trilha de auditoria;
  - bloqueio de transicao invalida.
- `AdminSupportTicketsControllerInMemoryIntegrationTests`:
  - fluxo operacional ponta a ponta (listar, atribuir, responder, fechar);
  - `404` para ticket inexistente.

## Diagramas Mermaid

- Fluxo: `Documentacao/DIAGRAMAS/ADMIN_PORTAL/ST-022-api-atendimento-portal-admin/fluxo-api-atendimento-portal-admin.mmd`
- Sequencia: `Documentacao/DIAGRAMAS/ADMIN_PORTAL/ST-022-api-atendimento-portal-admin/sequencia-api-atendimento-portal-admin.mmd`
