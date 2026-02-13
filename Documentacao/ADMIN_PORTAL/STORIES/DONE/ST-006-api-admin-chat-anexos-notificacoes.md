# ST-006 - API Admin Conversas, anexos e notificacoes

Status: Done  
Epic: EPIC-001

## Objetivo

Permitir monitoramento administrativo de conversas e suporte operacional de notificacoes.

## Criterios de aceite

- Admin consegue listar conversas por pedido/prestador/cliente.
- Admin consegue visualizar anexos enviados no chat.
- Admin consegue disparar notificacao manual para um usuario.
- Todas as rotas protegidas por autorizacao Admin.

## Tasks

- [x] Criar endpoint `GET /api/admin/chats` com filtros e paginacao.
- [x] Criar endpoint `GET /api/admin/chats/{requestId}/{providerId}` para historico completo.
- [x] Criar endpoint `GET /api/admin/chat-attachments` com busca por pedido/usuario.
- [x] Criar endpoint `POST /api/admin/notifications/send`.
- [x] Aplicar mascaramento de dados sensiveis quando necessario.
- [x] Registrar envio de notificacao em auditoria.
- [x] Criar testes de integracao dos endpoints de chat e notificacao admin.

