# ST-025 - Realtime, notificacoes e SLA basico de suporte

Status: Done  
Epic: EPIC-008

## Objetivo

Como operacao, quero notificacoes e atualizacao quase em tempo real dos chamados para reduzir tempo de resposta e evitar fila esquecida.

## Criterios de aceite

- Novo ticket criado gera aviso para o painel admin.
- Nova resposta do admin gera aviso para o prestador.
- Tela de detalhe atualiza mensagens sem refresh manual (quando possivel).
- Existe indicador de tempo sem resposta (SLA basico) na fila admin.
- Falhas de notificacao nao quebram o fluxo principal de atendimento.

## Tasks

- [x] Definir estrategia de realtime (reuso de `NotificationHub`).
- [x] Publicar eventos de ticket criado/mensagem/status alterado.
- [x] Implementar consumo de eventos no portal admin.
- [x] Implementar consumo de eventos no portal prestador.
- [x] Implementar fallback de polling para ambientes sem websocket.
- [x] Adicionar calculo de SLA basico (primeira resposta e tempo sem resposta atual).
- [x] Criar testes de integracao/realtime com cenarios de reconexao e falha de notificacao.
- [x] Criar/atualizar diagrama de fluxo Mermaid da funcionalidade.
- [x] Criar/atualizar diagrama de sequencia Mermaid da funcionalidade.

## Entregaveis tecnicos

- Notificacoes resilientes no backend:
  - `ConsertaPraMim.Application/Services/MobileProviderService.cs`
  - `ConsertaPraMim.Application/Services/AdminSupportTicketService.cs`
- Realtime no portal admin:
  - `ConsertaPraMim.Web.Admin/Views/Shared/_Layout.cshtml`
  - `ConsertaPraMim.Web.Admin/wwwroot/js/layout/admin-layout.js`
- Fallback de polling e auto refresh nos detalhes:
  - `ConsertaPraMim.Web.Admin/Controllers/AdminSupportTicketsController.cs`
  - `ConsertaPraMim.Web.Provider/Controllers/SupportTicketsController.cs`
  - `ConsertaPraMim.Web.Admin/wwwroot/js/views/admin-support-tickets/details.js`
  - `ConsertaPraMim.Web.Provider/wwwroot/js/views/support-tickets/details.js`
- Consumo de evento nas listas:
  - `ConsertaPraMim.Web.Admin/wwwroot/js/views/admin-support-tickets/index.js`
  - `ConsertaPraMim.Web.Provider/wwwroot/js/views/support-tickets/index.js`
- SLA basico na fila admin:
  - `ConsertaPraMim.Web.Admin/Views/AdminSupportTickets/Index.cshtml`

## Testes

- `MobileProviderSupportTicketServiceInMemoryIntegrationTests`:
  - criacao de chamado continua funcionando mesmo com falha no canal de notificacao.
- `AdminSupportTicketServiceInMemoryIntegrationTests`:
  - resposta admin continua funcionando mesmo com falha no canal de notificacao.
- `AdminSupportTicketsControllerTests`:
  - endpoint `PollDetails` retorna snapshot de atualizacao.
- `ProviderSupportTicketsControllerTests`:
  - endpoint `PollDetails` retorna snapshot de atualizacao.

## Diagramas Mermaid

- Fluxo: `Documentacao/DIAGRAMAS/ADMIN_PORTAL/ST-025-realtime-notificacoes-sla-suporte/fluxo-realtime-notificacoes-sla-suporte.mmd`
- Sequencia: `Documentacao/DIAGRAMAS/ADMIN_PORTAL/ST-025-realtime-notificacoes-sla-suporte/sequencia-realtime-notificacoes-sla-suporte.mmd`
