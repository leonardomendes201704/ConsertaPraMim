# ST-008 - UI Admin Gestao de usuarios

Status: Backlog  
Epic: EPIC-001

## Objetivo

Entregar tela administrativa para operacao de usuarios com filtros e acoes de status.

## Criterios de aceite

- Listagem de usuarios com paginacao e filtros.
- Detalhe de usuario com dados principais e historico resumido.
- Acao de ativar/desativar usuario com confirmacao.
- Feedback visual de sucesso/erro.

## Tasks

- [ ] Criar `AdminUsersController` (web admin) com actions Index e Details.
- [ ] Integrar com endpoints `GET /api/admin/users` e `GET /api/admin/users/{id}`.
- [ ] Criar filtros por role, status e busca.
- [ ] Implementar acao de ativar/desativar usuario.
- [ ] Exibir confirmacao antes de acao sensivel.
- [ ] Atualizar grid apos alteracao sem refresh completo.

