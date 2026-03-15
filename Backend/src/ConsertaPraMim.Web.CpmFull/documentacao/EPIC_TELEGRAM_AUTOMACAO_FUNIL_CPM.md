# EPIC-TELEGRAM-001 - Automacao do Bot Telegram com Funis CPM e Chatwoot

## 1. Metadados da EPIC
- Epic ID: `EPIC-TELEGRAM-001`
- Produto: `ConsertaPraMim`
- Data de criacao: `2026-03-14`
- Prioridade: `Alta`
- Status atual: `Completed`
- Time alvo: `Backend`, `API`, `Frontend/TelegramBridge`, `Dados`, `DevOps`, `QA`
- Objetivo macro: conectar o bot Telegram ao funil do CPM Full e as inboxes `CPM Clientes` e `CPM Prestadores` do Chatwoot, mantendo o CPM como sistema de verdade do atendimento.

## 2. Contexto atual
- O `ConsertaPraMim.Web.TelegramBridge` ja abre/retoma sessoes conversacionais no canal `telegram`.
- A trilha atual persiste conversas, mensagens, snapshots e action logs em:
- `ChatbotConversations`
- `ChatbotMessages`
- `ChatbotContextSnapshots`
- `ChatbotActionLogs`
- O `TelegramChatbotController` agora aceita trilha conversacional para `Client` e `Provider`, mantendo endpoints operacionais de pedidos/agenda restritos a `Client`.
- O bridge ja consegue criar `service requests`, criar/atualizar lead no funil CPM Full e espelhar mensagens bidirecionais para o Chatwoot quando a automacao esta habilitada.
- A integracao Chatwoot do CPM Full ja alimenta automaticamente as inboxes `CPM Clientes` e `CPM Prestadores` a partir do lead do Kanban, mantendo o funil como sistema de verdade.

## 3. Objetivos de negocio
1. Transformar sessoes qualificadas do bot Telegram em leads operacionais no CPM Full.
2. Direcionar automaticamente cada atendimento para a inbox correta do Chatwoot (`clientes` ou `prestadores`).
3. Preservar rastreabilidade ponta a ponta entre sessao Telegram, lead no funil e conversa humana no Chatwoot.
4. Permitir handoff do bot para humano sem perda de contexto, historico e canal de origem.
5. Padronizar o canal `Telegram` como origem oficial do lead no ecossistema CPM.

## 4. Escopo

## 4.1 Em escopo
1. Criacao e atualizacao de lead do CPM Full a partir da sessao do bot Telegram.
2. Definicao do board correto (`clientes` ou `prestadores`) com base no contexto/intencao do bot.
3. Projecao do canal de origem `Telegram` no lead e no Chatwoot.
4. Reaproveitamento da integracao atual do Chatwoot para abrir contato e conversa nas inboxes corretas.
5. Espelhamento de mensagens relevantes entre Telegram e Chatwoot.
6. Vinculo tecnico entre `ChatbotConversation`, lead do CPM e `ChatwootConversationId`.
7. Observabilidade, diagnostico, retentativa e homologacao da trilha.

## 4.2 Fora de escopo (nesta EPIC)
1. Substituir o bot atual por um novo motor conversacional.
2. Integrar canais adicionais alem de Telegram.
3. Fazer broadcast outbound em massa via Telegram.
4. Bypassar o CPM Full e escrever direto no Chatwoot como sistema de verdade.
5. Refatorar integralmente o fluxo atual de `service request` do bridge sem necessidade funcional.

## 5. Diretrizes tecnicas

## 5.1 Modelo de integracao recomendado
1. O bot Telegram continua operando sobre `ChatbotConversations` como trilha conversacional de origem.
2. O CPM Full continua sendo o sistema de verdade do funil operacional.
3. O Chatwoot continua sendo a camada de atendimento humano e conversa.
4. A integracao deve criar/atualizar lead no CPM Full primeiro; so depois a trilha atual do Chatwoot deve ser acionada.
5. O vinculo tecnico minimo precisa cobrir:
- `ChatbotConversationId`
- `ChannelConversationId`
- `LeadId`
- `BoardType`
- `ChatwootContactId`
- `ChatwootConversationId`
- `ChatwootInboxId`

## 5.2 Regras de roteamento iniciais
1. Conversas classificadas como solicitacao de servico, acompanhamento de pedido, duvida operacional do cliente ou suporte de atendimento entram no board `clientes`.
2. Conversas classificadas como cadastro de prestador, envio de documentacao, validacao tecnica, reativacao ou suporte do prestador entram no board `prestadores`.
3. O canal/origem projetado no lead e no Chatwoot deve ser `Telegram`.
4. A criacao direta de conversa no Chatwoot nao deve acontecer sem lead correspondente no CPM Full.

## 5.3 Mapeamento inicial sugerido
1. Fluxos `clientes` no Telegram:
- `abrir_pedido`
- `acompanhar_pedido`
- `suporte_cliente`
- `agendamento_cliente`
2. Fluxos `prestadores` no Telegram:
- `quero_ser_prestador`
- `documentacao_prestador`
- `validacao_prestador`
- `reativacao_prestador`
3. O mapeamento definitivo deve considerar intencao, passo atual da conversa e eventual acao explicita do operador no bridge.

## 6. Alteracoes de dados previstas

## 6.1 Alteracoes em `cpm_web_kanban_leads`
1. Adicionar `TelegramChatbotConversationId UNIQUEIDENTIFIER NULL`
2. Adicionar `TelegramChannelConversationId NVARCHAR(128) NULL`
3. Adicionar `TelegramSourceSlug NVARCHAR(32) NULL`
4. Criar indice para busca por `TelegramChatbotConversationId`

## 6.2 Novas tabelas
1. `cpm_web_telegram_funil_links`
- Id (PK)
- ChatbotConversationId (unique)
- LeadId
- BoardType
- TelegramChatId
- TelegramUserId
- ChatwootConversationId
- LastInboundAtUtc
- LastOutboundAtUtc
- HandoffStatus
- CreatedAt
- UpdatedAt
2. `cpm_web_telegram_delivery_queue`
- Id (PK)
- ChatbotConversationId
- LeadId
- Direction (`telegram_to_chatwoot`, `chatwoot_to_telegram`)
- PayloadJson
- AttemptCount
- NextAttemptAt
- LastError
- CreatedAt
- ProcessedAt

