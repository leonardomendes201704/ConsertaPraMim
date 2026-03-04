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
- ST-005 concluida:
  - Tela e controller de login adicionados no `ConsertaPraMim.Web.TelegramBridge`.
  - `chat.js` e SignalR carregados apenas na tela do chat (`Home/Index`) para nao quebrar paginas de autenticacao.
  - Login da bridge integrado ao endpoint oficial `POST /api/auth/login` da API.
  - Sessao autenticada persistida por cookie seguro (`HttpOnly`, `SameSite=Strict`, expiracao e sliding expiration).
  - Rotas do chat (MVC/API/SignalR) protegidas por autenticacao e redirecionamento para login.
  - `ChatApiController` sincroniza sessao/mensagem com `ConsertaPraMim.API` via token da sessao (ClientId derivado no backend).
  - Login abre automaticamente conversa unica por cliente (sem input de `chatId`) e bloqueia acesso a conversas de outros clientes.
  - Logout invalida cookie de autenticacao local e remove acesso imediato ao chat.
- ST-006 concluida:
  - Criado gateway OpenAI na bridge (`OpenAiTelegramGateway`) usando `Responses API`.
  - Implementados retries para erros transientes (`408`, `429`, `500`, `502`, `503`, `504`), timeout por request e tratamento de erro de rede/timeout.
  - Criadas opcoes dedicadas (`TelegramBridgeAi`) e modelos de transporte para prompt/resposta da orquestracao.
  - Orquestrador `TelegramChatbotOrchestrator` integrado ao envio de mensagem do cliente, com resposta automatica da IA no mesmo chat.
  - Prompt system/politicas de atendimento humano e contrato de saida estruturada em JSON (`messageToClient`, `intent`, `nextStep`, `confidence`, `entities`).
  - Montagem de contexto por cliente/conversa via historico consolidado da API (`messages`, `contextSnapshots`, `actionLogs`).
  - Persistencia de trilha da orquestracao na API (`context-snapshots`, `actions`, `state`) com `intent`, `nextStep`, tokens e correlacao.
  - Fallback seguro quando IA falha e cache em memoria por conversa/mensagem para reduzir custo e latencia em reenvios identicos.
  - Instrumentacao de logs e metricas (`requests`, `failures`, `fallbacks`, `latency`, `tokens`) para auditoria operacional/custo.
- ST-007 concluida:
  - Contrato de intent `open_service_request` e entidades de triagem definidos no orquestrador (categoria, descricao do problema, equipamento, marca/modelo, CEP, cidade/rua e disponibilidade).
  - Motor de triagem (`TelegramServiceRequestTriageEngine`) implementado para manter estado por conversa, identificar dados faltantes e orientar follow-up em linguagem natural.
  - Abertura automatica de pedido integrada via `POST /api/service-requests` quando os dados minimos estao completos.
  - Payload de criacao alinhado ao contrato da API: categoria enviada como enum numerico (`ServiceCategory`) para evitar rejeicao por model binding.
  - Bridge passou a pre-resolver CEP em `GET /api/service-requests/zip-resolution` antes da criacao para enriquecer rua/cidade/coordenadas e reduzir falha transiente na abertura.
  - Payload final usado na abertura e estado de triagem persistidos em snapshots/contexto conversacional para continuidade.
  - Confirmacao amigavel com resumo e protocolo do pedido enviada automaticamente ao cliente apos criacao com sucesso.
- ST-008 concluida:
  - Endpoint do chatbot para matching foi iniciado em `GET /api/telegram-chatbot/service-requests/{serviceRequestId}/eligible-providers`.
  - Matching considera categoria do pedido, cobertura geografica (raio/distancia), preferencia PF/PJ e status ativo do prestador.
  - Endpoint de agendamento em lote iniciado em `POST /api/telegram-chatbot/service-requests/{serviceRequestId}/schedule-visits-batch`.
  - Agendamento em lote valida limite de ate 3 visitas, dias distintos e devolve status por visita (sucesso/falha) para replanejamento conversacional.
  - Parser de linguagem natural da agenda criado na bridge para interpretar dia da semana + periodo/horario e gerar janelas UTC.
  - Orquestrador agora fecha o ciclo ST-008: apos criar pedido, lista prestadores elegiveis, persiste sugestoes/decisoes em snapshots/actions e dispara agendamento em lote com resposta natural de sucesso parcial/total.

## 3. Validacoes executadas no ciclo atual

