# ST-079 - Bot Telegram cria lead de clientes no funil CPM Full

## Como
operacao comercial e de atendimento do ecossistema ConsertaPraMim

## Eu quero
que a conversa do bot Telegram gere e atualize automaticamente lead no funil `clientes` do CPM Full

## Para
eliminar cadastro manual, manter o CPM Full como sistema de verdade e reaproveitar a integracao ja existente do Chatwoot.

## Criterios de aceite

1. O `ConsertaPraMim.Web.TelegramBridge` deve possuir configuracao `TelegramAutomation` validada e com fallback seguro quando desligada.
2. Apos abertura automatica do pedido no bot, o bridge deve chamar a automacao interna do CPM Full sem quebrar a resposta do usuario quando a trilha externa falhar.
3. O CPM Full deve expor endpoint interno protegido por segredo para criar ou atualizar lead do board `clientes`.
4. A automacao deve reaproveitar o mesmo lead quando a mesma `ChatbotConversationId` retornar.
5. O lead criado/atualizado deve registrar `Source = Telegram`, contexto operacional e historico funcional em PT-BR.
6. O fluxo deve reaproveitar a sincronizacao atual do Chatwoot a partir do lead do funil.
7. Manual QA/Operacao, changelog, epic e indice documental devem refletir a entrega.

## Tasks

- [x] criar opcoes `TelegramAutomation` com validacao no `ConsertaPraMim.Web.TelegramBridge` e no `ConsertaPraMim.Web.CpmFull`;
- [x] criar endpoint interno `POST /api/integrations/telegram/automation/lead` no CPM Full com `ApiExplorerSettings(IgnoreApi = true)` e autenticacao por header compartilhado;
- [x] implementar `UpsertTelegramLead` no `SqlAdminKanbanService` com tabela de vinculo `cpm_web_telegram_funil_links` e idempotencia por `ChatbotConversationId`;
- [x] acionar a automacao a partir do `TelegramChatbotOrchestrator` apos `service_request_created` e no caso de reentrada com pedido ja existente;
- [x] reaproveitar `ChatwootLeadSyncService` para encaminhar o lead Telegram ao Chatwoot sem fluxo paralelo;
- [x] adicionar testes de opcoes, persistencia SQL e regressao do orquestrador;
- [x] atualizar `EPIC-TELEGRAM-001`, manual QA/Operacao do CPM Full, README da bridge, changelog e indice central.