## 6.3 Configuracoes
1. Reaproveitar a secao `Chatwoot` do CPM Full.
2. Acrescentar secao `TelegramAutomation` no CPM Full e no bridge.
3. Campos minimos sugeridos:
- `Enabled`
- `ClientsAutomationEnabled`
- `ProvidersAutomationEnabled`
- `MirrorMessagesEnabled`
- `RequireHumanHandoffForOutbound`
- `TelegramBridgeBaseUrl`
- `CpmFullBaseUrl`
- `AllowedBotSources`
- `SharedSecret`
- `RequestTimeoutSeconds`
- `DeliveryWorkerEnabled`
- `DeliveryWorkerIntervalSeconds`
- `DeliveryWorkerBatchSize`
- `DeliveryQueueMaxAttempts`
- `WebhookSecret` (quando o bot usar webhook)

## 7. Historias e tasks detalhadas

## US-01 - Configuracao base e contrato de automacao Telegram
### Descricao
Como equipe tecnica, queremos definir configuracao, feature flags e contrato minimo da ponte Telegram -> CPM Full -> Chatwoot.

### Status
- Concluida em `2026-03-14` na primeira fatia do epic.

### Criterios de aceite
1. Existe secao de configuracao validada para a automacao Telegram.
2. O bridge e a API conseguem identificar claramente quando a automacao esta habilitada ou desligada.
3. O modo desligado nao quebra o fluxo atual do bot nem do `service request`.

### Tasks
- `TASK-01.01` Criar `TelegramAutomationOptions` com validacao de campos obrigatorios.
- `TASK-01.02` Definir feature flags independentes para `clientes`, `prestadores` e espelhamento de mensagens.
- `TASK-01.03` Mapear pontos de interceptacao no bridge e na API.
- `TASK-01.04` Documentar variaveis de ambiente, secrets e health checks da trilha.
- `TASK-01.05` Garantir fallback seguro quando a automacao estiver desligada.

### Entrega aplicada
1. `ConsertaPraMim.Web.TelegramBridge` e `ConsertaPraMim.Web.CpmFull` passaram a expor secao `TelegramAutomation` com `Enabled`, `ClientsAutomationEnabled`, `ProvidersAutomationEnabled`, `MirrorMessagesEnabled`, `RequireHumanHandoffForOutbound`, `AllowedBotSources` e segredo compartilhado.
2. O bridge ganhou cliente HTTP tipado para a automacao interna do CPM Full, usando `CpmFullBaseUrl`, timeout configuravel e header `X-Telegram-Automation-Key`.
3. O CPM Full ganhou endpoint interno `POST /api/integrations/telegram/automation/lead`, escondido do Swagger via `ApiExplorerSettings(IgnoreApi = true)` e protegido por segredo compartilhado.
4. Quando a automacao esta desligada, o bot continua criando `service request` e respondendo ao usuario sem bloquear o fluxo principal.

## US-02 - Criar/atualizar lead de clientes a partir do bot Telegram
### Descricao
Como operacao, queremos que conversas Telegram de clientes gerem lead no funil `clientes` sem cadastro manual.

### Status
- Concluida em `2026-03-14` na primeira fatia do epic.

### Criterios de aceite
1. Conversa de cliente qualificada gera lead no board `clientes`.
2. O lead preserva nome, telefone, contexto e origem `Telegram`.
3. Conversa repetida do mesmo chat reaproveita lead ja vinculado quando aplicavel.

### Tasks
- `TASK-02.01` Definir regra de deduplicacao por `ChatbotConversationId`, telefone e cliente autenticado.
- `TASK-02.02` Criar adaptador para abrir lead no CPM Full a partir do bridge/API.
- `TASK-02.03` Projetar dados minimos do bot no lead (`nome`, `telefone`, `observacao inicial`, `fonte`).
- `TASK-02.04` Registrar historico funcional `lead_criado_via_telegram`.
- `TASK-02.05` Cobrir cenarios de reentrada e conversa duplicada.

### Entrega aplicada
1. O `TelegramChatbotOrchestrator` passou a disparar a automacao do funil apos `service_request_created` e tambem em reentrada com `ServiceRequestId` ja existente no contexto.
2. O CPM Full agora cria ou atualiza lead do board `clientes` via `UpsertTelegramLead`, com idempotencia por `ChatbotConversationId`.
3. O vinculo tecnico inicial ficou persistido em `dbo.cpm_web_telegram_funil_links`, com `ChatbotConversationId`, `LeadId`, `BoardType`, `ChannelConversationId`, `TelegramChatId`, `ClientId`, `ClientEmail` e `ServiceRequestId`.
4. O lead criado/atualizado projeta `Source = Telegram`, `StatusNote`, `InternalNotes` e `LastContactAt`, enquanto o historico registra eventos PT-BR `telegram_lead_criado` e `telegram_lead_atualizado`.
5. A sincronizacao atual do Chatwoot foi reaproveitada logo apos o upsert do lead, sem abrir uma trilha paralela fora do funil do CPM Full.
6. Nesta primeira fatia, o `telefone` ainda nao e preenchido automaticamente porque o contrato atual do bridge autenticado nao expoe telefone do cliente; esse enriquecimento permanece para evolucao posterior da trilha.

## US-03 - Criar/atualizar lead de prestadores a partir do bot Telegram
### Descricao
Como operacao, queremos que fluxos do bot voltados a prestadores entrem automaticamente no funil `prestadores`.

### Status
- Concluida em `2026-03-14` com ampliacao do bridge/API para `Provider` e automacao inicial do board `prestadores`.

### Criterios de aceite
1. Conversa de prestador qualificada gera lead no board `prestadores`.
2. O fluxo respeita feature flag separada de `clientes`.
3. O bridge nao depende do controller exclusivo de `Client` para fluxos de prestador.