- `dotnet build Backend/src/src.sln`
- `dotnet ef migrations add AddTelegramChatbotConversationFoundation --project Backend/src/ConsertaPraMim.Infrastructure --startup-project Backend/src/ConsertaPraMim.API --output-dir Migrations`
- `dotnet build Backend/src/ConsertaPraMim.Web.TelegramBridge/ConsertaPraMim.Web.TelegramBridge.csproj`
- Validacao funcional manual ST-005 task 2:
  - Submissao de login chama `ITelegramBridgeAuthApiClient` -> `POST {ApiBaseUrl}/api/auth/login`.
  - Bridge aceita apenas resposta com role `Client`.
- Validacao funcional manual ST-005 task 3:
  - Login bem-sucedido executa `SignInAsync` com token API em claim `telegram_bridge_api_token`.
  - Cookie `ConsertaPraMim.TelegramBridge.Auth` usa expiracao de 12h (ou 7 dias com `RememberMe`).
- Validacao funcional manual ST-005 task 4:
  - `HomeController` exige usuario autenticado (`[Authorize]`) e redireciona anonimos para `/Account/Login`.
  - `ChatApiController` e `TelegramChatHub` exigem autenticacao para operacoes de chat.
- Validacao funcional manual ST-005 task 5:
  - Abertura de conversa no painel chama `/api/telegram-chatbot/session` com `Bearer` do claim `telegram_bridge_api_token`.
  - Envio de mensagem no painel chama `/api/telegram-chatbot/messages` usando `conversationId` da sessao sincronizada.
- Validacao funcional manual ST-005 task 6:
  - Botao `Sair` executa `POST /Account/Logout` com antiforgery.
  - Após logout, usuario volta para `/Account/Login` e endpoints protegidos retornam nao autorizado para nova chamada.
- Validacao funcional manual ST-005 conversa unica por cliente:
  - `GET /api/chats` retorna somente a conversa derivada do `ClientId` da sessao.
  - Primeira carga do chat cria conversa automaticamente quando ainda nao existe historico local.
  - `GET /api/chats/{chatId}/messages` e `POST /api/chats/{chatId}/messages` retornam `403` quando `chatId` nao pertence ao cliente autenticado.
  - `TelegramChatHub.JoinConversation` aceita apenas o grupo de conversa derivado do `ClientId` da sessao.
  - `chatId` e serializado como string no payload (`WriteAsString`) para evitar perda de precisao do `long` no JavaScript.
- Validacao tecnica manual ST-006 task 1:
  - `OpenAiTelegramGateway.GenerateReplyAsync` falha com erro controlado quando `ApiKey` ou prompt estao ausentes.
  - Chamadas HTTP da OpenAI respeitam timeout configuravel e retries para erros transientes.
  - Build da bridge valida compilacao das novas opcoes/modelos/interfaces do gateway.
- Validacao funcional manual ST-006 tasks 2 a 7:
  - `ChatApiController.SendMessage` agora aciona o orquestrador apos persistir mensagem do cliente e publica resposta do assistente no mesmo chat.
  - `TelegramChatbotApiClient` registra mensagem do assistente com `intent`, `nextStep`, tokens e metadata da orquestracao.
  - `TelegramChatbotOrchestrator` consulta historico da conversa na API e compoe contexto limitado por `MaxContextMessages`, `MaxContextSnapshots` e `MaxContextActionLogs`.
  - Quando OpenAI falha, o cliente recebe `FallbackMessage` seguro e a falha fica registrada em `actions`.
  - Para mensagens repetidas no TTL configurado, resposta e reutilizada via cache e `UsedCache=true`.
  - Metricas da orquestracao sao emitidas para requests, fallbacks, falhas, latencia e tokens.
- Validacao documental ST-006 task 9:
  - Publicado diagrama Mermaid de fluxo da orquestracao OpenAI com contexto, fallback, cache e persistencia da trilha.
- Validacao documental ST-006 task 10:
  - Publicado diagrama Mermaid de sequencia da orquestracao OpenAI detalhando chamadas entre `ChatApi`, `Orchestrator`, `OpenAI` e `/api/telegram-chatbot/*`.
- Validacao funcional manual ST-007 tasks 1 a 6:
  - `TelegramChatbotOrchestrator` aplica triagem apos resposta da IA e aciona o endpoint de criacao de pedido quando categoria, descricao e CEP estao completos.
  - Em caso de dados faltantes, a resposta ao cliente vira follow-up objetivo para coletar o campo ausente (categoria, descricao ou CEP).
  - Em caso de sucesso, o orquestrador registra `open_service_request_api` em `actions` e snapshot `service_request_open_payload` no historico.
  - Em caso de erro na criacao, o fluxo registra falha e orienta retry com mensagem segura para o cliente.
