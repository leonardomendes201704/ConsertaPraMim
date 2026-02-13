# ST-005 - API Admin Gestao de pedidos e propostas

Status: Done  
Epic: EPIC-001

## Objetivo

Dar ao admin visao e controle operacional sobre pedidos e propostas.

## Criterios de aceite

- Listagem de pedidos com filtros por status, categoria e periodo.
- Detalhe do pedido com cliente, propostas e historico de status.
- Acao administrativa para alterar status de pedido em casos excepcionais.
- Listagem de propostas com filtro por pedido, prestador e status.

## Tasks

- [x] Criar DTOs admin para pedido/proposta com visao consolidada.
- [x] Criar endpoint `GET /api/admin/service-requests`.
- [x] Criar endpoint `GET /api/admin/service-requests/{id}`.
- [x] Criar endpoint `PUT /api/admin/service-requests/{id}/status`.
- [x] Criar endpoint `GET /api/admin/proposals`.
- [x] Criar endpoint `PUT /api/admin/proposals/{id}/invalidate` (quando aplicavel).
- [x] Registrar todas as acoes sensiveis em auditoria.
- [x] Criar testes de integracao para fluxo feliz e validacoes.