### Tasks
- `TASK-03.01` Definir intencoes/passos do bot que roteiam para `prestadores`.
- `TASK-03.02` Abrir contrato tecnico para suportar prestador no bridge/API.
- `TASK-03.03` Criar lead no board `prestadores` com labels e historico operacionais.
- `TASK-03.04` Cobrir erros de classificacao e fallback manual.
- `TASK-03.05` Adicionar testes para fluxo de `clientes` x `prestadores`.

### Entrega aplicada
1. O `ConsertaPraMim.Web.TelegramBridge` passou a aceitar login de `Provider`, preservando a mesma sessao autenticada do bridge para cliente ou prestador.
2. O `TelegramChatbotController` da API agora aceita os endpoints de sessao, mensagens, snapshots, actions, estado e historico para `Client` e `Provider`, mantendo endpoints de pedidos/agendamentos como `client-only`.
3. O `TelegramChatbotOrchestrator` ganhou contexto explicito de papel autenticado e passou a tratar `Provider` como fluxo proprio, com guardrail para nao abrir pedido de cliente nem consultar carteira/agendamento.
4. Conversas autenticadas como `Provider` passam a criar ou atualizar lead no board `prestadores` do CPM Full usando `ProvidersAutomationEnabled`, com idempotencia por `ChatbotConversationId`.
5. A automacao de `prestadores` registra `Source = Telegram`, contexto operacional do prestador, `UserId`/`UserEmail` autenticados e segue reaproveitando a sincronizacao ja existente do Chatwoot a partir do lead do funil.

## US-04 - Vinculo tecnico bot Telegram <-> lead <-> Chatwoot
### Descricao
Como sistema, quero rastrear com seguranca qual sessao do bot originou qual lead e qual conversa humana.

### Status
- Concluida em `2026-03-14` com exposicao operacional do vinculo no admin do CPM Full.

### Criterios de aceite
1. Existe vinculo tecnico persistido entre `ChatbotConversation`, lead e Chatwoot.
2. O vinculo pode ser consultado para reprocessamento, suporte e auditoria.
3. O lead exibe que a origem foi `Telegram`.

### Tasks
- `TASK-04.01` Criar modelo/tabela de vinculo operacional.
- `TASK-04.02` Persistir `ChatbotConversationId` e `ChannelConversationId` no lead ou tabela de ligacao.
- `TASK-04.03` Projetar `Source = Telegram` no CPM Full.
- `TASK-04.04` Expor o vinculo no admin para suporte operacional.
- `TASK-04.05` Criar testes de leitura/escrita do vinculo.

### Entrega aplicada
1. O detalhe do lead no Kanban passou a carregar o vinculo mais recente salvo em `dbo.cpm_web_telegram_funil_links`, incluindo `ChatbotConversationId`, `ChannelConversationId`, `TelegramChatId`, `ClientId`, `ClientEmail`, `ServiceRequestId` e `UpdatedAt`.
2. O endpoint `GET /admin/funil/lead/{id}/json` do CPM Full agora expoe um bloco `telegram` dedicado para auditoria e suporte operacional.
3. O modal de detalhes do lead ganhou a secao `Vinculo Telegram`, exibindo origem automatizada, IDs tecnicos da conversa, vinculo com cliente autenticado e pedido associado.
4. O comportamento de `Source = Telegram` continuou preservado no detalhe do lead, reforcando a rastreabilidade entre bot, funil e Chatwoot.
5. Foi adicionada cobertura de regressao para validar leitura e persistencia do vinculo Telegram no `SqlAdminKanbanService`.

## US-05 - Alimentar a inbox correta do Chatwoot via lead Telegram
### Descricao
Como atendente, quero que o lead originado no Telegram abra a conversa humana no inbox correto do Chatwoot.

### Status
- Concluida em `2026-03-14` com bootstrap dedicado de leads Telegram no Chatwoot, reaproveitando inbox correta por board e registrando historico operacional proprio.

### Criterios de aceite
1. Lead Telegram de clientes abre/reaproveita conversa em `CPM Clientes`.
2. Lead Telegram de prestadores abre/reaproveita conversa em `CPM Prestadores`.
3. O contato e a conversa no Chatwoot mostram `Canal de Origem = Telegram`.

### Tasks
- `TASK-05.01` Reaproveitar o `ChatwootLeadSyncService` do CPM Full.
- `TASK-05.02` Garantir `cpm_lead_source = Telegram` e `cpm_lead_source_slug = telegram`.
- `TASK-05.03` Registrar historico `chatwoot_bootstrap_via_telegram`.
- `TASK-05.04` Validar deduplicacao entre conversa Telegram existente e conversa Chatwoot ja aberta.
- `TASK-05.05` Cobrir cenarios de reprocessamento manual.

### Entrega aplicada
1. O fluxo `Telegram -> CPM Full -> Chatwoot` continuou reaproveitando o `ChatwootLeadSyncService`, sem abrir uma trilha paralela fora do funil.
2. Leads Telegram de `clientes` seguem resolvendo `ClientsInboxId`, enquanto leads Telegram de `prestadores` passam a resolver `ProvidersInboxId` via board do funil.
3. O contato e a conversa do Chatwoot continuam recebendo `cpm_lead_source = Telegram` e `cpm_lead_source_slug = telegram` a partir do `Source = Telegram` persistido no lead do CPM Full.
4. Quando um lead Telegram ainda nao possui `ChatwootConversationId`, o bootstrap agora registra historico funcional `chatwoot_bootstrap_via_telegram`, indicando se a conversa foi criada ou reaproveitada no inbox correto.
5. A cobertura de regressao passou a validar inbox correta para `prestadores`, canal `Telegram` em contato/conversa e reaproveitamento de conversa existente sem duplicar atendimento no Chatwoot.

## US-06 - Espelhar mensagens Telegram -> Chatwoot
### Descricao
Como operacao, quero que mensagens relevantes do Telegram aparecam na conversa humana do Chatwoot sem copiar e colar manualmente.

### Status
- Concluida em `2026-03-14` com fila dedicada `telegram_to_chatwoot`, idempotencia por `ChannelMessageId` e historico operacional no funil.

