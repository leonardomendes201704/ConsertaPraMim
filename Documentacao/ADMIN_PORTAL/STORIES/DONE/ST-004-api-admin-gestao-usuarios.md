# ST-004 - API Admin Gestao de usuarios

Status: Done  
Epic: EPIC-001

## Objetivo

Permitir ao admin listar, filtrar, bloquear/desbloquear e consultar detalhes de usuarios.

## Criterios de aceite

- Listagem de usuarios com filtros por role, status e busca textual.
- Endpoint de detalhe por usuario.
- Acao de ativar/desativar usuario com auditoria.
- Endpoint protegido por autorizacao Admin.

## Tasks

- [x] Criar DTOs de listagem/detalhe de usuario para admin.
- [x] Criar endpoint `GET /api/admin/users` com paginacao e filtros.
- [x] Criar endpoint `GET /api/admin/users/{id}`.
- [x] Criar endpoint `PUT /api/admin/users/{id}/status` (ativo/inativo).
- [x] Validar regras de negocio (ex.: evitar auto-bloqueio do unico admin ativo).
- [x] Registrar acao administrativa em auditoria.
- [x] Criar testes de integracao para filtros e mudanca de status.
