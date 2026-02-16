# ST-004 - Detalhes do pedido com acompanhamento/historico e fluxo operacional correto

Status: Done
Epic: EPIC-003

## Objetivo

Exibir na tela de detalhes do pedido do app cliente:

- historico real de eventos (timeline);
- fluxo operacional atualizado (etapas atuais do pedido);
- comportamento consistente com o status real do backend.

## Criterios de aceite

- Existe endpoint dedicado para detalhe mobile:
  - `GET /api/mobile/client/orders/{orderId}`
- Endpoint retorna:
  - resumo do pedido (`order`);
  - etapas do fluxo (`flowSteps`);
  - timeline historica (`timeline`).
- Endpoint valida ownership do cliente autenticado.
- Tela de detalhes do app deixa de usar timeline fixa/mockada.
- Tela mostra loading, erro amigavel e retry ao carregar historico.
- Em `401/403`, app invalida sessao e retorna para login.
- Timeline e ordenada por data/hora de ocorrencia.

## Tasks

- [x] Evoluir DTO mobile para suportar fluxo e timeline no detalhe.
- [x] Evoluir interface `IMobileClientOrderService` com busca de detalhe por ID.
- [x] Implementar `GetOrderDetailsAsync` em `MobileClientOrderService`.
- [x] Montar flow steps de acordo com status do pedido.
- [x] Montar timeline historica com eventos de:
  - criacao do pedido;
  - propostas;
  - agendamentos e historico de status;
  - avaliacao do cliente (quando houver);
  - fechamento/cancelamento (fallback).
- [x] Expor endpoint `GET /api/mobile/client/orders/{orderId}` no controller mobile.
- [x] Ajustar repositório para incluir relacionamentos necessarios ao historico (propostas provider/reviews).
- [x] Criar consumo front-end `fetchMobileClientOrderDetails`.
- [x] Refatorar `RequestDetails.tsx` para renderizar fluxo e timeline reais.
- [x] Refatorar `App.tsx` para carregar detalhes sob demanda ao abrir pedido.
- [x] Incluir tratamento de erro e retry na tela de detalhes.
- [x] Atualizar documentacao e diagramas.

## Arquivos impactados

- Backend:
  - `Backend/src/ConsertaPraMim.API/Controllers/MobileClientOrdersController.cs`
  - `Backend/src/ConsertaPraMim.Application/DTOs/MobileClientOrderDTOs.cs`
  - `Backend/src/ConsertaPraMim.Application/Interfaces/IMobileClientOrderService.cs`
  - `Backend/src/ConsertaPraMim.Application/Services/MobileClientOrderService.cs`
  - `Backend/src/ConsertaPraMim.Infrastructure/Repositories/ServiceRequestRepository.cs`
- App:
  - `conserta-pra-mim app/services/mobileOrders.ts`
  - `conserta-pra-mim app/types.ts`
  - `conserta-pra-mim app/App.tsx`
  - `conserta-pra-mim app/components/RequestDetails.tsx`

## Diagramas

- Fluxo:
  - `Documentacao/DIAGRAMAS/CONSUMER_APP_WEB/APP-004-detalhes-pedido-fluxo-historico/fluxo-detalhes-pedido-historico.mmd`
- Sequencia:
  - `Documentacao/DIAGRAMAS/CONSUMER_APP_WEB/APP-004-detalhes-pedido-fluxo-historico/sequencia-detalhes-pedido-historico.mmd`