### Criterios de aceite
1. Mensagem do Telegram elegivel vira nota/mensagem na conversa correta do Chatwoot.
2. Eventos duplicados nao geram mensagens repetidas.
3. O historico do lead indica que houve espelhamento.

### Tasks
- `TASK-06.01` Definir quais mensagens do bot/cliente devem ser espelhadas.
- `TASK-06.02` Criar adaptador `TelegramToChatwootMessageSync`.
- `TASK-06.03` Implementar idempotencia por `ChannelMessageId`.
- `TASK-06.04` Registrar historico `telegram_message_synced_to_chatwoot`.
- `TASK-06.05` Cobrir falha externa sem quebrar o bot.

### Entrega aplicada
1. O `ConsertaPraMim.Web.TelegramBridge` passou a espelhar mensagens reais recebidas do Telegram para o CPM Full por `POST /api/integrations/telegram/automation/message`, preservando `ChatbotConversationId`, `ChannelConversationId`, `TelegramChatId` e `ChannelMessageId`.
2. O CPM Full ganhou a fila `telegram_to_chatwoot` em `dbo.cpm_web_telegram_delivery_queue`, com deduplicacao por `Direction + DeliveryKey`, historico `telegram_entrega_enfileirada` e reprocessamento pelo worker dedicado.
3. A entrega efetiva ao Chatwoot passou a criar mensagem `incoming` na conversa humana correta, reaproveitando o bootstrap existente quando o lead ainda nao possuia `ChatwootConversationId`.
4. O vinculo Telegram do lead agora registra `LastTelegramMessageSyncedAt`, e o historico funcional registra `telegram_message_synced_to_chatwoot` quando a entrega conclui com sucesso.
5. Falhas externas nao quebram o bot: a mensagem fica enfileirada para retentativa e o erro operacional fica rastreavel na fila local do CPM Full.

## US-07 - Espelhar handoff e mensagens humanas do Chatwoot -> Telegram
### Descricao
Como usuario final, quero continuar no Telegram mesmo quando um humano assumir o atendimento no Chatwoot.

### Status
- Concluida em `2026-03-14` com fila `chatwoot_to_telegram`, handoff humano rastreado no lead e entrega outbound pelo bridge interno protegido.

### Criterios de aceite
1. O sistema consegue identificar quando a resposta humana deve voltar ao Telegram.
2. O handoff humano e rastreado no CPM Full.
3. O operador nao perde o contexto do que ja foi tratado pelo bot.

### Tasks
- `TASK-07.01` Definir regra de handoff humano e permissao de outbound.
- `TASK-07.02` Mapear webhook/evento do Chatwoot que deve gerar envio ao Telegram.
- `TASK-07.03` Implementar fila de entrega `chatwoot_to_telegram`.
- `TASK-07.04` Registrar historico `chatwoot_message_synced_to_telegram`.
- `TASK-07.05` Tratar janelas em que o bot deve silenciar respostas automaticas.

### Entrega aplicada
1. O webhook inbound do Chatwoot passou a identificar mensagens humanas publicas elegiveis e a enfileirar entregas `chatwoot_to_telegram` usando `message_id` como chave principal de idempotencia.
2. O CPM Full agora chama o bridge por `POST /api/internal/telegram/messages/send`, protegido por `X-Telegram-Automation-Key`, para entregar a resposta humana ao chat correto no Telegram.
3. O primeiro outbound humano da conversa passa a registrar `HumanHandoffStartedAt` no vinculo Telegram do lead, alem de `LastChatwootMessageSyncedAt`.
4. O historico funcional do lead passou a registrar `chatwoot_handoff_humano_iniciado` e `chatwoot_message_synced_to_telegram`, mantendo rastreabilidade do takeover humano no CPM Full.
5. O bridge ganhou estado local de handoff por `chatId`; quando esse estado esta ativo e a trilha web passa pelo `ChatApiController`, respostas automaticas do assistente deixam de ser emitidas para aquele chat.

## US-08 - Observabilidade, fila e diagnostico operacional
### Descricao
Como time tecnico, queremos diagnosticar rapidamente falhas entre bot, CPM Full e Chatwoot.

### Status
- Concluida em `2026-03-14` com correlation id propagado, drawer operacional no Kanban e diagnostico interno do Telegram Bridge.

### Criterios de aceite
1. A trilha possui correlation id fim a fim.
2. Falhas ficam visiveis no admin e reprocessaveis.
3. Existe visao de fila para entregas Telegram -> Chatwoot e Chatwoot -> Telegram.

### Tasks
- `TASK-08.01` Padronizar logs estruturados na ponte Telegram/CPM/Chatwoot.
- `TASK-08.02` Criar fila de retentativa para entregas bidirecionais.
- `TASK-08.03` Expor diagnostico no admin do funil.
- `TASK-08.04` Criar acoes rapidas de reprocessamento por conversa/lead.
- `TASK-08.05` Adicionar metricas de volume, falha e latencia.

### Entrega aplicada
1. O `ConsertaPraMim.Web.TelegramBridge` passou a propagar `X-Correlation-ID` para o CPM Full nas automacoes de lead e mensagem, enquanto o CPM Full passou a propagar o mesmo header no caminho de entrega humana de volta ao bridge.
2. O bridge ganhou o endpoint interno `GET /api/internal/telegram/observability/dashboard`, protegido pelo mesmo `SharedSecret` da automacao, expondo snapshot operacional de volume, falha e latencia para consumo interno do CPM Full.
3. O CPM Full passou a agregar diagnostico local da tabela `dbo.cpm_web_telegram_delivery_queue` com metricas por board, fila ativa, dead-letter, handoff e espelhamento inbound/outbound.
4. O Kanban agora possui o drawer `Diagnostico Telegram`, com resumo operacional, metricas do Telegram Bridge, falhas recentes, fila/dead-letter, incidentes do bot e acoes rapidas de `Ver lead`, `Reprocessar` e `Abrir no Chatwoot`.
5. A retentativa manual agora pode ser disparada por item da fila Telegram via `POST /admin/funil/telegram/fila/{queueItemId}/retentativa`, reaproveitando o worker bidirecional sem SQL manual.
6. A cobertura de regressao passou a validar o snapshot SQL do diagnostico Telegram e a retentativa manual de item `dead_letter` para `retrying`.

