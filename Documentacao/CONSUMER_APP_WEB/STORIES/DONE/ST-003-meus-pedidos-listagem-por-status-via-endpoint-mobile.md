# ST-003 - Listagem real de "Meus Pedidos" no app com endpoint mobile dedicado

Status: Done
Epic: EPIC-003

## Objetivo

Implementar o fluxo fim-a-fim para o app cliente consultar pedidos reais do usuario autenticado, separados em finalizados e nao finalizados, usando endpoint exclusivo do app/mobile.

## Criterios de aceite

- App nao usa mais mock para popular "Meus Pedidos".
- Existe endpoint dedicado para app/mobile:
  - `GET /api/mobile/client/orders`
- Endpoint exige autenticacao e role `Client`.
- Endpoint retorna listas separadas:
  - `openOrders`
  - `finalizedOrders`
- Contrato do endpoint e documentado no Swagger com contexto de negocio.
- Tela "Meus Pedidos" exibe contadores corretos por aba.
- Cards continuam clicaveis para abrir detalhes.
- Em falha da API de pedidos, app exibe mensagem amigavel e opcao de tentar novamente.
- Em 401/403 no endpoint de pedidos, app invalida sessao e redireciona para login.

## Tasks

- [x] Criar DTO dedicado para resposta mobile de pedidos.
- [x] Criar interface de aplicacao para leitura de pedidos mobile do cliente.
- [x] Implementar servico de aplicacao para:
  - classificar pedidos em aberto/finalizados;
  - mapear status do dominio para status de exibicao no app;
  - mapear categoria para icone de card.
- [x] Registrar servico no `DependencyInjection` da camada Application.
- [x] Criar controller API dedicado ao app em rota `api/mobile/client/orders`.
- [x] Documentar endpoint com comentarios de negocio para Swagger.
- [x] Criar servico front-end no app para consumo do endpoint com token JWT.
- [x] Refatorar `App.tsx` para:
  - carregar pedidos apos login;
  - carregar pedidos ao restaurar sessao;
  - manter estado separado de pedidos ativos/finalizados.
- [x] Refatorar `OrdersList.tsx` para receber listas separadas do container.
- [x] Adicionar tratamento de erro/loading na tela de pedidos.
- [x] Atualizar documentacao da trilha e README do app.
- [x] Criar diagramas Mermaid (fluxo e sequencia) da funcionalidade.

## Atualizacao de implementacao (2026-02-16)

### Backend/API

- Novos arquivos:
  - `Backend/src/ConsertaPraMim.Application/DTOs/MobileClientOrderDTOs.cs`
  - `Backend/src/ConsertaPraMim.Application/Interfaces/IMobileClientOrderService.cs`
  - `Backend/src/ConsertaPraMim.Application/Services/MobileClientOrderService.cs`
  - `Backend/src/ConsertaPraMim.API/Controllers/MobileClientOrdersController.cs`
- Registro DI adicionado em:
  - `Backend/src/ConsertaPraMim.Application/DependencyInjection.cs`
- Endpoint novo:
  - `GET /api/mobile/client/orders?takePerBucket=100`
- Regras principais:
  - Apenas role `Client`.
  - Bucket `openOrders`: pedidos nao finalizados.
  - Bucket `finalizedOrders`: `Completed`, `Validated`, `Canceled`.
  - Status de saida adaptado para app: `AGUARDANDO`, `EM_ANDAMENTO`, `CONCLUIDO`, `CANCELADO`.

### App cliente web

- Novo servico:
  - `conserta-pra-mim app/services/mobileOrders.ts`
- `conserta-pra-mim app/App.tsx` atualizado para:
  - remover mock fixo de pedidos;
  - carregar pedidos via API no login e na restauracao de sessao;
  - separar estado em `openOrders` e `finalizedOrders`;
  - tratar erro de carregamento com mensagem amigavel;
  - redirecionar para login quando resposta de pedidos for `401/403`.
- `conserta-pra-mim app/components/OrdersList.tsx` atualizado para:
  - receber listas separadas;
  - mostrar loading e retry;
  - manter tabs "Ativos" e "Historico" com contadores reais.

## Diagramas

- Fluxo:
  - `Documentacao/DIAGRAMAS/CONSUMER_APP_WEB/APP-003-meus-pedidos-api-mobile/fluxo-meus-pedidos-api-mobile.mmd`
- Sequencia:
  - `Documentacao/DIAGRAMAS/CONSUMER_APP_WEB/APP-003-meus-pedidos-api-mobile/sequencia-meus-pedidos-api-mobile.mmd`
