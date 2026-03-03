# Manual QA/Operacao - Chatbot Telegram (ST-004 a ST-010)

## 1. Objetivo

Padronizar QA e operacao para o fluxo de chatbot Telegram mediado por IA, incluindo persistencia conversacional na API, triagem automatica, abertura de pedido, matching, agendamento e consultas naturais.

## 2. Escopo atual da entrega

- ST-004 concluida:
  - Entidades de dominio base para conversa, mensagens, snapshots de contexto e logs de acao.
  - Mapeamento EF Core + migration inicial de persistencia do chatbot.
  - Servico de aplicacao para abrir/retomar conversa e registrar mensagem, contexto, estado e eventos com validacao de campos e normalizacao UTC.
  - Endpoints `/api/telegram-chatbot/*` publicados para sessao, mensagens, contexto, acoes, estado e historico com `Authorize(Roles = "Client")`.
  - Politica temporal consolidada: API persiste/retorna UTC e evita conversao de timezone no contrato de backend.
  - Controle de isolamento por cliente aplicado no servico para impedir leitura/escrita cruzada entre `ClientId`.
- ST-005 em andamento:
  - Tela e controller de login adicionados no `ConsertaPraMim.Web.TelegramBridge`.
  - `chat.js` e SignalR carregados apenas na tela do chat (`Home/Index`) para nao quebrar paginas de autenticacao.

## 3. Validacoes executadas no ciclo atual

- `dotnet build Backend/src/src.sln`
- `dotnet ef migrations add AddTelegramChatbotConversationFoundation --project Backend/src/ConsertaPraMim.Infrastructure --startup-project Backend/src/ConsertaPraMim.API --output-dir Migrations`
- `dotnet build Backend/src/ConsertaPraMim.Web.TelegramBridge/ConsertaPraMim.Web.TelegramBridge.csproj`
- Validacao tecnica manual do contrato `ITelegramChatbotConversationService`:
  - `OpenOrResumeConversationAsync` cria/retoma conversa por (`ClientId`, `Channel`, `ChannelConversationId`).
  - `RegisterMessageAsync` persiste mensagem inbound/outbound/system e atualiza `LastInteractionAtUtc`.
  - `RegisterContextSnapshotAsync` persiste contexto de orquestracao com payload JSON.
  - `RegisterActionLogAsync` registra eventos de negocio e status `Pending/Succeeded/Failed`.
- Validacao tecnica manual dos endpoints:
  - `POST /api/telegram-chatbot/session`
  - `POST /api/telegram-chatbot/messages`
  - `POST /api/telegram-chatbot/context-snapshots`
  - `POST /api/telegram-chatbot/actions`
  - `PATCH /api/telegram-chatbot/conversations/{conversationId}/state`
  - `GET /api/telegram-chatbot/conversations/{conversationId}/history`
- Validacao de documentacao OpenAPI:
  - `ApiEndpointDocumentationCatalog` com narrativa dedicada para `/api/telegram-chatbot/*`.
  - `ComprehensiveSwaggerOperationFilter` com exemplos e parametros de canal/intent para chatbot.
  - `ApiTagDescriptionsDocumentFilter` com ordenacao priorizando a tag `TelegramChatbot`.
- Testes automatizados executados:
  - `dotnet test Backend/tests/ConsertaPraMim.Tests.Unit/ConsertaPraMim.Tests.Unit.csproj --filter "FullyQualifiedName~TelegramChatbot"`
  - Cobertura criada para:
    - servico `TelegramChatbotConversationService` (UTC, validacao de payload e isolamento por cliente);
    - controller `TelegramChatbotController` com SQLite (autorizacao por role Client, acesso cruzado e persistencia UTC).

## 4. Checklist smoke inicial (em evolucao)

- [ ] QA-CBT-001: persistencia de conversa vinculada ao `ClientId` autenticado.
- [ ] QA-CBT-002: registro de mensagem inbound/outbound com `timestamp` UTC.
- [ ] QA-CBT-003: bloqueio de acesso cruzado entre clientes em historico/conversa.
- [ ] QA-CBT-004: registro de log de acao conversacional com trilha auditavel.
- [ ] QA-CBT-005: endpoint de sessao retorna a mesma conversa para mesmo (`ClientId`, `Channel`, `ChannelConversationId`).
- [ ] QA-CBT-006: endpoint de historico nao retorna conversa de outro cliente (esperado `404`).
- [ ] QA-CBT-007: rota `/Account/Login` renderiza formulario de email/senha sem executar `chat.js`.

## 5. Troubleshooting inicial

### 5.1 Conversa nao persiste no banco

- Validar se migration da ST-004 foi aplicada no ambiente.
- Verificar string de conexao e disponibilidade do SQL Server.
- Revisar logs da API para erro de validacao de entidade.

### 5.2 Historico retorna vazio para cliente valido

- Confirmar `ClientId` do token JWT e vinculo da conversa no banco.
- Revisar filtros de autorizacao por cliente no endpoint.

## 6. Historico de revisoes

- 2026-03-03: versao inicial criada durante a ST-004 (Task 1).
- 2026-03-03: atualizacao com mapeamento EF Core e migration da ST-004 (Task 2).
- 2026-03-03: atualizacao com servico e repositorio de persistencia conversacional da ST-004 (Task 3).
- 2026-03-03: atualizacao com endpoints API e Swagger do chatbot Telegram da ST-004 (Task 4).
- 2026-03-03: atualizacao com consolidacao de UTC, autorizacao por `ClientId` e paridade Swagger da ST-004 (Tasks 5, 6 e 7).
- 2026-03-03: atualizacao com testes unitarios/integracao de persistencia e autorizacao da ST-004 (Task 8).
- 2026-03-03: atualizacao com diagrama Mermaid de fluxo da ST-004 (Task 9).
- 2026-03-03: atualizacao com diagrama Mermaid de sequencia da ST-004 (Task 10).
- 2026-03-03: atualizacao com tela/controller de login no Telegram Bridge da ST-005 (Task 1).