## US-09 - Seguranca e conformidade da trilha Telegram
### Descricao
Como time de seguranca, queremos proteger dados e segredos do bot e do Chatwoot.

### Status
- Concluida em `2026-03-14` com mascaramento de PII/segredos, retention controlada de payloads/anexos e endurecimento operacional dos endpoints internos da trilha Telegram.

### Criterios de aceite
1. Telefone, chat id, token e dados sensiveis aparecem mascarados em logs e telas tecnicas.
2. Webhook do bot, quando existir, valida origem/segredo.
3. Payloads sensiveis possuem retention controlada.

### Tasks
- `TASK-09.01` Mascarar PII e segredos na trilha Telegram.
- `TASK-09.02` Validar origem/segredo do webhook do bot.
- `TASK-09.03` Revisar retention de payloads e anexos.
- `TASK-09.04` Publicar runbook de rotacao de token do bot.
- `TASK-09.05` Revisar politica de permissao para outbound humano.

### Entrega aplicada
1. O CPM Full e o Telegram Bridge ganharam `TelegramSecuritySanitizer`, passando a mascarar chat id, telefone, e-mail, token, segredo e mensagens tecnicas sensiveis em logs, diagnosticos e telas administrativas.
2. O detalhe do lead e o drawer `Diagnostico Telegram` no Kanban agora exibem `TelegramChatId`, `ClientEmail` e erros operacionais somente em formato mascarado, sem expor PII bruta para suporte.
3. A trilha interna continua protegida por `X-Telegram-Automation-Key` nos endpoints bridge -> CPM Full e CPM Full -> bridge; a validacao de segredo/origem para webhook do Telegram passou a fazer parte da trilha suportada do `TelegramBridge`.
4. O CPM Full ganhou retention controlada para `dbo.cpm_web_telegram_delivery_queue`, com redacao automatica de `PayloadJson` antigo em itens `processed`/`dead_letter` e preenchimento de `PayloadPurgedAt`.
5. O Telegram Bridge ganhou retention controlada de anexos baixados em `wwwroot/uploads/telegram-bridge`, com worker periodico para remover arquivos fora da janela configurada e limpar diretorios vazios.
6. A politica de outbound humano ficou explicitada no manual: respostas do Chatwoot so retornam ao Telegram quando a mensagem e publica, a automacao bidirecional esta habilitada, o lead possui vinculo Telegram valido e o handoff humano foi permitido para a conversa.

## US-10 - QA, testes e homologacao
### Descricao
Como QA, queremos validar a automacao Telegram ponta a ponta antes de operar em producao.

### Status
- Concluida em `2026-03-14` com ampliacao da cobertura automatizada, checklist final de homologacao e plano de rollback por feature flags.

### Criterios de aceite
1. Casos felizes e de falha validados para `clientes` e `prestadores`.
2. Espelhamento de mensagem e handoff humano homologados.
3. Plano de rollback definido.

### Tasks
- `TASK-10.01` Criar testes unitarios da ponte Telegram -> lead -> Chatwoot.
- `TASK-10.02` Criar cenarios E2E com conversa Telegram real ou mockada.
- `TASK-10.03` Validar idempotencia de mensagens e handoff.
- `TASK-10.04` Validar retentativa com indisponibilidade simulada.
- `TASK-10.05` Criar checklist operacional de homologacao.
- `TASK-10.06` Criar plano de rollback e feature flags de desativacao.

### Entrega aplicada
1. A suite automatizada passou a cobrir servico de automacao de lead Telegram, fila bidirecional do worker Telegram e cenarios adicionais de idempotencia para outbound humano sem `ChatwootMessageId`.
2. A cobertura agora valida fluxo feliz e bloqueios da automacao de lead (`clientes` e `prestadores`), processamento do worker com sucesso, dead-letter quando retentativas esgotam e reaproveitamento da mesma delivery key em reenvio idempotente.
3. O manual operacional passou a consolidar checklist final de homologacao da trilha Telegram, incluindo `clientes`, `prestadores`, bootstrap Chatwoot, espelhamento inbound, handoff humano, drawer de diagnostico, seguranca e retention.
4. O runbook passou a documentar rollback operacional por feature flags, permitindo desligar seletivamente automacao de leads, espelhamento de mensagens e outbound humano sem interromper a trilha principal do bot.
5. Com a conclusao da US-10, o epic `EPIC-TELEGRAM-001` fica encerrado como entregue do ponto de vista funcional, tecnico e operacional.

## Pos-epico - ST-088 - Transporte webhook seguro no TelegramBridge
### Descricao
Como operacao, queremos que o `ConsertaPraMim.Web.TelegramBridge` possa receber mensagens do Telegram tambem por webhook seguro, mantendo fallback explicito para long polling.

### Status
- Concluida em `2026-03-14` como evolucao operacional pos-epic, preservando o epic principal como `Completed`.

### Criterios de aceite
1. O bridge aceita `LongPolling` e `Webhook` como modos inbound validos.
2. Em `Webhook`, o bridge registra `setWebhook` automaticamente, valida `X-Telegram-Bot-Api-Secret-Token` e reaproveita a mesma trilha de processamento/mirror.
3. Em `LongPolling`, o bootstrap remove webhook anterior e o worker de polling continua como fallback suportado.

### Tasks
- `TASK-11.01` Adicionar configuracao `TelegramBridge:UpdateTransport` e parametros do webhook.
- `TASK-11.02` Reaproveitar o mesmo pipeline de processamento inbound entre polling e webhook.
- `TASK-11.03` Registrar/remover webhook automaticamente na Bot API conforme o modo configurado.
- `TASK-11.04` Publicar endpoint interno do webhook com validacao de segredo.
- `TASK-11.05` Atualizar documentacao operacional e cobertura automatizada minima do bridge.

