# ST-024 - UI de atendimento no portal admin

Status: Done  
Epic: EPIC-008

## Objetivo

Como admin, quero um painel de atendimento para operar chamados de prestadores com foco em fila e produtividade.

## Criterios de aceite

- Existe item de menu dedicado para atendimento de prestadores.
- Tela principal mostra fila com filtros, ordenacao e pagina.
- Admin abre detalhe do chamado e visualiza historico completo.
- Admin responde chamado, altera status e atribui responsavel pela UI.
- Feedback visual de atualizacao de status e erros de operacao.

## Tasks

- [x] Criar menu e rota de atendimento no `ConsertaPraMim.Web.Admin`.
- [x] Implementar grid de fila com filtros (status, prioridade, atribuicao, busca).
- [x] Implementar painel de detalhe de chamado com mensagens.
- [x] Implementar acoes de responder, alterar status e atribuir.
- [x] Integrar com endpoints admin de ST-022.
- [x] Incluir indicadores de fila (abertos, sem resposta, em atraso).
- [x] Garantir acessibilidade basica e responsividade.
- [x] Criar testes de regressao da tela de atendimento admin.
- [x] Criar/atualizar diagrama de fluxo Mermaid da funcionalidade.
- [x] Criar/atualizar diagrama de sequencia Mermaid da funcionalidade.

## Entregaveis tecnicos

- Controller MVC do atendimento admin:
  - `ConsertaPraMim.Web.Admin/Controllers/AdminSupportTicketsController.cs`
- Integracao HTTP com API admin de suporte:
  - `ConsertaPraMim.Web.Admin/Services/IAdminOperationsApiClient.cs`
  - `ConsertaPraMim.Web.Admin/Services/AdminOperationsApiClient.cs`
- View models de fila e detalhe:
  - `ConsertaPraMim.Web.Admin/Models/AdminOperationsViewModels.cs`
- Views de atendimento:
  - `ConsertaPraMim.Web.Admin/Views/AdminSupportTickets/Index.cshtml`
  - `ConsertaPraMim.Web.Admin/Views/AdminSupportTickets/Details.cshtml`
- Scripts por contexto:
  - `ConsertaPraMim.Web.Admin/wwwroot/js/views/admin-support-tickets/index.js`
  - `ConsertaPraMim.Web.Admin/wwwroot/js/views/admin-support-tickets/details.js`
- Navegacao:
  - Item de menu "Atendimento Suporte" em `ConsertaPraMim.Web.Admin/Views/Shared/_Layout.cshtml`

## Testes

- `AdminSupportTicketsControllerTests` cobrindo:
  - normalizacao de filtros na listagem;
  - bloqueio de envio com mensagem vazia;
  - atualizacao de status com feedback de sucesso;
  - atribuicao com tratamento de erro de API.

## Diagramas Mermaid

- Fluxo: `Documentacao/DIAGRAMAS/ADMIN_PORTAL/ST-024-ui-atendimento-portal-admin/fluxo-ui-atendimento-portal-admin.mmd`
- Sequencia: `Documentacao/DIAGRAMAS/ADMIN_PORTAL/ST-024-ui-atendimento-portal-admin/sequencia-ui-atendimento-portal-admin.mmd`
