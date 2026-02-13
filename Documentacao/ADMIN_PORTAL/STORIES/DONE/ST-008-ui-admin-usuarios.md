# ST-008 - UI Admin Gestao de usuarios

Status: Done  
Epic: EPIC-001

## Objetivo

Entregar tela administrativa para operacao de usuarios com filtros e acoes de status.

## Criterios de aceite

- Listagem de usuarios com paginacao e filtros.
- Detalhe de usuario com dados principais e historico resumido.
- Acao de ativar/desativar usuario com confirmacao.
- Feedback visual de sucesso/erro.

## Tasks

- [x] Criar `AdminUsersController` (web admin) com actions Index e Details.
- [x] Integrar com endpoints `GET /api/admin/users` e `GET /api/admin/users/{id}`.
- [x] Criar filtros por role, status e busca.
- [x] Implementar acao de ativar/desativar usuario.
- [x] Exibir confirmacao antes de acao sensivel.
- [x] Atualizar grid apos alteracao sem refresh completo.