### Entrega aplicada
1. O `ConsertaPraMim.Web.TelegramBridge` passou a suportar `TelegramBridge:UpdateTransport=LongPolling|Webhook`, com validacao de `WebhookPublicBaseUrl`, `WebhookPath`, `WebhookSecretToken` e `WebhookDropPendingUpdates`.
2. O processamento inbound foi extraido para um servico compartilhado, permitindo que `long polling` e `webhook` passem exatamente pela mesma trilha de persistencia local, observabilidade e espelhamento para o CPM Full/Chatwoot.
3. O bridge ganhou o endpoint `POST /api/integrations/telegram/webhook`, protegido por `X-Telegram-Bot-Api-Secret-Token` e oculto do Swagger por se tratar de rota tecnica.
4. O bootstrap do transporte agora registra `setWebhook` automaticamente quando o modo `Webhook` esta ativo e remove webhook anterior quando o modo `LongPolling` esta configurado, evitando conflito entre `getUpdates` e entrega por webhook.
5. A documentacao operacional do CPM Full e do bridge passou a cobrir publicacao HTTPS do webhook, rotacao do secret token, troubleshooting do transporte e validacao do fallback.

## Pos-epico - ST-089 - Publicacao do TelegramBridge na VPS com webhook HTTPS
### Descricao
Como operacao, queremos publicar o `ConsertaPraMim.Web.TelegramBridge` como servico proprio da pipeline VPS, com healthcheck dedicado e URL HTTPS publica, para operar o modo `Webhook` sem etapa manual fora do workflow.

### Status
- Concluida em `2026-03-14` como evolucao operacional pos-epic, preservando o epic principal como `Completed`.

### Criterios de aceite
1. O workflow `deploy-vps` detecta mudancas do `TelegramBridge` e publica `web-telegrambridge`.
2. Existe compose e Dockerfile dedicados para o bridge na VPS.
3. O healthcheck da pipeline valida `GET /health` no bridge, com fallback coerente por branch.
4. A documentacao operacional cobre `PUBLIC_TELEGRAM_BRIDGE_URL`, `TELEGRAM_BRIDGE_*`, `TELEGRAM_AUTOMATION_*` e o subdominio HTTPS recomendado.

### Tasks
- `TASK-12.01` Adicionar `Dockerfile.web.telegrambridge` e `docker-compose.vps.web-telegrambridge.yml`.
- `TASK-12.02` Publicar job `deploy-web-telegrambridge` no workflow `deploy-vps`.
- `TASK-12.03` Adicionar job `health-web-telegrambridge` com fallback `dev-local` e loopback `main/master`.
- `TASK-12.04` Atualizar `vps-deploy.sh` e `vps-deploy-service.sh` para o novo servico.
- `TASK-12.05` Atualizar README, manual, runbook/story e changelog com a URL publica do bridge.

### Entrega aplicada
1. O workflow `.github/workflows/deploy-vps.yml` passou a detectar `Backend/src/ConsertaPraMim.Web.TelegramBridge/**`, `Backend/docker/vps/Dockerfile.web.telegrambridge` e `Backend/docker-compose.vps.web-telegrambridge.yml`, acionando `deploy-web-telegrambridge` e `health-web-telegrambridge`.
2. O deploy do bridge passou a gerar `Backend/.env.vps` com `PUBLIC_TELEGRAM_BRIDGE_URL`, `TELEGRAM_BRIDGE_*` e `TELEGRAM_AUTOMATION_*`, incluindo fallback de `WebhookPublicBaseUrl` para a propria URL publica quando necessario.
3. O `ConsertaPraMim.Web.TelegramBridge` passou a responder `GET /health` e a interpretar `X-Forwarded-*` por `ForwardedHeaders`, evitando redirecionamento HTTPS indevido atras do Nginx.
4. A stack VPS agora inclui compose e Dockerfile dedicados do bridge, e os scripts `scripts/deploy/vps-deploy.sh` / `scripts/deploy/vps-deploy-service.sh` passaram a aceitar `web-telegrambridge`.
5. A documentacao operacional foi atualizada para recomendar `https://telegram.consertapramim.com`, orientar `PUBLIC_TELEGRAM_BRIDGE_URL` por environment e validar o webhook seguro do Telegram em ambiente publicado.
6. Um hotfix pos-publicacao alinhou a imagem final do bridge para `.NET 8`, removendo o restart loop observado no `dev-local` quando o runtime final estava em `aspnet:9.0` para um app `net8.0`.

## Pos-epico - ST-090 - Ativacao operacional do bot Telegram no ambiente publicado

### Descricao
Como operacao, queremos ativar o bot Telegram no ambiente publicado com `LongPolling`, garantindo que bridge e CPM Full compartilhem a mesma configuracao `TelegramAutomation` e evitando disputa do mesmo bot entre `development` e `production`.

### Status
- Concluida em `2026-03-14` como ativacao operacional pos-epic.

### Criterios de aceite
1. O `web-cpmfull` publicado passa a consumir `TelegramAutomation__*` no compose da VPS, incluindo `TelegramBridgeBaseUrl`.
2. O workflow `deploy-vps` escreve `TELEGRAM_AUTOMATION_TELEGRAM_BRIDGE_BASE_URL` no `Backend/.env.vps`.
3. O bot publicado opera em `LongPolling` apenas no environment `production`, evitando dois consumidores do mesmo `BotToken`.
4. A documentacao operacional cobre o checklist de ativacao do bot e o troubleshooting de disputa de `getUpdates`.

### Tasks
- `TASK-13.01` Propagar `TELEGRAM_AUTOMATION_TELEGRAM_BRIDGE_BASE_URL` no workflow e no `.env.vps.example`.
- `TASK-13.02` Injetar `TelegramAutomation__*` no `Backend/docker-compose.vps.web-cpmfull.yml`.
- `TASK-13.03` Atualizar manual, runbook e changelog com a regra de um bot por environment em `LongPolling`.
- `TASK-13.04` Ativar os secrets do bot somente em `production`.

