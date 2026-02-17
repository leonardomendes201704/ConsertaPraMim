# ST-012 - Agendamento na tela de proposta com slots e solicitacao de visita

Status: Done
Epic: EPIC-010

## Objetivo

Permitir que o cliente agende a visita diretamente na tela de detalhes da proposta aceita, com o mesmo comportamento operacional do portal do cliente e contrato mobile dedicado.

## Criterios de aceite

- Endpoint mobile dedicado para slots por proposta:
  - `GET /api/mobile/client/orders/{orderId}/proposals/{proposalId}/schedule/slots?date=yyyy-MM-dd`
- Endpoint mobile dedicado para solicitar agendamento por proposta:
  - `POST /api/mobile/client/orders/{orderId}/proposals/{proposalId}/schedule`
- Ambos os endpoints validam:
  - ownership do pedido/proposta pelo cliente autenticado;
  - proposta aceita e nao invalidada.
- Slots retornam apenas disponibilidade valida do prestador conforme regra atual da agenda.
- Criacao de agendamento respeita conflitos e regras de janela do dominio.
- `GET /api/mobile/client/orders/{orderId}/proposals/{proposalId}` passa a retornar `currentAppointment` quando existir agendamento para a proposta.
- App exibe na tela de proposta:
  - data + observacao;
  - botao `Buscar horarios disponiveis`;
  - chips clicaveis de horarios;
  - feedback de sucesso/erro;
  - resumo da janela atual quando ja existe agendamento.
- Build do app deve compilar sem erro.
- Testes unitarios do `MobileClientOrderService` devem cobrir retorno de `currentAppointment`.

## Tasks

- [x] Evoluir DTOs mobile de proposta para incluir resumo de agendamento atual (`currentAppointment`).
- [x] Ajustar `MobileClientOrderService` para mapear agendamento vinculado ao prestador da proposta.
- [x] Criar endpoint mobile dedicado de slots por proposta.
- [x] Criar endpoint mobile dedicado de solicitacao de agendamento por proposta.
- [x] Documentar detalhadamente os novos endpoints no Swagger (XML comments com regras de negocio).
- [x] Evoluir `mobileOrders.ts` com chamadas para slots e schedule.
- [x] Evoluir `types.ts` com tipos de slot e resumo de agendamento da proposta.
- [x] Evoluir `ProposalDetails.tsx` com secao completa de agendamento.
- [x] Integrar `App.tsx` para estado de agendamento (fetch slots + solicitar visita + refresh do pedido).
- [x] Cobrir backend com teste unitario para `currentAppointment` no detalhe da proposta.
- [x] Gerar diagramas Mermaid de fluxo e sequencia e atualizar indices.

## Arquivos impactados

### Backend

- `Backend/src/ConsertaPraMim.Application/DTOs/MobileClientOrderDTOs.cs`
- `Backend/src/ConsertaPraMim.Application/Services/MobileClientOrderService.cs`
- `Backend/src/ConsertaPraMim.API/Controllers/MobileClientOrdersController.cs`
- `Backend/tests/ConsertaPraMim.Tests.Unit/Services/MobileClientOrderServiceTests.cs`

### App

- `conserta-pra-mim app/types.ts`
- `conserta-pra-mim app/services/mobileOrders.ts`
- `conserta-pra-mim app/components/ProposalDetails.tsx`
- `conserta-pra-mim app/App.tsx`

### Documentacao

- `Documentacao/CONSUMER_APP_WEB/EPICS/EPIC-010-agendamento-detalhe-proposta-endpoints-mobile.md`
- `Documentacao/CONSUMER_APP_WEB/STORIES/DONE/ST-012-agendamento-na-proposta-slots-e-solicitacao.md`
- `Documentacao/DIAGRAMAS/CONSUMER_APP_WEB/APP-012-agendamento-proposta-mobile/fluxo-agendamento-proposta-mobile.mmd`
- `Documentacao/DIAGRAMAS/CONSUMER_APP_WEB/APP-012-agendamento-proposta-mobile/sequencia-agendamento-proposta-mobile.mmd`

## Diagramas

- Fluxo:
  - `Documentacao/DIAGRAMAS/CONSUMER_APP_WEB/APP-012-agendamento-proposta-mobile/fluxo-agendamento-proposta-mobile.mmd`
- Sequencia:
  - `Documentacao/DIAGRAMAS/CONSUMER_APP_WEB/APP-012-agendamento-proposta-mobile/sequencia-agendamento-proposta-mobile.mmd`
