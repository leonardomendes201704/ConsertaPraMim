# ST-081 - Bot Telegram cria lead de prestadores no funil CPM Full

## Como
operacao comercial, onboarding e suporte ao prestador no ecossistema ConsertaPraMim

## Eu quero
que a conversa autenticada de prestadores no bot Telegram gere e atualize automaticamente lead no funil `prestadores` do CPM Full

## Para
eliminar cadastro manual, manter o CPM Full como sistema de verdade e reaproveitar a integracao ja existente do Chatwoot tambem para a jornada do prestador.

## Criterios de aceite

1. O `ConsertaPraMim.Web.TelegramBridge` deve aceitar login de `Provider` sem quebrar o fluxo atual de `Client`.
2. O `TelegramChatbotController` deve aceitar sessao, mensagens, snapshots, actions, estado e historico para `Client` e `Provider`.
3. Endpoints de pedidos/agendamentos devem continuar restritos a `Client`.
4. Conversa autenticada como `Provider` deve criar ou atualizar lead no board `prestadores`.
5. O fluxo deve respeitar `ProvidersAutomationEnabled` de forma independente de `ClientsAutomationEnabled`.
6. A conversa do prestador deve registrar `Source = Telegram`, contexto operacional e vinculo tecnico por `ChatbotConversationId`.
7. Manual QA/Operacao, changelog, epic, README da bridge e indice central devem refletir a entrega.

## Tasks

- [x] ampliar o login do bridge para aceitar `Provider`;
- [x] adaptar o contrato do `TelegramChatbotController` para sessao/trilha conversacional de `Client` e `Provider`;
- [x] preservar `service requests`, pedidos e agenda como fluxos `client-only`;
- [x] adicionar contexto de papel autenticado no `TelegramChatbotOrchestrator`;
- [x] criar automacao do board `prestadores` usando `ProvidersAutomationEnabled`;
- [x] adicionar cobertura de regressao para login provider, controller e orquestrador;
- [x] atualizar `EPIC-TELEGRAM-001`, manual QA/Operacao, README da bridge, changelog e indice central.