### Entrega aplicada
1. O workflow `.github/workflows/deploy-vps.yml` passou a derivar `TELEGRAM_AUTOMATION_TELEGRAM_BRIDGE_BASE_URL` com fallback interno para `http://<container-prefix>-telegrambridge:<porta>`.
2. O `Backend/docker-compose.vps.web-cpmfull.yml` passou a consumir `TelegramAutomation__Enabled`, `ClientsAutomationEnabled`, `ProvidersAutomationEnabled`, `MirrorMessagesEnabled`, `RequireHumanHandoffForOutbound`, `AllowedBotSources`, `TelegramBridgeBaseUrl`, `SharedSecret` e parametros de delivery/cleanup.
3. A ativacao operacional do bot foi planejada apenas para `production` enquanto o transporte estiver em `LongPolling`, evitando disputa do mesmo `BotToken` entre `development` e `production`.
4. O manual operacional passou a cobrir o checklist de ativacao real do bot, incluindo validacao do bridge, do CPM Full e da configuracao compartilhada de automacao.

## Pos-epico - ST-091 - Bootstrap publico da primeira mensagem do bot Telegram

### Descricao
Como operacao, queremos que a primeira mensagem recebida diretamente pelo bot publicado, sem login previo no painel web, abra automaticamente o lead tecnico no CPM Full e bootstrape a conversa humana no Chatwoot.

### Status
- Concluida em `2026-03-14` como correcao funcional pos-epic do fluxo publicado.

### Criterios de aceite
1. A primeira mensagem direta do bot publicado cria ou atualiza lead tecnico por `TelegramChatId`, sem depender de sessao autenticada no bridge.
2. O bootstrap usa `ChatbotConversationId` deterministico e permite que o espelhamento `Telegram -> CPM Full -> Chatwoot` aconteca na mesma conversa.
3. O board inicial e resolvido automaticamente entre `clientes` e `prestadores`.
4. O bot envia um ACK inicial ao usuario apenas quando o lead nasce e nao existe handoff humano ativo.

### Tasks
- `TASK-14.01` Bootstrapar lead tecnico no inbound do `TelegramBridge` antes do espelhamento da mensagem.
- `TASK-14.02` Gerar `ChatbotConversationId` e `UserId` deterministicos a partir do `TelegramChatId`.
- `TASK-14.03` Resolver board inicial por heuristica leve (`clientes` x `prestadores`) e enviar ACK simples ao usuario.
- `TASK-14.04` Cobrir a regressao com testes automatizados e atualizar manual/README/changelog/story.

### Entrega aplicada
1. O `TelegramInboundUpdateProcessor` passou a criar ou atualizar o lead tecnico no CPM Full antes do mirror, reutilizando `ITelegramLeadAutomationClient` mesmo quando a conversa nasce diretamente do bot publicado.
2. O bridge passou a gerar `ChatbotConversationId` e `UserId` deterministicos por `TelegramChatId`, evitando duplicidade entre mensagens sucessivas da mesma conversa.
3. O board inicial agora e resolvido automaticamente por heuristica simples de onboarding de prestador; mensagens comuns continuam entrando em `clientes`.
4. O mirror inbound passou a carregar `ChatbotConversationId` no payload, aumentando a chance de o CPM Full localizar corretamente o lead mesmo na primeira mensagem.
5. Quando o bootstrap cria o lead e ainda nao existe handoff humano ativo, o bot envia um ACK inicial ao usuario no Telegram para evitar a sensacao de silencio operacional.

## Pos-epico - ST-092 - Hotfix do deploy do CPM Full para ativar TelegramAutomation no ambiente publicado

### Descricao
Como operacao, queremos garantir que o `ConsertaPraMim.Web.CpmFull` publicado receba a mesma configuracao `TelegramAutomation` ja ativa no bridge, evitando o falso negativo em que o bot recebe o update real, mas o CPM Full responde `Automacao Telegram desabilitada no ambiente atual.`.

### Status
- Concluida em `2026-03-14` como correcao de deploy pos-epic no ambiente publicado.

### Criterios de aceite
1. O job `deploy-web-cpmfull` escreve todas as variaveis `TELEGRAM_AUTOMATION_*` exigidas pelo compose e pelo runtime do CPM Full.
2. O container publicado do `web-cpmfull` sobe com `TelegramAutomation__Enabled=true`, `MirrorMessagesEnabled=true` e `SharedSecret` preenchido quando os secrets do environment estiverem configurados.
3. O bridge publicado deixa de receber `409` do CPM Full ao bootstrapar lead e espelhar mensagem real do bot.
4. O troubleshooting operacional deixa explicito esse sintoma e a forma de validar o runtime do container.

### Tasks
- `TASK-15.01` Corrigir o bloco `Write VPS env file` do job `deploy-web-cpmfull`.
- `TASK-15.02` Recriar o container publicado do `web-cpmfull` com as envs de Telegram ativas.
- `TASK-15.03` Atualizar changelog, manual operacional e story de pos-epic com a causa raiz do `409`.

### Entrega aplicada
1. O workflow `.github/workflows/deploy-vps.yml` passou a propagar no job `deploy-web-cpmfull` os mesmos parametros `TELEGRAM_AUTOMATION_*` ja usados pelo compose do CPM Full.
2. O fallback de `TELEGRAM_AUTOMATION_TELEGRAM_BRIDGE_BASE_URL` ficou alinhado ao endereco interno `http://<container-prefix>-telegrambridge:<porta>`.
3. O hotfix operacional publicado recriou o `cpm-prd-cpmfull` com `TelegramAutomation` e espelhamento inbound realmente habilitados.
4. O manual passou a orientar a verificacao de `printenv` no container do CPM Full quando o bridge estiver recebendo updates, mas o lead nao aparecer no funil.

## Pos-epico - ST-093 - Bootstrap Chatwoot de lead Telegram sem telefone ou e-mail

### Descricao
Como operacao, queremos que o primeiro contato publico do Telegram sincronize no Chatwoot mesmo antes de o usuario compartilhar telefone ou e-mail, reaproveitando o identificador tecnico do bot como chave do contato.

### Status
- Concluida em `2026-03-15` como correcao funcional pos-epic para o fluxo publicado.

