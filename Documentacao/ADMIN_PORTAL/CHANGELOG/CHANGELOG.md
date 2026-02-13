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

## Template de entrada

- `[YYYY-MM-DD] [ST-XXX] Titulo curto`
- `Tipo: feat|fix|refactor|docs|test`
- `Resumo: o que foi entregue`
- `Arquivos principais: caminho1, caminho2`
- `Risco/Impacto: baixo|medio|alto`
