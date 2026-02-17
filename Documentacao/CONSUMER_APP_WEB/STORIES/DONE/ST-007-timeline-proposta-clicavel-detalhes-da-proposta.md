# ST-007 - Timeline de proposta clicavel com tela de detalhes da proposta no app

Status: Done
Epic: EPIC-006

## Objetivo

Transformar o item "Proposta recebida" do historico do pedido em um ponto de navegacao para uma tela detalhada da proposta, com endpoint mobile dedicado.

## Criterios de aceite

- Timeline de `GET /api/mobile/client/orders/{orderId}` retorna, nos eventos de proposta:
  - `relatedEntityType = "proposal"`
  - `relatedEntityId = <proposalId>`
- App renderiza evento de proposta como item clicavel no historico.
- Clique no evento de proposta abre tela "Detalhes da proposta".
- Tela "Detalhes da proposta" consome endpoint mobile dedicado:
  - `GET /api/mobile/client/orders/{orderId}/proposals/{proposalId}`
- Endpoint valida ownership do cliente (pedido deve pertencer ao cliente autenticado).
- Em erro 404/401/403, app mostra mensagem amigavel e retry.
- Swagger da API documenta o novo endpoint com foco de negocio.

## Tasks

- [x] Evoluir contrato de timeline mobile para incluir referencia de entidade relacionada.
- [x] Implementar endpoint mobile dedicado de detalhe de proposta por pedido.
- [x] Implementar metodo de aplicacao para retornar resumo do pedido + detalhe comercial da proposta.
- [x] Atualizar service mobile do app para consumir novo endpoint.
- [x] Tornar evento "Proposta recebida" clicavel na timeline da tela de detalhes.
- [x] Criar tela dedicada `ProposalDetails` no app.
- [x] Integrar navegacao de estado (`AppState`) para fluxo `REQUEST_DETAILS -> PROPOSAL_DETAILS`.
- [x] Cobrir backend com testes unitarios de timeline referenciada e detalhe de proposta.
- [x] Atualizar documentacao da trilha e indices.
- [x] Gerar diagramas Mermaid (fluxo e sequencia) e catalogar no indice.

## Arquivos impactados

### Backend

- `Backend/src/ConsertaPraMim.Application/DTOs/MobileClientOrderDTOs.cs`
- `Backend/src/ConsertaPraMim.Application/Interfaces/IMobileClientOrderService.cs`
- `Backend/src/ConsertaPraMim.Application/Services/MobileClientOrderService.cs`
- `Backend/src/ConsertaPraMim.API/Controllers/MobileClientOrdersController.cs`
- `Backend/tests/ConsertaPraMim.Tests.Unit/Services/MobileClientOrderServiceTests.cs`

### App

- `conserta-pra-mim app/types.ts`
- `conserta-pra-mim app/services/mobileOrders.ts`
- `conserta-pra-mim app/components/RequestDetails.tsx`
- `conserta-pra-mim app/components/ProposalDetails.tsx`
- `conserta-pra-mim app/App.tsx`

## Diagramas

- Fluxo:
  - `Documentacao/DIAGRAMAS/CONSUMER_APP_WEB/APP-007-historico-proposta-detalhes/fluxo-historico-proposta-detalhes.mmd`
- Sequencia:
  - `Documentacao/DIAGRAMAS/CONSUMER_APP_WEB/APP-007-historico-proposta-detalhes/sequencia-historico-proposta-detalhes.mmd`
