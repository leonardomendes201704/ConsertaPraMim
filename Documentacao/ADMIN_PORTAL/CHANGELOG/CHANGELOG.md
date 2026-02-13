# Admin Portal Changelog

## Como usar

1. Concluiu uma story: mover para `STORIES/DONE/`.
2. Adicionar uma nova entrada em `Unreleased`.
3. Em release, mover blocos de `Unreleased` para uma secao versionada.

## Unreleased

- [2026-02-13] [ST-001] Hardening inicial de seguranca para Admin
- Tipo: feat
- Resumo: bloqueio de auto-cadastro com role Admin, policy `AdminOnly` criada e seed de admin controlado por ambiente/config.
- Arquivos principais: `ConsertaPraMim.Application/Services/AuthService.cs`, `ConsertaPraMim.API/Controllers/AuthController.cs`, `ConsertaPraMim.Infrastructure/Data/DbInitializer.cs`, `ConsertaPraMim.Web.Provider/Controllers/AdminController.cs`
- Risco/Impacto: medio
- [2026-02-13] [ST-002] Bootstrap do novo portal web admin
- Tipo: feat
- Resumo: criado projeto `ConsertaPraMim.Web.Admin` com cookie auth, policy `AdminOnly`, login admin e dashboard inicial.
- Arquivos principais: `ConsertaPraMim.Web.Admin/Program.cs`, `ConsertaPraMim.Web.Admin/Controllers/AccountController.cs`, `ConsertaPraMim.Web.Admin/Controllers/AdminHomeController.cs`, `ConsertaPraMim.Web.Admin/Views/Shared/_Layout.cshtml`
- Risco/Impacto: medio
- [2026-02-13] [ST-003] API de dashboard administrativo com filtros e eventos paginados
- Tipo: feat
- Resumo: criado endpoint `GET /api/admin/dashboard` protegido por `AdminOnly`, com agregados de usuarios/pedidos/propostas/chat e eventos recentes paginados com filtros.
- Arquivos principais: `ConsertaPraMim.API/Controllers/AdminDashboardController.cs`, `ConsertaPraMim.Application/Services/AdminDashboardService.cs`, `ConsertaPraMim.Application/DTOs/AdminDashboardDTOs.cs`, `ConsertaPraMim.Infrastructure/Repositories/ProposalRepository.cs`
- Risco/Impacto: medio
- [2026-02-13] [ST-004] API Admin para gestao de usuarios com auditoria
- Tipo: feat
- Resumo: adicionados endpoints admin para listar/filtrar usuarios, detalhe por id e alteracao de status com regras de seguranca (ultimo admin e auto-bloqueio) e registro de auditoria.
- Arquivos principais: `ConsertaPraMim.API/Controllers/AdminUsersController.cs`, `ConsertaPraMim.Application/Services/AdminUserService.cs`, `ConsertaPraMim.Domain/Entities/AdminAuditLog.cs`, `ConsertaPraMim.Infrastructure/Migrations/20260213021345_AddAdminAuditLogs.cs`
- Risco/Impacto: medio
- [2026-02-13] [ST-005] API Admin para gestao de pedidos e propostas
- Tipo: feat
- Resumo: criados endpoints admin para listagem/detalhe de pedidos, alteracao administrativa de status, listagem de propostas e invalidacao com auditoria e regras de seguranca.
- Arquivos principais: `ConsertaPraMim.API/Controllers/AdminServiceRequestsController.cs`, `ConsertaPraMim.API/Controllers/AdminProposalsController.cs`, `ConsertaPraMim.Application/Services/AdminRequestProposalService.cs`, `ConsertaPraMim.Infrastructure/Migrations/20260213110423_AddProposalInvalidation.cs`
- Risco/Impacto: medio
- [2026-02-13] [ST-006] API Admin para monitoramento de chats e notificacao manual
- Tipo: feat
- Resumo: adicionados endpoints admin para listagem/detalhe de conversas, consulta de anexos com filtros, envio manual de notificacao para usuario e auditoria com mascaramento de dados sensiveis.
- Arquivos principais: `ConsertaPraMim.API/Controllers/AdminChatsController.cs`, `ConsertaPraMim.API/Controllers/AdminChatAttachmentsController.cs`, `ConsertaPraMim.API/Controllers/AdminNotificationsController.cs`, `ConsertaPraMim.Application/Services/AdminChatNotificationService.cs`
- Risco/Impacto: medio
- [2026-02-13] [ST-007] Dashboard web admin integrado com API e polling
- Tipo: feat
- Resumo: dashboard do `ConsertaPraMim.Web.Admin` passou a consumir `GET /api/admin/dashboard` com filtros, cards KPI, tabela de eventos e estados de loading/erro/vazio com atualizacao automatica via polling controlado.
- Arquivos principais: `ConsertaPraMim.Web.Admin/Controllers/AdminHomeController.cs`, `ConsertaPraMim.Web.Admin/Views/AdminHome/Index.cshtml`, `ConsertaPraMim.Web.Admin/Services/AdminDashboardApiClient.cs`, `ConsertaPraMim.Web.Admin/Controllers/AccountController.cs`
- Risco/Impacto: medio
- [2026-02-13] [ST-008] UI Admin para operacao de usuarios
- Tipo: feat
- Resumo: implementada tela de usuarios no portal admin com filtros e paginacao, detalhe de usuario, acao de ativar/desativar com confirmacao e atualizacao da linha sem refresh completo.
- Arquivos principais: `ConsertaPraMim.Web.Admin/Controllers/AdminUsersController.cs`, `ConsertaPraMim.Web.Admin/Views/AdminUsers/Index.cshtml`, `ConsertaPraMim.Web.Admin/Views/AdminUsers/Details.cshtml`, `ConsertaPraMim.Web.Admin/Services/AdminUsersApiClient.cs`
- Risco/Impacto: medio
- [2026-02-13] [ST-009] UI Admin para operacao de pedidos, propostas e conversas
- Tipo: feat
- Resumo: criados modulos web admin para pedidos, propostas e conversas com filtros, detalhes, acoes administrativas com confirmacao, envio de notificacao manual e navegacao cruzada entre usuario, pedido, proposta e chat.
- Arquivos principais: `ConsertaPraMim.Web.Admin/Controllers/AdminServiceRequestsController.cs`, `ConsertaPraMim.Web.Admin/Controllers/AdminProposalsController.cs`, `ConsertaPraMim.Web.Admin/Controllers/AdminChatsController.cs`, `ConsertaPraMim.Web.Admin/Services/AdminOperationsApiClient.cs`
- Risco/Impacto: medio

## Template de entrada

- `[YYYY-MM-DD] [ST-XXX] Titulo curto`
- `Tipo: feat|fix|refactor|docs|test`
- `Resumo: o que foi entregue`
- `Arquivos principais: caminho1, caminho2`
- `Risco/Impacto: baixo|medio|alto`