- Validacao funcional manual ST-007 pos-hotfix (categoria + CEP):
  - Fluxo de triagem com CEP `11704150` e categoria `Eletrodomesticos` abre pedido com sucesso sem cair no fallback de instabilidade.
  - Payload enviado para `POST /api/service-requests` usa `category` numerico (ex.: `4` para `Appliances`) e nao string literal.
  - Quando `zip-resolution` retorna sucesso, request final usa `street/city/lat/lng` resolvidos; quando falha, mantém fallback do estado da triagem.
- Validacao funcional manual ST-008 task 1:
  - `GET /api/telegram-chatbot/service-requests/{serviceRequestId}/eligible-providers` retorna apenas prestadores elegiveis para o `ClientId` autenticado.
  - Resultado vem ordenado por distancia e limitado pelo parametro `take` (1..10).
- Validacao funcional manual ST-008 task 2:
  - `POST /api/telegram-chatbot/service-requests/{serviceRequestId}/schedule-visits-batch` aceita no maximo 3 visitas por solicitacao.
  - Requisicoes com visitas no mesmo dia retornam `duplicate_visit_day`.
  - Conflitos de slot/indisponibilidade retornam erro por item no resultado do lote.
- Validacao automatizada ST-008 task 3:
  - `TelegramSchedulingNaturalLanguageParserTests` cobre interpretacao de "quarta e sexta de manha", erro sem periodo, erro por dias insuficientes e bypass para mensagem sem intencao de agenda.
- Validacao documental ST-008 tasks 8 e 9:
  - Publicados diagramas Mermaid de fluxo e sequencia da ST-008 cobrindo matching de prestadores e agendamento em lote multi-visitas.
- Validacao automatizada ST-008 tasks 5 e 6:
  - `TelegramChatbotOrchestratorTests` valida fluxo completo de agenda natural (matching + parser + batch), incluindo persistencia de trilha e resposta final de agendamento no chat.
- Validacao automatizada ST-008 hotfix (loop de resposta):
  - `TelegramServiceRequestTriageEngineTests` garante que snapshot com `serviceRequestId` nao forza triagem quando a intent nao e de abertura.
  - `TelegramChatbotOrchestratorTests` garante resposta de status/agendamento com listagem de prestadores quando cliente pergunta "ja foi agendado?".
- Validacao documental ST-007 task 8:
  - Publicado diagrama Mermaid de fluxo da triagem natural com abertura automatica de pedido e persistencia de trilha.
- Validacao documental ST-007 task 9:
  - Publicado diagrama Mermaid de sequencia da triagem com criacao automatica de pedido e persistencia de snapshots/acoes.
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
  - `dotnet test Backend/tests/ConsertaPraMim.Tests.Unit/ConsertaPraMim.Tests.Unit.csproj --filter "FullyQualifiedName~TelegramBridge"`
  - `dotnet test Backend/tests/ConsertaPraMim.Tests.Unit/ConsertaPraMim.Tests.Unit.csproj --filter "FullyQualifiedName~Telegram"`
  - Cobertura criada para:
    - servico `TelegramChatbotConversationService` (UTC, validacao de payload e isolamento por cliente);
    - controller `TelegramChatbotController` com SQLite (autorizacao por role Client, acesso cruzado e persistencia UTC).
    - controller `AccountController` da bridge (login valido com criacao de sessao e erro de credencial);
    - validacao de autorizacao por atributos em `HomeController`, `ChatApiController`, `TelegramChatHub` e `AllowAnonymous` no login.
    - parser de resposta estruturada da IA (`TelegramAiResponseParserTests`);
    - fallback e cache do orquestrador da bridge (`TelegramChatbotOrchestratorTests`).
    - regras de completude, merge de contexto e mapeamento da triagem para abertura de pedido (`TelegramServiceRequestTriageEngineTests`);
    - integracao da criacao automatica de pedido dentro do orquestrador (`TelegramChatbotOrchestratorTests`).

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
- Se o browser mostrar `ERR_TOO_MANY_REDIRECTS` em `/api/chats/*`, validar se a build em execucao ja possui retorno `401/403` sem redirect para `/Account/Login` em rotas `/api` e `/hubs`.

### 5.3 Chatbot pede CEP novamente mesmo com CEP valido

