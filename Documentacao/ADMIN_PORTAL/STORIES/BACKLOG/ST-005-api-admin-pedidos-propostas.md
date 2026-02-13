# ST-005 - API Admin Gestao de pedidos e propostas

Status: Backlog  
Epic: EPIC-001

## Objetivo

Dar ao admin visao e controle operacional sobre pedidos e propostas.

## Criterios de aceite

- Listagem de pedidos com filtros por status, categoria e periodo.
- Detalhe do pedido com cliente, propostas e historico de status.
- Acao administrativa para alterar status de pedido em casos excepcionais.
- Listagem de propostas com filtro por pedido, prestador e status.

## Tasks

- [ ] Criar DTOs admin para pedido/proposta com visao consolidada.
- [ ] Criar endpoint `GET /api/admin/service-requests`.
- [ ] Criar endpoint `GET /api/admin/service-requests/{id}`.
- [ ] Criar endpoint `PUT /api/admin/service-requests/{id}/status`.
- [ ] Criar endpoint `GET /api/admin/proposals`.
- [ ] Criar endpoint `PUT /api/admin/proposals/{id}/invalidate` (quando aplicavel).
- [ ] Registrar todas as acoes sensiveis em auditoria.
- [ ] Criar testes de integracao para fluxo feliz e validacoes.

