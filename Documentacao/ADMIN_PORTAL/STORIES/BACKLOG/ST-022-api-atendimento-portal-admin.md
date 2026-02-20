# ST-022 - API de atendimento para portal admin

Status: Backlog  
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

- [ ] Criar endpoint `GET /api/admin/support/tickets` com filtros/paginacao/ordenacao.
- [ ] Criar endpoint `GET /api/admin/support/tickets/{ticketId}`.
- [ ] Criar endpoint `POST /api/admin/support/tickets/{ticketId}/messages`.
- [ ] Criar endpoint `PATCH /api/admin/support/tickets/{ticketId}/status`.
- [ ] Criar endpoint `PATCH /api/admin/support/tickets/{ticketId}/assign`.
- [ ] Aplicar regras de transicao de status e validar combinacoes invalidas.
- [ ] Registrar eventos de auditoria (alteracao de status, atribuicao e fechamento).
- [ ] Criar testes de integracao cobrindo fluxo operacional admin.
- [ ] Criar/atualizar diagrama de fluxo Mermaid da funcionalidade.
- [ ] Criar/atualizar diagrama de sequencia Mermaid da funcionalidade.
