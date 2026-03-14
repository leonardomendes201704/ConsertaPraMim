# EPIC-TELEGRAM-001 - Automacao do Bot Telegram com Funis CPM e Chatwoot

## 1. Metadados da EPIC
- Epic ID: `EPIC-TELEGRAM-001`
- Produto: `ConsertaPraMim`
- Data de criacao: `2026-03-14`
- Prioridade: `Alta`
- Status atual: `In progress`
- Time alvo: `Backend`, `API`, `Frontend/TelegramBridge`, `Dados`, `DevOps`, `QA`
- Objetivo macro: conectar o bot Telegram ao funil do CPM Full e as inboxes `CPM Clientes` e `CPM Prestadores` do Chatwoot, mantendo o CPM como sistema de verdade do atendimento.

## 2. Contexto atual
- O `ConsertaPraMim.Web.TelegramBridge` ja abre/retoma sessoes conversacionais no canal `telegram`.
- A trilha atual persiste conversas, mensagens, snapshots e action logs em:
- `ChatbotConversations`
- `ChatbotMessages`
- `ChatbotContextSnapshots`
- `ChatbotActionLogs`
- O `TelegramChatbotController` atual atende apenas o papel `Client`.
- O bridge ja consegue criar `service requests`, mas ainda nao cria lead no funil CPM Full.
- A integracao Chatwoot entregue no CPM Full parte do lead do Kanban; sem lead, nao existe inbox `CPM Clientes` ou `CPM Prestadores` alimentada automaticamente.

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
- `AllowedBotSources`
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
6. Nesta primeira fatia, o `telefone` ainda nao e preenchido automaticamente porque o contrato atual do bridge autenticado nao expõe telefone do cliente; esse enriquecimento permanece para evolucao posterior da trilha.

## US-03 - Criar/atualizar lead de prestadores a partir do bot Telegram
### Descricao
Como operacao, queremos que fluxos do bot voltados a prestadores entrem automaticamente no funil `prestadores`.

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

## US-04 - Vinculo tecnico bot Telegram <-> lead <-> Chatwoot
### Descricao
Como sistema, quero rastrear com seguranca qual sessao do bot originou qual lead e qual conversa humana.

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

## US-05 - Alimentar a inbox correta do Chatwoot via lead Telegram
### Descricao
Como atendente, quero que o lead originado no Telegram abra a conversa humana no inbox correto do Chatwoot.

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

## US-06 - Espelhar mensagens Telegram -> Chatwoot
### Descricao
Como operacao, quero que mensagens relevantes do Telegram aparecam na conversa humana do Chatwoot sem copiar e colar manualmente.

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

## US-07 - Espelhar handoff e mensagens humanas do Chatwoot -> Telegram
### Descricao
Como usuario final, quero continuar no Telegram mesmo quando um humano assumir o atendimento no Chatwoot.

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

## US-08 - Observabilidade, fila e diagnostico operacional
### Descricao
Como time tecnico, queremos diagnosticar rapidamente falhas entre bot, CPM Full e Chatwoot.

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

## US-09 - Seguranca e conformidade da trilha Telegram
### Descricao
Como time de seguranca, queremos proteger dados e segredos do bot e do Chatwoot.

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

## US-10 - QA, testes e homologacao
### Descricao
Como QA, queremos validar a automacao Telegram ponta a ponta antes de operar em producao.

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
