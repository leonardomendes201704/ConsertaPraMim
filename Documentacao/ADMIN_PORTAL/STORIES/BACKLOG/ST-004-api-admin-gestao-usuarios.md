# ST-004 - API Admin Gestao de usuarios

Status: Backlog  
Epic: EPIC-001

## Objetivo

Permitir ao admin listar, filtrar, bloquear/desbloquear e consultar detalhes de usuarios.

## Criterios de aceite

- Listagem de usuarios com filtros por role, status e busca textual.
- Endpoint de detalhe por usuario.
- Acao de ativar/desativar usuario com auditoria.
- Endpoint protegido por autorizacao Admin.

## Tasks

- [ ] Criar DTOs de listagem/detalhe de usuario para admin.
- [ ] Criar endpoint `GET /api/admin/users` com paginacao e filtros.
- [ ] Criar endpoint `GET /api/admin/users/{id}`.
- [ ] Criar endpoint `PUT /api/admin/users/{id}/status` (ativo/inativo).
- [ ] Validar regras de negocio (ex.: evitar auto-bloqueio do unico admin ativo).
- [ ] Registrar acao administrativa em auditoria.
- [ ] Criar testes de integracao para filtros e mudanca de status.

