# ST-023 - UI de suporte no portal prestador

Status: Done  
Epic: EPIC-008

## Objetivo

Como prestador, quero uma area de suporte com lista de chamados e conversa para abrir e acompanhar atendimentos.

## Criterios de aceite

- Existe item de menu de suporte no portal prestador.
- Tela lista chamados com status, prioridade, ultima atualizacao e acao de detalhe.
- Prestador consegue abrir novo chamado via formulario dedicado.
- Tela de detalhe exibe historico e permite enviar nova mensagem.
- Fluxo de erro, carregamento e vazio tratados de forma clara.

## Tasks

- [x] Criar menu e rota de suporte no `ConsertaPraMim.Web.Provider`.
- [x] Implementar tela de listagem de chamados com paginacao.
- [x] Implementar tela/modal de criacao de chamado.
- [x] Implementar tela de detalhe com timeline de mensagens.
- [x] Integrar com endpoints provider de ST-021.
- [x] Aplicar padrao visual e responsividade para desktop/mobile web.
- [x] Adicionar estados UX (loading, empty state, erro de API).
- [x] Criar testes funcionais basicos da navegacao e do fluxo principal.
- [x] Criar/atualizar diagrama de fluxo Mermaid da funcionalidade.
- [x] Criar/atualizar diagrama de sequencia Mermaid da funcionalidade.

## Entregaveis tecnicos

- Controller MVC de suporte no portal prestador:
  - `ConsertaPraMim.Web.Provider/Controllers/SupportTicketsController.cs`
- View models da UI de suporte:
  - `ConsertaPraMim.Web.Provider/Models/SupportTicketsViewModels.cs`
- Integracao HTTP com API mobile/provider:
  - `ConsertaPraMim.Web.Provider/Services/IProviderBackendApiClient.cs`
  - `ConsertaPraMim.Web.Provider/Services/ProviderBackendApiClient.cs`
- Views de suporte:
  - `ConsertaPraMim.Web.Provider/Views/SupportTickets/Index.cshtml`
  - `ConsertaPraMim.Web.Provider/Views/SupportTickets/Create.cshtml`
  - `ConsertaPraMim.Web.Provider/Views/SupportTickets/Details.cshtml`
- Scripts por contexto:
  - `ConsertaPraMim.Web.Provider/wwwroot/js/views/support-tickets/index.js`
  - `ConsertaPraMim.Web.Provider/wwwroot/js/views/support-tickets/create.js`
  - `ConsertaPraMim.Web.Provider/wwwroot/js/views/support-tickets/details.js`
- Navegacao:
  - Item de menu "Suporte" em `ConsertaPraMim.Web.Provider/Views/Shared/_Layout.cshtml`

## Testes

- `ProviderSupportTicketsControllerTests` cobrindo:
  - normalizacao de filtros na listagem;
  - fluxo de criacao (sucesso e ModelState invalido);
  - validacao de mensagem vazia;
  - fechamento com ticket invalido.

## Diagramas Mermaid

- Fluxo: `Documentacao/DIAGRAMAS/ADMIN_PORTAL/ST-023-ui-suporte-portal-prestador/fluxo-ui-suporte-portal-prestador.mmd`
- Sequencia: `Documentacao/DIAGRAMAS/ADMIN_PORTAL/ST-023-ui-suporte-portal-prestador/sequencia-ui-suporte-portal-prestador.mmd`