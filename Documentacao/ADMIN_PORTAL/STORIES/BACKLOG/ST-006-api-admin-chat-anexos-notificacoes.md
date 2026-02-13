# ST-006 - API Admin Conversas, anexos e notificacoes

Status: Backlog  
Epic: EPIC-001

## Objetivo

Permitir monitoramento administrativo de conversas e suporte operacional de notificacoes.

## Criterios de aceite

- Admin consegue listar conversas por pedido/prestador/cliente.
- Admin consegue visualizar anexos enviados no chat.
- Admin consegue disparar notificacao manual para um usuario.
- Todas as rotas protegidas por autorizacao Admin.

## Tasks

- [ ] Criar endpoint `GET /api/admin/chats` com filtros e paginacao.
- [ ] Criar endpoint `GET /api/admin/chats/{requestId}/{providerId}` para historico completo.
- [ ] Criar endpoint `GET /api/admin/chat-attachments` com busca por pedido/usuario.
- [ ] Criar endpoint `POST /api/admin/notifications/send`.
- [ ] Aplicar mascaramento de dados sensiveis quando necessario.
- [ ] Registrar envio de notificacao em auditoria.
- [ ] Criar testes de integracao dos endpoints de chat e notificacao admin.

