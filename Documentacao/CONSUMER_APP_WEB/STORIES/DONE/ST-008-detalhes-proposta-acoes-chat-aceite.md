# ST-008 - Tela de detalhes da proposta com acoes de chat e aceite

Status: Done
Epic: EPIC-006

## Objetivo

Permitir que o cliente, ao abrir os detalhes de uma proposta no app, consiga:

- iniciar conversa direta com o prestador da proposta;
- aceitar a proposta sem sair da tela de detalhes.

## Criterios de aceite

- Tela `PROPOSAL_DETAILS` exibe botao `Conversar com o prestador`.
- Ao tocar em `Conversar com o prestador`, app abre a tela `CHAT` com contexto do prestador da proposta.
- Tela `PROPOSAL_DETAILS` exibe botao `Aceitar proposta`.
- Aceite utiliza endpoint mobile dedicado:
  - `POST /api/mobile/client/orders/{orderId}/proposals/{proposalId}/accept`
- Endpoint valida ownership do cliente e consistencia de proposta antes de aceitar.
- Em aceite com sucesso:
  - app atualiza estado local do pedido;
  - status da proposta reflete `Aceita`;
  - app exibe feedback amigavel de sucesso.
- Quando proposta estiver `Aceita` ou `Invalidada`, botao de aceite fica indisponivel.
- Swagger documenta endpoint de aceite com foco de negocio.

## Tasks

- [x] Evoluir contrato mobile de detalhe de proposta para incluir `providerId`.
- [x] Expor endpoint mobile dedicado para aceitar proposta no contexto de pedido do cliente.
- [x] Implementar metodo de aplicacao para aceitar proposta e retornar payload atualizado.
- [x] Atualizar `mobileOrders.ts` com chamada `POST` de aceite de proposta.
- [x] Evoluir `ProposalDetails.tsx` com botoes de `Conversar` e `Aceitar proposta`.
- [x] Integrar `App.tsx` para:
  - abrir chat com contexto do prestador;
  - executar aceite e atualizar estado local do pedido/proposta.
- [x] Cobrir backend com teste unitario de aceite na service mobile.
- [x] Atualizar documentacao da trilha e catalogo de diagramas.
- [x] Gerar diagramas Mermaid (fluxo e sequencia) para a nova funcionalidade.

## Arquivos impactados

### Backend

- `Backend/src/ConsertaPraMim.Application/DTOs/MobileClientOrderDTOs.cs`
- `Backend/src/ConsertaPraMim.Application/Interfaces/IMobileClientOrderService.cs`
- `Backend/src/ConsertaPraMim.Application/Services/MobileClientOrderService.cs`
- `Backend/src/ConsertaPraMim.API/Controllers/MobileClientOrdersController.cs`
- `Backend/tests/ConsertaPraMim.Tests.Unit/Services/MobileClientOrderServiceTests.cs`

### App

- `conserta-pra-mim app/services/mobileOrders.ts`
- `conserta-pra-mim app/components/ProposalDetails.tsx`
- `conserta-pra-mim app/App.tsx`
- `conserta-pra-mim app/types.ts`

## Diagramas

- Fluxo:
  - `Documentacao/DIAGRAMAS/CONSUMER_APP_WEB/APP-008-detalhes-proposta-chat-aceite/fluxo-detalhes-proposta-chat-aceite.mmd`
- Sequencia:
  - `Documentacao/DIAGRAMAS/CONSUMER_APP_WEB/APP-008-detalhes-proposta-chat-aceite/sequencia-detalhes-proposta-chat-aceite.mmd`
