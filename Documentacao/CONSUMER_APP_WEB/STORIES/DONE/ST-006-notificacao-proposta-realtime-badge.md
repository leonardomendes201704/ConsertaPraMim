# ST-006 - Notificacao realtime de proposta no sino, toast no app e badge por pedido

Status: Done
Epic: EPIC-005

## Objetivo

Implementar fluxo fim-a-fim para que, ao enviar proposta no backend, o cliente receba atualizacao em tempo real no app e visualize imediatamente o impacto no pedido.

## Criterios de aceite

- App conecta no SignalR `notificationHub` apos login com token JWT.
- App invoca `JoinUserGroup` para receber notificacoes do usuario autenticado.
- Ao receber `ReceiveNotification` de proposta:
  - adiciona item na central de notificacoes;
  - incrementa contador de nao lidas no sino;
  - exibe toast na tela atual.
- Quando notificacao referencia pedido:
  - app identifica `requestId` via `actionUrl`;
  - incrementa `proposalCount` do pedido no estado local.
- `proposalCount` tambem vem do endpoint mobile dedicado:
  - `GET /api/mobile/client/orders`
  - `GET /api/mobile/client/orders/{orderId}`
- Cards de pedido no Dashboard e em Meus Pedidos exibem badge de propostas.
- Contrato mobile segue isolado dos portais web (sem quebra de telas existentes).

## Tasks

- [x] Evoluir contrato mobile de pedidos para incluir `proposalCount`.
- [x] Atualizar mapeamento backend de pedidos mobile para contar propostas ativas (`!IsInvalidated`).
- [x] Atualizar servico mobile de criacao de pedido para retornar `proposalCount` inicial.
- [x] Adicionar testes unitarios para `MobileClientOrderService` cobrindo contagem de propostas.
- [x] Adicionar cliente SignalR no app (`@microsoft/signalr`).
- [x] Criar servico front de notificacao realtime para:
  - [x] iniciar/parar conexao;
  - [x] escutar `ReceiveNotification`;
  - [x] extrair `requestId` de `actionUrl`.
- [x] Integrar `App.tsx` com notificacao realtime:
  - [x] atualizar sino/notificacoes;
  - [x] exibir toast;
  - [x] incrementar `proposalCount` no pedido impactado.
- [x] Atualizar cards de pedido (Dashboard/OrdersList) para renderizar badge de propostas.
- [x] Atualizar manual e checklist QA do app.
- [x] Gerar diagramas Mermaid (fluxo e sequencia) e atualizar indices.

## Arquivos impactados

### Backend

- `Backend/src/ConsertaPraMim.Application/DTOs/MobileClientOrderDTOs.cs`
- `Backend/src/ConsertaPraMim.Application/Services/MobileClientOrderService.cs`
- `Backend/src/ConsertaPraMim.Application/Services/MobileClientServiceRequestService.cs`
- `Backend/src/ConsertaPraMim.API/Controllers/MobileClientOrdersController.cs`
- `Backend/tests/ConsertaPraMim.Tests.Unit/Services/MobileClientOrderServiceTests.cs`

### App

- `conserta-pra-mim app/package.json`
- `conserta-pra-mim app/package-lock.json`
- `conserta-pra-mim app/App.tsx`
- `conserta-pra-mim app/types.ts`
- `conserta-pra-mim app/services/realtimeNotifications.ts`
- `conserta-pra-mim app/services/mobileOrders.ts`
- `conserta-pra-mim app/services/mobileServiceRequests.ts`
- `conserta-pra-mim app/components/Dashboard.tsx`
- `conserta-pra-mim app/components/OrdersList.tsx`

## Diagramas

- Fluxo:
  - `Documentacao/DIAGRAMAS/CONSUMER_APP_WEB/APP-006-notificacao-proposta-realtime-badge/fluxo-notificacao-proposta-realtime-app.mmd`
- Sequencia:
  - `Documentacao/DIAGRAMAS/CONSUMER_APP_WEB/APP-006-notificacao-proposta-realtime-badge/sequencia-notificacao-proposta-realtime-app.mmd`