### Criterios de aceite
1. Lead `Source = Telegram` sem telefone/e-mail nao falha na sync se possuir `TelegramChatId`, `ChatbotConversationId` ou `ChannelConversationId`.
2. O contato do Chatwoot e criado ou reaproveitado com identificador tecnico deterministico do Telegram.
3. Leads nao-Telegram continuam exigindo telefone ou e-mail valido.
4. O dry-run/backfill reconhece leads Telegram sem contato manual como elegiveis quando houver vinculo tecnico suficiente.

### Tasks
- `TASK-16.01` Permitir fallback de identificador do contato no `ChatwootLeadSyncService` para leads Telegram sem telefone/e-mail.
- `TASK-16.02` Projetar metadados tecnicos do Telegram em `additional_attributes` do contato.
- `TASK-16.03` Ajustar dry-run/backfill e testes de regressao para o novo criterio de elegibilidade.
- `TASK-16.04` Atualizar manual, changelog, indice e story de suporte operacional.

### Entrega aplicada
1. O `ChatwootLeadSyncService` passou a aceitar leads `Source = Telegram` sem telefone/e-mail quando houver `TelegramChatId`, `ChatbotConversationId` ou `ChannelConversationId`, gerando identificador deterministico (`telegram:chat:*`, `telegram:conversation:*` ou `telegram:channel:*`).
2. O contato do Chatwoot agora recebe `additional_attributes` tecnicos do Telegram (`telegram_chat_id`, `telegram_chatbot_conversation_id`, `telegram_channel_conversation_id`) para rastreabilidade interna.
3. A anotacao privada de abertura da conversa passou a registrar explicitamente quando o primeiro contato via Telegram ainda nao informou telefone/e-mail.
4. O `ChatwootBackfillService` passou a tratar leads Telegram sem telefone/e-mail como elegiveis no `dry-run` quando o vinculo tecnico estiver preenchido, mantendo a regra antiga para outras origens.
5. A suite ganhou testes de regressao cobrindo a sincronizacao do lead Telegram sem telefone/e-mail e o dry-run do backfill com identificador tecnico do bot.

## Pos-epico - ST-094 - Ativacao publica do webhook do TelegramBridge

### Descricao
Como operacao, queremos tirar o bot publicado de `LongPolling` e operar em `Webhook` com host HTTPS dedicado, para reduzir dependencia de polling continuo e endurecer a borda publica do canal Telegram.

### Status
- Concluida em `2026-03-15` como ativacao operacional pos-epic na borda publicada.

### Criterios de aceite
1. `telegram.consertapramim.com` resolve para a VPS publicada e responde `GET /health` com TLS valido.
2. O `TelegramBridge` publicado passa a operar com `TelegramBridge:UpdateTransport=Webhook`.
3. A Bot API registra `setWebhook` para `https://telegram.consertapramim.com/api/integrations/telegram/webhook`.
4. O bot continua entregando o fluxo `Telegram -> CPM Full -> Chatwoot` apos a troca de transporte.

### Tasks
- `TASK-17.01` Ativar DNS e borda Nginx/TLS para `telegram.consertapramim.com`.
- `TASK-17.02` Ajustar os secrets publicados para `WebhookPublicBaseUrl`, `WebhookPath` e `WebhookSecretToken`.
- `TASK-17.03` Validar `getWebhookInfo`, healthcheck HTTPS e mensagem real apos a virada.
- `TASK-17.04` Atualizar changelog, manual operacional, indice e story de operacao.

### Entrega aplicada
1. O host `telegram.consertapramim.com` passou a responder na mesma VPS do ecossistema publicado, com proxy reverso para `127.0.0.1:5175`.
2. O certificado TLS do host foi emitido e o endpoint `https://telegram.consertapramim.com/health` ficou saudavel.
3. O environment `production` passou a usar `TelegramBridge:UpdateTransport=Webhook`, `WebhookPublicBaseUrl=https://telegram.consertapramim.com` e `WebhookSecretToken` dedicado.
4. A Bot API passou a devolver `getWebhookInfo.url = https://telegram.consertapramim.com/api/integrations/telegram/webhook`, confirmando o `setWebhook` do bridge publicado.
5. O transporte inbound do bot publicado saiu de `LongPolling` e passou a operar com `Webhook`, preservando a mesma trilha `Telegram -> CPM Full -> Chatwoot` ja homologada no pos-epico.

## 8. Sequencia de entrega recomendada
1. Sprint 1:
- US-01
- US-02
- US-04
2. Sprint 2:
- US-03
- US-05
- US-06
3. Sprint 3:
- US-07
- US-08
4. Sprint 4:
- US-09
- US-10

## 9. Dependencias externas
1. Bot Telegram publicado e com token operacional valido.
2. `ConsertaPraMim.Web.TelegramBridge` e `ConsertaPraMim.API` com acesso ao CPM Full.
3. Instancia Chatwoot ativa com inboxes `CPM Clientes` e `CPM Prestadores`.
4. Definicao final das intencoes do bot que abrem funil `clientes` e `prestadores`.

## 10. Riscos e mitigacoes
1. Risco: conversa do bot gerar leads duplicados.
- Mitigacao: deduplicacao por `ChatbotConversationId`, telefone e lead aberto no board.
2. Risco: bot e humano responderem ao mesmo tempo.
- Mitigacao: estado explicito de handoff e feature flag para outbound humano.
3. Risco: espelhamento gerar ruido ou duplicidade no Chatwoot.
- Mitigacao: idempotencia por `ChannelMessageId` e fila com replay controlado.
4. Risco: fluxo de prestador ficar preso no contrato atual exclusivo de `Client`.
- Mitigacao: abrir contrato separado ou ampliar o controller com papel/escopo apropriado antes da automacao.

## 11. Definicao de pronto (DoD) da EPIC
1. Conversa Telegram qualificada cria ou atualiza lead no board correto do CPM Full.
2. O lead abre ou reaproveita contato/conversa na inbox correta do Chatwoot.
3. O canal `Telegram` fica visivel como origem do lead e da conversa.
4. Existe vinculo tecnico entre sessao do bot, lead e conversa humana.
5. Mensagens e handoff possuem trilha auditavel com fila e retentativa.
6. Documentacao tecnica, operacional e de rollback publicada para suporte e operacao.