- Verificar logs do `TelegramChatbotApiClient` para status/erro de `POST /api/service-requests` e `GET /api/service-requests/zip-resolution`.
- Confirmar que o payload de criacao esta enviando `category` numerico (nao string), conforme contrato `CreateServiceRequestDto`.
- Validar conectividade externa dos provedores de geocodificacao (BrasilAPI, AwesomeAPI, ViaCEP e Nominatim) usados pelo backend.

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
- 2026-03-03: atualizacao com integracao de login da bridge no endpoint oficial da API na ST-005 (Task 2).
- 2026-03-03: atualizacao com persistencia segura de sessao por cookie no Telegram Bridge da ST-005 (Task 3).
- 2026-03-03: atualizacao com protecao de rotas de chat por autenticacao no Telegram Bridge da ST-005 (Task 4).
- 2026-03-03: atualizacao com vinculo de `ClientId` da sessao aos calls da API do chatbot na ST-005 (Task 5).
- 2026-03-03: atualizacao com fluxo de logout e limpeza de sessao no Telegram Bridge da ST-005 (Task 6).
- 2026-03-03: atualizacao com conversa unica automatica por cliente no login da ST-005 (sem input manual de `chatId`).
- 2026-03-03: atualizacao com serializacao segura de `chatId` (string) e retorno `401/403` sem loop de redirect nas rotas `/api` e `/hubs`.
- 2026-03-03: atualizacao com testes unitarios da ST-005 para login/autorizacao basica no Telegram Bridge (Task 7).
- 2026-03-03: atualizacao com diagrama Mermaid de fluxo da ST-005 (Task 8).
- 2026-03-03: atualizacao com diagrama Mermaid de sequencia da ST-005 e indices de diagramas/board (Task 9).
- 2026-03-03: atualizacao da ST-006 (Task 1) com gateway OpenAI resiliente (timeout, retries e tratamento de erro) e opcoes de configuracao de IA na bridge.
- 2026-03-03: atualizacao da ST-006 (Tasks 2 a 8) com orquestrador IA integrado ao fluxo do chat, contexto historico, saida estruturada, fallback/cache, observabilidade e testes unitarios de parser/orquestrador.
- 2026-03-03: atualizacao da ST-006 (Task 9) com diagrama Mermaid de fluxo da orquestracao OpenAI e atualizacao de indices da trilha.
- 2026-03-03: atualizacao da ST-006 (Task 10) com diagrama Mermaid de sequencia da orquestracao OpenAI e encerramento da story em `DONE`.
- 2026-03-03: atualizacao da ST-007 (Tasks 1 a 6) com contrato de intent `open_service_request`, state machine de triagem, validacao de dados minimos, criacao automatica de pedido via API e persistencia da trilha de abertura no historico conversacional.
- 2026-03-03: atualizacao da ST-007 (Task 7) com testes unitarios da engine de triagem e cenarios de criacao automatica de pedido no orquestrador.
- 2026-03-03: atualizacao da ST-007 (Task 8) com diagrama Mermaid de fluxo da triagem e abertura automatica de pedido.
- 2026-03-03: atualizacao da ST-007 (Task 9) com diagrama Mermaid de sequencia da triagem, encerramento da story em `DONE` e atualizacao dos indices da trilha.
- 2026-03-03: hotfix ST-007 aplicado para compatibilizar `category` numerico no payload de criacao de pedido e adicionar pre-resolucao de CEP via `zip-resolution` no Telegram Bridge.
- 2026-03-03: inicio da ST-008 com servico/endpoint de matching de prestadores elegiveis por pedido no dominio `TelegramChatbot`.
- 2026-03-03: evolucao da ST-008 com endpoint/servico de agendamento em lote (ate 3 visitas), validacao de dias distintos e retorno consolidado por visita.
- 2026-03-03: atualizacao da ST-008 com diagramas Mermaid de fluxo/sequencia, validacao documental e atualizacao de indices da trilha.
- 2026-03-03: evolucao da ST-008 com parser de janela temporal em linguagem natural (dias/periodos/horarios) na bridge e cobertura de testes unitarios dedicada.
- 2026-03-03: evolucao da ST-008 com integracao do orquestrador ao matching/agendamento em lote, persistencia de sugestoes/decisoes no historico conversacional e mensagens naturais de confirmacao/replanejamento.
- 2026-03-03: encerramento da ST-008 com move da story para `DONE` e atualizacao do board da trilha realtime.
- 2026-03-03: hotfix ST-008 para remover loop de resposta "pedido ja registrado" apos criacao do pedido; triagem nao intercepta mais mensagens gerais com pedido ja aberto e o orquestrador passou a responder consultas de status/agendamento/prestadores com base no contexto historico do pedido.
