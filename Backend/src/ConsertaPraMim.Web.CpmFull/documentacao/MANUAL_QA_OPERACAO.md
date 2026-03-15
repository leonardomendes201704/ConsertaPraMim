# Manual de QA e Operacao - ConsertaPraMim.Web.CpmFull

## Objetivo

Orientar validacao funcional e operacao basica do projeto `ConsertaPraMim.Web.CpmFull`.

## Automacao Telegram -> funis clientes/prestadores -> Chatwoot

### Objetivo desta etapa

- Transformar a conversa qualificada do bot Telegram em lead operacional nos boards `clientes` e `prestadores` do CPM Full.
- Reaproveitar a sincronizacao atual do Chatwoot a partir do lead criado ou atualizado no funil.
- Espelhar mensagens Telegram para a conversa humana do Chatwoot com fila e idempotencia.
- Permitir handoff humano do Chatwoot de volta para o Telegram com rastreabilidade no lead.

### Escopo entregue nesta fatia

- O `ConsertaPraMim.Web.TelegramBridge` passou a chamar uma automacao interna do CPM Full apos a qualificacao do fluxo autenticado.
- O CPM Full cria ou atualiza lead no board `clientes` quando o usuario autenticado e `Client` e no board `prestadores` quando o usuario autenticado e `Provider`.
- A deduplicacao desta fase e feita por `ChatbotConversationId`.
- O lead nasce com `Source = Telegram`, contexto inicial da conversa e vinculo tecnico persistido em `dbo.cpm_web_telegram_funil_links`.
- O detalhe do lead no Kanban agora exibe a secao `Vinculo Telegram` com `ChatbotConversationId`, `ChannelConversationId`, `TelegramChatId`, `ClientId`, `ClientPhone`, `ClientEmail`, `ServiceRequestId`, `HumanHandoffStartedAt`, `LastTelegramMessageSyncedAt`, `LastChatwootMessageSyncedAt` e horario da ultima atualizacao do vinculo.
- O Chatwoot continua sendo alimentado pela trilha ja existente do lead do Kanban, agora com bootstrap operacional dedicado para leads Telegram no inbox correto de `clientes` ou `prestadores`.
- O `TelegramChatbotController` da API agora aceita sessao, mensagens, snapshots, actions, estado e historico para `Client` e `Provider`, mantendo pedidos/agendamentos como `client-only`.
- Mensagens recebidas do Telegram agora podem ser espelhadas para o Chatwoot pela fila `telegram_to_chatwoot`, com deduplicacao por `ChannelMessageId`.
- Mensagens humanas publicas do Chatwoot agora podem voltar para o Telegram pela fila `chatwoot_to_telegram`, com endpoint interno protegido no bridge e handoff humano marcado no lead.
- O detalhe do lead e o drawer `Diagnostico Telegram` passaram a exibir `TelegramChatId`, `ClientEmail` e erros operacionais em formato mascarado, sem expor PII bruta para suporte.
- O CPM Full agora expurga payloads antigos da fila `dbo.cpm_web_telegram_delivery_queue`, enquanto o bridge remove anexos antigos de `wwwroot/uploads/telegram-bridge` dentro da janela de retention configurada.
- O bridge agora suporta dois modos inbound: `LongPolling` e `Webhook`.
- Em `LongPolling`, o bootstrap remove webhook anterior do bot e continua usando `getUpdates`.
- Em `Webhook`, o bridge registra automaticamente `setWebhook`, exige `X-Telegram-Bot-Api-Secret-Token` no endpoint `POST /api/integrations/telegram/webhook` e desabilita o worker de polling.
- O primeiro ACK do bot agora pode solicitar telefone com botao nativo `request_contact`, sem bloquear a jornada.
- Quando o usuario compartilha contato no Telegram ou envia telefone/e-mail por texto em formato seguro, o mesmo lead do CPM Full e o mesmo contato do Chatwoot sao enriquecidos automaticamente.
- O modal de detalhes do lead no Kanban agora expõe a acao `Excluir lead`, removendo o lead local, historico, vinculo Telegram e filas relacionadas.
- Quando o lead possui `TelegramChatId`, a exclusao tenta resetar o handoff humano em memoria no `TelegramBridge` antes de concluir o reset local.
- O fluxo de exclusao agora tambem pode apagar opcionalmente o contato tecnico no Chatwoot quando o lead ja possui `ChatwootContactId`, por meio de checkbox desmarcado por padrao no modal.

### Configuracao minima

No `ConsertaPraMim.Web.TelegramBridge`, configurar a secao `TelegramAutomation`:

- `Enabled`
- `ClientsAutomationEnabled`
- `ProvidersAutomationEnabled`
- `MirrorMessagesEnabled`
- `RequireHumanHandoffForOutbound`
- `CpmFullBaseUrl`
- `SharedSecret`
- `RequestTimeoutSeconds`

No `ConsertaPraMim.Web.TelegramBridge`, configurar tambem a secao `TelegramBridge`:

- `UpdateTransport`
- `WebhookPublicBaseUrl`
- `WebhookPath`
- `WebhookSecretToken`
- `WebhookDropPendingUpdates`
- `AttachmentRetentionEnabled`
- `AttachmentRetentionDays`
- `AttachmentRetentionIntervalMinutes`

No `ConsertaPraMim.Web.CpmFull`, configurar a secao `TelegramAutomation`:

- `Enabled`
- `ClientsAutomationEnabled`
- `ProvidersAutomationEnabled`
- `MirrorMessagesEnabled`
- `RequireHumanHandoffForOutbound`
- `TelegramBridgeBaseUrl`
- `SharedSecret`
- `RequestTimeoutSeconds`
- `DeliveryWorkerEnabled`
- `DeliveryWorkerIntervalSeconds`
- `DeliveryWorkerBatchSize`
- `DeliveryQueueMaxAttempts`
- `DeliveryPayloadCleanupEnabled`
- `DeliveryPayloadRetentionDays`
- `DeliveryPayloadCleanupIntervalMinutes`

### Comportamento esperado

- Com `TelegramAutomation:Enabled=false`, o bot continua operando normalmente e apenas o pedido e criado na API principal.
- Com `TelegramAutomation:Enabled=true`, a trilha do bot deve:
- abrir ou reaproveitar `service request` apenas para `Client`;
- chamar `POST /api/integrations/telegram/automation/lead` no CPM Full;
- criar ou atualizar lead no board correto (`clientes` ou `prestadores`);
- sincronizar o lead com o Chatwoot via `ChatwootLeadSyncService`;
- criar ou reaproveitar conversa no inbox correto do Chatwoot para o board atual;
- registrar historico `Bootstrap Telegram no Chatwoot` quando o lead ainda nao possuía conversa vinculada.
- A mesma `ChatbotConversationId` deve reaproveitar o mesmo lead no CPM Full.
- O lead deve exibir `Source = Telegram` e historico do tipo `Lead criado via bot Telegram` / `Lead atualizado via bot Telegram`.
- No primeiro ACK de um lead Telegram sem telefone, o bridge deve enviar teclado com botao nativo `request_contact` para capturar o numero sem interromper a conversa.
- Quando o usuario compartilhar o contato pelo Telegram, o mesmo lead e o mesmo vinculo tecnico devem ser enriquecidos com `ClientPhone`, sem duplicar lead nem apagar dados anteriores.
- Quando o usuario informar telefone ou e-mail em texto livre, o bridge deve aceitar apenas fallbacks seguros e atualizar o mesmo lead do CPM Full.
- O modal `Vinculo Telegram` deve exibir `Telefone capturado (mascarado)` quando o enriquecimento ja tiver ocorrido.
- Fluxos autenticados como `Provider` nao devem abrir pedido de cliente nem consultar carteira/agendamentos do cliente.
- Com `MirrorMessagesEnabled=true` nos dois lados, cada mensagem nova do Telegram deve ser enfileirada no CPM Full e entregue como mensagem `incoming` na conversa correta do Chatwoot.
- A mesma mensagem do Telegram nao deve duplicar entrega quando o mesmo `ChannelMessageId` voltar a ser processado.
- Quando um operador humano responder publicamente no Chatwoot em conversa originada do Telegram, o webhook do Chatwoot deve enfileirar a entrega para o Telegram.
- O primeiro outbound humano deve marcar `HumanHandoffStartedAt` no vinculo do lead e registrar historico `Handoff humano iniciado`.
- O bridge deve aceitar o envio humano apenas pelo endpoint interno `POST /api/internal/telegram/messages/send`, protegido por `X-Telegram-Automation-Key`.
- Quando o handoff humano estiver ativo, a trilha web do bridge que passa pelo `ChatApiController` deve deixar de emitir nova resposta automatica para o `chatId` marcado.
- O modal `Vinculo Telegram` e o drawer `Diagnostico Telegram` devem exibir `Chat ID Telegram`, `E-mail autenticado` e mensagens de erro apenas em formato mascarado.
- Em `LongPolling`, o bridge deve remover qualquer webhook anterior do bot e continuar recebendo updates por `getUpdates`.
- Em `Webhook`, o bridge deve publicar `POST /api/integrations/telegram/webhook` em HTTPS, registrar automaticamente a URL publica na Bot API e rejeitar requests sem `X-Telegram-Bot-Api-Secret-Token` valido.
- Os endpoints internos bridge <-> CPM Full continuam exigindo o mesmo `SharedSecret`.
- Itens `processed` ou `dead_letter` antigos da fila `dbo.cpm_web_telegram_delivery_queue` devem ter `PayloadJson` redigido automaticamente pelo worker de retention, e anexos antigos do bridge devem ser removidos da pasta `uploads/telegram-bridge`.

### Checklist de QA

1. Configurar `TelegramAutomation` no bridge e no CPM Full com o mesmo `SharedSecret`.
2. Subir `ConsertaPraMim.Web.CpmFull` e confirmar `GET /health`.
3. Subir `ConsertaPraMim.Web.TelegramBridge`.
4. Logar como cliente no bridge.
5. Enviar mensagem de triagem suficiente para o bot abrir um pedido.
6. Confirmar que o bot responde com a mensagem normal de `service_request_created`, sem regressao conversacional.
7. Acessar `/admin/funil/clientes` no CPM Full.
8. Confirmar a criacao do lead com `Source = Telegram`.
9. Abrir o detalhe do lead e validar historico com `Lead criado via bot Telegram`.
10. Confirmar que o modal exibe a secao `Vinculo Telegram` com `Origem automatizada = Telegram`.
11. Validar no modal os campos `Conversa bot Telegram`, `Conversa do canal`, `Chat ID Telegram (mascarado)`, `Cliente vinculado`, `E-mail autenticado (mascarado)` e `Pedido vinculado`.
12. Confirmar que o lead recebeu `StatusNote` e `InternalNotes` descrevendo a origem automatica.
13. Validar que `Sync Chatwoot` foi atualizado pela trilha atual do Chatwoot.
14. Abrir o Chatwoot e confirmar que a conversa foi criada ou reaproveitada no inbox `CPM Clientes`.
15. Validar na conversa e no contato os atributos `CPM Canal de Origem = Telegram` e `CPM Canal de Origem Slug = telegram`.
16. Reabrir o detalhe do lead e confirmar historico `Bootstrap Telegram no Chatwoot`.
17. Reenviar mensagem na mesma conversa do bot, mantendo o contexto do pedido ja criado.
18. Confirmar que o mesmo lead foi reaproveitado e recebeu historico `Lead atualizado via bot Telegram`.
19. Consultar `dbo.cpm_web_telegram_funil_links` e validar um unico registro por `ChatbotConversationId`.
20. Fazer logout e logar como prestador no bridge.
21. Enviar mensagem com intencao de cadastro/reativacao, por exemplo `Sou eletricista em Praia Grande e quero entrar na plataforma`.
22. Confirmar que o bridge nao tenta abrir pedido de cliente nem consultar agenda/pedidos do cliente.
23. Acessar `/admin/funil/prestadores` no CPM Full.
24. Confirmar a criacao ou atualizacao do lead com `Source = Telegram`.
25. Validar no Chatwoot que a conversa foi aberta ou reaproveitada no inbox `CPM Prestadores`.
26. Abrir o detalhe do lead e validar que o vinculo Telegram reaproveitou a mesma `ChatbotConversationId` e registrou o `ClientId`/`ClientEmail` autenticados do prestador por compatibilidade tecnica.
27. Confirmar historico `Bootstrap Telegram no Chatwoot` tambem para o fluxo de prestadores quando o lead ainda nao tinha conversa vinculada.

### Checklist complementar para captura de contato e enriquecimento

1. Iniciar uma conversa nova com o bot Telegram em um chat sem telefone previamente enriquecido.
2. Confirmar que o primeiro ACK do bot inclui botao nativo para compartilhar contato.
3. Compartilhar o telefone usando o recurso `request_contact` do Telegram.
4. Reabrir o detalhe do lead no CPM Full e validar `Telefone capturado (mascarado)` na secao `Vinculo Telegram`.
5. Confirmar em banco que `dbo.cpm_web_telegram_funil_links.ClientPhone` foi preenchido para o mesmo `ChatbotConversationId`.
6. Confirmar que o telefone do lead no CPM Full foi atualizado sem criar novo lead.
7. Reenviar mensagem comum no mesmo chat e validar que o telefone continua preservado no lead e no vinculo.
8. Em outra conversa de teste, informar telefone em texto livre com formato valido e confirmar enriquecimento no mesmo lead.
9. Informar e-mail em texto livre e confirmar que o vinculo Telegram e o lead reaproveitam o mesmo registro, sem limpar telefone/cidade/categoria ja existentes.
10. Abrir o contato no Chatwoot e validar que o contato tecnico foi enriquecido com o telefone real capturado apos o bootstrap inicial.

### Checklist complementar para qualificacao inicial do lead Telegram

1. Iniciar uma conversa nova com o bot Telegram em um chat sem cidade/categoria previamente enriquecidas.
2. Enviar uma mensagem de cliente como `Preciso de ajuda urgente com meu chuveiro em Santos`.
3. Confirmar que o primeiro ACK do bot pede telefone e tambem orienta o usuario a informar cidade, tipo de servico e o que precisa resolver.
4. Abrir o lead em `/admin/funil/clientes` e validar `ServiceCategory = Eletricista` e `City = Santos`.
5. Confirmar que o `StatusNote` do lead resume `cidade`, `categoria` e `intencao` em PT-BR operacional.
6. Reenviar mensagem complementar do mesmo chat, por exemplo `Meu CEP e 11035-010`, e confirmar enriquecimento do mesmo lead sem duplicidade.
7. Em outro chat, enviar uma mensagem de prestador como `Sou eletricista em Praia Grande e quero me cadastrar como prestador parceiro`.
8. Confirmar que o lead cai em `/admin/funil/prestadores`, com `ServiceCategory = Eletricista`, `City = Praia Grande` e objetivo de cadastro refletido no `StatusNote`.
9. Validar que o roteamento `clientes` x `prestadores` ocorreu sem depender de ajuste manual no board.
10. Revisar `InternalNotes` do lead e confirmar que cidade/regiao, categoria e intencao ficaram registradas junto da mensagem inicial.

### Checklist complementar para espelhamento e handoff

1. Habilitar `MirrorMessagesEnabled=true` no bridge e no CPM Full.
2. Garantir que o lead Telegram ja possua conversa humana valida no Chatwoot.
3. Enviar uma nova mensagem real no mesmo chat do Telegram apos a conversa humana ja existir.
4. Confirmar no Chatwoot o recebimento de uma nova mensagem `incoming` correspondente ao texto enviado no Telegram.
5. Reabrir o detalhe do lead e validar historico `Mensagem Telegram sincronizada para Chatwoot`.
6. Validar no modal `Ultima msg Telegram sincronizada` preenchida.
7. Reprocessar a mesma entrega pelo mesmo `ChannelMessageId` apenas em teste tecnico e confirmar ausencia de duplicidade na conversa do Chatwoot.
8. No Chatwoot, enviar uma resposta humana publica para a conversa originada do Telegram.
9. Confirmar que a mensagem chega no chat do Telegram do usuario.
10. Reabrir o detalhe do lead e validar historico `Handoff humano iniciado` no primeiro outbound e `Mensagem humana sincronizada para Telegram`.
11. Validar no modal `Handoff humano iniciado` e `Ultima msg Chatwoot sincronizada` preenchidos.

### Checklist complementar para bot publico sem login previo

1. Confirmar que o bot publicado esta ativo em `LongPolling` ou `Webhook`, com `TelegramAutomation:Enabled=true` e `MirrorMessagesEnabled=true`.
2. Abrir o Telegram e iniciar conversa direta com o bot publicado sem acessar o painel web do bridge.
3. Enviar uma mensagem simples de cliente, por exemplo `Preciso de ajuda com meu chuveiro`.
4. Confirmar que o usuario recebe um ACK inicial do bot informando que o atendimento foi registrado.
5. Acessar `/admin/funil/clientes` no CPM Full.
6. Confirmar a criacao ou atualizacao de um lead com `Source = Telegram` e historico `Lead criado automaticamente a partir da conversa do bot Telegram`.
7. Abrir o detalhe do lead e validar a secao `Vinculo Telegram` com `ChatbotConversationId` preenchido, `Chat ID Telegram` mascarado e `ChannelConversationId` igual ao `chatId` original.
8. Validar no Chatwoot que a conversa humana foi criada ou reaproveitada na inbox `CPM Clientes`.
9. Se o usuario ainda nao tiver informado telefone/e-mail, validar no Chatwoot que o contato foi criado com identificador tecnico do Telegram e que a nota privada registra essa ausencia temporaria.
10. Enviar uma mensagem publica no Chatwoot e confirmar o retorno ao mesmo chat do Telegram.
11. Repetir o teste com texto de onboarding de prestador, por exemplo `Quero me cadastrar como prestador parceiro`.
12. Confirmar que o lead caiu em `/admin/funil/prestadores` e que a conversa humana foi criada ou reaproveitada na inbox `CPM Prestadores`.

### Checklist complementar para exclusao operacional do lead

1. Abrir o detalhe de um lead no Kanban de `clientes` ou `prestadores`.
2. Validar que o checkbox `Excluir tambem o contato no Chatwoot` inicia desmarcado.
3. Se o lead ja possuir `ChatwootContactId`, marcar o checkbox para teste de limpeza remota; se nao possuir, confirmar que o checkbox permanece desabilitado.
4. Clicar em `Excluir lead`.
5. Confirmar que o modal de confirmacao informa explicitamente quando o contato remoto sera apagado e quando nao sera.
6. Confirmar que o lead desaparece do quadro apos a exclusao.
7. Validar em banco que nao restaram registros para o `LeadId` em `dbo.cpm_web_kanban_leads`, `dbo.cpm_web_kanban_lead_history`, `dbo.cpm_web_telegram_funil_links`, `dbo.cpm_web_telegram_delivery_queue` e `dbo.cpm_web_chatwoot_sync_queue`.
8. Para lead Telegram com handoff humano previo, reenviar mensagem no mesmo chat e validar que o bot voltou a responder sem exigir restart manual do bridge.
9. Quando o checkbox tiver sido marcado, validar no Chatwoot que o contato tecnico foi removido; quando o checkbox nao tiver sido marcado, confirmar que o contato continua existindo.
10. Confirmar que a conversa no Chatwoot segue o comportamento da propria plataforma e nao e prometida como excluida pelo CPM Full.

### Roteiro rapido de validacao em producao

- Para uma execucao objetiva do smoke E2E publicado, usar o documento `ROTEIRO_TESTE_E2E_TELEGRAM_PRODUCAO.md` nesta mesma pasta.
- O roteiro consolida pre-check, fluxo de `clientes`, fluxo de `prestadores`, espelhamento `Telegram -> Chatwoot`, handoff `Chatwoot -> Telegram` e troubleshooting rapido.

### Troubleshooting

- `401` na automacao interna: validar se bridge e CPM Full usam exatamente o mesmo `TelegramAutomation:SharedSecret`.
- `409` com automacao desabilitada: conferir `TelegramAutomation:Enabled` e a flag correta (`ClientsAutomationEnabled` ou `ProvidersAutomationEnabled`) nos dois projetos.
- `409` com automacao desabilitada apenas no ambiente publicado: revisar se o job `deploy-web-cpmfull` escreveu todas as variaveis `TELEGRAM_AUTOMATION_*` no `Backend/.env.vps` e se o container `cpm-prd-cpmfull` foi recriado apos a mudanca.
- Pedido criado, mas sem lead no CPM Full: revisar `TelegramAutomation:CpmFullBaseUrl`, reachability HTTP e logs do `TelegramLeadAutomationClient`.
- Lead duplicado: validar se a mesma conversa esta preservando o mesmo `ChatbotConversationId` na trilha do chatbot.
- Modal sem `Vinculo Telegram`: validar se existe registro em `dbo.cpm_web_telegram_funil_links` para o `LeadId` e se o detalhe do lead foi recarregado apos a automacao.
- Bot nao pediu telefone no primeiro ACK: validar se a conversa ainda nao tinha `ClientPhone` no vinculo Telegram e se o bridge publicado contem a entrega da `ST-095`.
- Telefone compartilhado nao apareceu no funil: validar se o update recebido possui `message.contact`, se o `user_id` do contato corresponde ao remetente e se o `TelegramInboundUpdateProcessor` nao descartou o payload por seguranca.
- Telefone ou e-mail sumiram apos nova mensagem: validar se o ambiente publicado contem o endurecimento do `SqlAdminKanbanService` com `COALESCE` para nao apagar dados opcionais em reprocessamentos.
- Exclusao do lead falhou antes de concluir: validar reachability do `TelegramBridge`, `TelegramAutomation:Enabled`, `TelegramBridgeBaseUrl`, `SharedSecret` e se o endpoint interno `/api/internal/telegram/messages/handoff/reset` esta acessivel.
- Checkbox de exclusao remota esta desabilitado: comportamento esperado quando o lead ainda nao possui `ChatwootContactId`; sincronizar o lead antes se quiser limpar tambem o contato remoto.
- Exclusao remota no Chatwoot falhou: validar `Chatwoot:Enabled`, conectividade com a API oficial, `ApiAccessToken`, `AccountId` e se o contato ainda existe no account configurado.
- Lead foi excluido, mas o mesmo chat ainda nao voltou a responder no bot: validar se a nova mensagem foi enviada apos a exclusao, se o handoff realmente foi resetado no bridge e, como ultimo recurso, reiniciar o `TelegramBridge` para limpar estado em memoria residual.
- Lead Telegram caiu no inbox errado do Chatwoot: revisar `BoardType` do lead, `ClientsInboxId`, `ProvidersInboxId` e se o board atual no CPM Full esta coerente com o papel autenticado.
- Lead Telegram sem historico `Bootstrap Telegram no Chatwoot`: validar se o lead ja possuia `ChatwootConversationId`; o evento so aparece quando o bootstrap precisa criar ou reaproveitar a conversa humana a partir do funil.
- Lead Telegram sem telefone/e-mail no primeiro contato: comportamento esperado; a sincronizacao com Chatwoot deve usar `TelegramChatId`, `ChatbotConversationId` ou `ChannelConversationId` como identificador tecnico do contato.
- Lead nao-Telegram sem telefone e sem e-mail: corrigir o cadastro antes de acionar `Sincronizar Chatwoot`.
- Prestador recebendo resposta de pedido/agendamento: validar se a publicacao contem a trilha `Provider` do `TelegramChatbotOrchestrator` e se o login do bridge carregou a claim `Role = Provider`.
- Mensagem nova do Telegram nao apareceu no Chatwoot: validar `MirrorMessagesEnabled=true` nos dois projetos, o worker `TelegramDeliveryWorker`, a tabela `dbo.cpm_web_telegram_delivery_queue` e se o lead ja possui vinculo Telegram ativo.
- Mensagem humana do Chatwoot nao voltou ao Telegram: validar se a mensagem no Chatwoot e publica, se o lead possui `TelegramChatId`, se o webhook inbound do Chatwoot esta saudavel e se o bridge aceita `POST /api/internal/telegram/messages/send`.
- Fila presa em retentativa: revisar `LastError`, `AttemptCount`, `NextAttemptAt`, reachability entre CPM Full e bridge, e se `DeliveryQueueMaxAttempts` nao ja levou o item para `dead_letter`.
- Bot continuou respondendo apos handoff humano: validar se `RequireHumanHandoffForOutbound=true`, se o primeiro outbound humano chegou a ativar o handoff e se a conversa esta passando pela trilha web controlada pelo `ChatApiController`.
- Bridge recebe updates, mas nada aparece no funil: abrir `docker logs --tail 200 cpm-prd-telegrambridge` e procurar `409 Automacao Telegram desabilitada no ambiente atual.`; se aparecer, validar `docker exec cpm-prd-cpmfull printenv | grep '^TelegramAutomation__'`.

### Checklist complementar para diagnostico operacional

1. Com `TelegramAutomation:Enabled=true`, acessar `/admin/funil/clientes` ou `/admin/funil/prestadores`.
2. Abrir o drawer `Diagnostico Telegram`.
3. Confirmar exibicao do resumo operacional com `Leads Telegram`, `Inbound espelhado`, `Outbound espelhado`, `Handoffs humanos`, `Fila ativa` e `Dead-letter`.
4. Confirmar exibicao das metricas do Telegram Bridge com volume e latencia (`Msgs recebidas`, `Msgs enviadas`, `Com anexos`, `Handoffs no bot`, `Falhas de IA`, `P95 IA`).
5. Validar que o drawer mostra incidentes recentes do bot com `Correlation ID`.
6. Gerar uma falha controlada na entrega Telegram -> Chatwoot ou Chatwoot -> Telegram.
7. Confirmar que a falha aparece em `Falhas recentes` e tambem em `Fila e dead-letter`.
8. Acionar `Reprocessar` diretamente no item de fila.
9. Confirmar retorno visual de sucesso e nova carga do drawer.
10. Reabrir o detalhe do lead e validar historico `Entrega Telegram enfileirada` apos a retentativa manual.

### Checklist complementar para seguranca e conformidade

1. Forcar uma falha operacional na trilha Telegram contendo e-mail, telefone, token ou `chatId` no texto bruto.
2. Abrir o drawer `Diagnostico Telegram` e confirmar que `Falhas recentes`, `Fila e dead-letter` e incidentes do bridge exibem apenas valores mascarados.
3. Abrir o detalhe do lead e confirmar que `Chat ID Telegram (mascarado)` e `E-mail autenticado (mascarado)` nao expoem o valor bruto.
4. Configurar `TelegramAutomation:DeliveryPayloadCleanupEnabled=true` no CPM Full com retention curta em ambiente local/QA.
5. Inserir item antigo em `dbo.cpm_web_telegram_delivery_queue` com `Status = processed` ou `dead_letter`.
6. Aguardar o worker de retention ou executa-lo manualmente reiniciando a aplicacao com a janela reduzida.
7. Confirmar que `PayloadJson` virou `{\"redacted\":true,\"reason\":\"retention\"}` e `PayloadPurgedAt` foi preenchido em UTC.
8. Configurar `TelegramBridge:AttachmentRetentionEnabled=true` com janela curta no bridge e criar um anexo antigo em `wwwroot/uploads/telegram-bridge`.
9. Confirmar que o worker do bridge remove o arquivo fora da janela e limpa diretorios vazios.
10. Validar que os endpoints internos `POST /api/integrations/telegram/automation/lead`, `POST /api/integrations/telegram/automation/message`, `POST /api/internal/telegram/messages/send` e `GET /api/internal/telegram/observability/dashboard` continuam recusando chamadas sem `X-Telegram-Automation-Key` valido.

### Checklist complementar para modo webhook do Telegram

1. Configurar `TelegramBridge:UpdateTransport=Webhook`.
2. Preencher `TelegramBridge:WebhookPublicBaseUrl`, `TelegramBridge:WebhookPath` e `TelegramBridge:WebhookSecretToken` com valores validos em HTTPS.
3. Publicar o `ConsertaPraMim.Web.TelegramBridge` na URL configurada.
4. Reiniciar o bridge e validar em log que o bootstrap registrou `setWebhook` com sucesso.
5. Enviar mensagem real ao bot e confirmar processamento pelo endpoint `POST /api/integrations/telegram/webhook`.
6. Confirmar que o worker de long polling nao ficou ativo no runtime quando o modo `Webhook` estiver ligado.
7. Repetir a validacao de lead, inbox Chatwoot, espelhamento inbound e handoff humano com o modo webhook ativo.

### Estado publicado validado em producao

- Host publicado: `https://telegram.consertapramim.com`
- Endpoint de webhook esperado: `https://telegram.consertapramim.com/api/integrations/telegram/webhook`
- Transporte inbound publicado: `Webhook`
- Healthcheck esperado: `curl https://telegram.consertapramim.com/health` -> `Healthy`
- Validacao da Bot API: `getWebhookInfo.url` deve refletir exatamente a URL publica do webhook e `pending_update_count` deve permanecer controlado.

### Publicacao do TelegramBridge na VPS

#### Objetivo

- Publicar o `ConsertaPraMim.Web.TelegramBridge` como servico proprio da VPS, com URL publica HTTPS e healthcheck dedicado para sustentar o modo `Webhook`.

#### Comportamento esperado

- O workflow `.github/workflows/deploy-vps.yml` deve detectar mudancas em `Backend/src/ConsertaPraMim.Web.TelegramBridge/**`, `Backend/docker/vps/Dockerfile.web.telegrambridge` e `Backend/docker-compose.vps.web-telegrambridge.yml`.
- O job `deploy-web-telegrambridge` deve publicar o container `${CONTAINER_PREFIX}-telegrambridge` na porta `TELEGRAM_BRIDGE_PORT` (`5175` em prod, `6175` em dev).
- O healthcheck `health-web-telegrambridge` deve validar `GET /health`.
- Em `dev-local`, o healthcheck deve preferir `PUBLIC_TELEGRAM_BRIDGE_URL` quando esse secret existir; sem ele, o fallback continua em `http://<VPS_PUBLIC_HOST>:6175/health`.
- Em `main/master`, o healthcheck deve usar `http://127.0.0.1:5175/health`.
- O bridge publicado atras do Nginx deve interpretar `X-Forwarded-For`, `X-Forwarded-Proto` e `X-Forwarded-Host`, evitando redirecionamento HTTPS indevido para o webhook.
- O `Dockerfile` publicado do bridge deve manter a mesma major do `TargetFramework` do projeto (`net8.0` -> `sdk/aspnet:8.0`) para evitar restart loop por framework ausente no container.

#### Configuracao minima no GitHub Actions / `.env.vps`

- `PUBLIC_TELEGRAM_BRIDGE_URL`
- `TELEGRAM_BRIDGE_PORT`
- `TELEGRAM_BRIDGE_BOT_TOKEN`
- `TELEGRAM_BRIDGE_UPDATE_TRANSPORT`
- `TELEGRAM_BRIDGE_WEBHOOK_PUBLIC_BASE_URL`
- `TELEGRAM_BRIDGE_WEBHOOK_PATH`
- `TELEGRAM_BRIDGE_WEBHOOK_SECRET_TOKEN`
- `TELEGRAM_AUTOMATION_ENABLED`
- `TELEGRAM_AUTOMATION_CLIENTS_ENABLED`
- `TELEGRAM_AUTOMATION_PROVIDERS_ENABLED`
- `TELEGRAM_AUTOMATION_MIRROR_MESSAGES_ENABLED`
- `TELEGRAM_AUTOMATION_REQUIRE_HANDOFF_FOR_OUTBOUND`
- `TELEGRAM_AUTOMATION_CPMFULL_BASE_URL`
- `TELEGRAM_AUTOMATION_TELEGRAM_BRIDGE_BASE_URL`
- `TELEGRAM_AUTOMATION_SHARED_SECRET`

#### Checklist operacional

1. Cadastrar no environment correto a URL publica do bridge:
2. `production` -> `https://telegram.consertapramim.com`
3. `development` -> URL HML dedicada, por exemplo `https://telegram-hml.consertapramim.com`, ou manter vazio para fallback em `http://<VPS_PUBLIC_HOST>:6175`
4. Se o transporte permanecer em `LongPolling`, habilitar o `TELEGRAM_BRIDGE_BOT_TOKEN` apenas em um environment por vez. O uso simultaneo do mesmo bot em `development` e `production` faz os consumidores disputarem `getUpdates`.
5. Publicar a branch desejada e acompanhar os jobs `deploy-web-telegrambridge` e `health-web-telegrambridge`.
6. Na VPS, validar `docker ps --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}" | grep telegrambridge`.
7. Validar `curl -I http://127.0.0.1:5175/health` em producao ou `curl -I http://127.0.0.1:6175/health` em homologacao.
8. Validar a URL publica coerente com o environment:
9. producao -> `curl -I https://telegram.consertapramim.com/health`
10. homologacao -> `curl -I <PUBLIC_TELEGRAM_BRIDGE_URL do environment development>/health`
11. Confirmar que o `web-cpmfull` publicado recebeu `TelegramAutomation__Enabled=true`, `TelegramAutomation__SharedSecret` e `TelegramAutomation__TelegramBridgeBaseUrl`.
12. Se o modo webhook estiver habilitado, enviar mensagem real ao bot e confirmar que o runtime nao caiu em `UseHttpsRedirection`/redirect loop.

#### Troubleshooting

- Workflow nao dispara o deploy do bridge: validar se a alteracao afetou `Backend/src/ConsertaPraMim.Web.TelegramBridge/**`, `Backend/docker/vps/Dockerfile.web.telegrambridge`, `Backend/docker-compose.vps.web-telegrambridge.yml` ou arquivos globais de deploy.
- `health-web-telegrambridge` falha so em `dev-local`: revisar o secret `PUBLIC_TELEGRAM_BRIDGE_URL`; se ele estiver incorreto, o workflow tentara essa URL antes do fallback `IP:6175`.
- Container do bridge entra em `Restarting` com erro `You must install or update .NET`: validar se `Backend/docker/vps/Dockerfile.web.telegrambridge` usa `sdk` e `aspnet` na mesma major do `TargetFramework` do projeto (`ConsertaPraMim.Web.TelegramBridge.csproj`).
- Mesmo bot responde de forma intermitente em dev e prod: validar se o `TELEGRAM_BRIDGE_BOT_TOKEN` foi cadastrado nos dois environments ao mesmo tempo com `TelegramBridge:UpdateTransport=LongPolling`.
- Bridge sobe, mas o webhook recebe `307/308`: validar se a publicacao contem `ForwardedHeaders` e se o Nginx esta encaminhando `X-Forwarded-Proto=https`.
- Webhook seguro nao registra `setWebhook`: revisar `TELEGRAM_BRIDGE_BOT_TOKEN`, `TELEGRAM_BRIDGE_WEBHOOK_PUBLIC_BASE_URL`, `TELEGRAM_BRIDGE_WEBHOOK_PATH` e `TELEGRAM_BRIDGE_WEBHOOK_SECRET_TOKEN`.
- Bot recebe mensagem, mas o CPM Full nao reage: validar `TelegramAutomation:Enabled=true` no `web-cpmfull`, `TelegramAutomation:SharedSecret` identico nos dois lados e `TelegramAutomation:TelegramBridgeBaseUrl` apontando para `http://<container-prefix>-telegrambridge:<porta>`.
- Bot recebe mensagem, mas nao cria lead nem conversa no Chatwoot: validar se a publicacao contem a correcao pos-epico de bootstrap publico da primeira mensagem (`TelegramInboundUpdateProcessor` com lead bootstrap antes do mirror), se `ClientsAutomationEnabled/ProvidersAutomationEnabled` estao habilitados e se o bridge nao ficou apenas na trilha autenticada do painel web.

### Runbook de rotacao do token e segredo do bot

1. Gerar novo token do bot no `@BotFather`, sem invalidar o antigo antes de preparar os dois lados da publicacao.
2. Atualizar `TelegramBridge__BotToken` no ambiente do `ConsertaPraMim.Web.TelegramBridge`.
3. Revisar e, se necessario, rotacionar tambem `TelegramAutomation__SharedSecret` no bridge e no CPM Full no mesmo change set.
4. Reiniciar bridge e CPM Full em janela controlada.
5. Validar login, conversa, criacao de lead, bootstrap Chatwoot, espelhamento inbound e outbound humano.
6. Se o transporte inbound estiver em `Webhook`, revisar tambem `TelegramBridge:WebhookPublicBaseUrl`, `TelegramBridge:WebhookPath` e `TelegramBridge:WebhookSecretToken` antes de invalidar o token antigo.
7. Revalidar o webhook publicado com mensagem real, confirmando header `X-Telegram-Bot-Api-Secret-Token` e entrega fim a fim antes de concluir a rotacao.

### Troubleshooting complementar do diagnostico e seguranca

- Drawer sem metricas do Bridge: validar `TelegramAutomation:TelegramBridgeBaseUrl`, `TelegramAutomation:SharedSecret` e se o bridge publicou `GET /api/internal/telegram/observability/dashboard`.
- Drawer com metricas locais mas sem snapshot do bridge: comportamento degradado esperado quando o bridge estiver indisponivel; revisar reachability HTTP entre CPM Full e bridge.
- Reprocessar nao encontra o item: validar se o `queueItemId` ainda existe em `dbo.cpm_web_telegram_delivery_queue` e se o item nao foi limpo por reprocessamento concorrente.
- `Correlation ID` vazio no diagnostico do bridge: validar se as chamadas do bridge para o CPM Full e do CPM Full para o bridge estao trafegando com `X-Correlation-ID` apos a publicacao da US-08.
- Payload antigo nao foi redigido: validar `TelegramAutomation:DeliveryPayloadCleanupEnabled`, janela de retention, status do item (`processed`/`dead_letter`) e se `PayloadPurgedAt` ainda esta `NULL`.
- Anexo antigo continua em disco no bridge: validar `TelegramBridge:AttachmentRetentionEnabled`, path `wwwroot/uploads/telegram-bridge`, horario UTC do arquivo e execucao do `TelegramAttachmentRetentionWorker`.
- Endpoint interno aceitou chamada sem segredo: validar reverse proxy, se o header `X-Telegram-Automation-Key` esta sendo exigido pela aplicacao e se nao existe reescrita indevida na borda.
- Webhook do Telegram nao recebe trafego: validar `TelegramBridge:UpdateTransport=Webhook`, `TelegramBridge:WebhookPublicBaseUrl`, publicacao HTTPS do endpoint `/api/integrations/telegram/webhook` e se a Bot API registrou a URL esperada.
- Telegram segue entregando por polling quando webhook foi ligado: validar se a instancia publicada recebeu as novas envs e se o log do bridge registrou `worker de long polling desabilitado`.

### Checklist final de homologacao do epic Telegram

1. Publicar `ConsertaPraMim.Web.CpmFull`, `ConsertaPraMim.Web.TelegramBridge` e API na branch alvo.
2. Confirmar `TelegramAutomation:Enabled=true` nos dois lados e `Chatwoot:Enabled=true` no CPM Full.
3. Validar fluxo `clientes`: abrir conversa autenticada, gerar lead, confirmar bootstrap no `CPM Clientes`, espelhar mensagem inbound e receber resposta humana.
4. Validar fluxo `prestadores`: abrir conversa autenticada, gerar lead, confirmar bootstrap no `CPM Prestadores`, espelhar mensagem inbound e receber resposta humana.
5. Reabrir o lead nos dois boards e confirmar `Vinculo Telegram`, `Sync Chatwoot`, `Ultima msg Telegram sincronizada`, `Ultima msg Chatwoot sincronizada` e `Handoff humano iniciado`.
6. Abrir o drawer `Diagnostico Telegram` e confirmar metricas locais + snapshot do bridge, fila ativa, dead-letter e incidentes recentes.
7. Simular indisponibilidade externa controlada no bridge ou no Chatwoot e confirmar item em `retrying`/`dead_letter` com reprocessamento manual funcionando.
8. Confirmar mascaramento de `chatId`, e-mail e erros sensiveis no modal e no drawer.
9. Confirmar retention de payloads e anexos em ambiente de QA com janela reduzida.
10. Validar smoke final em producao/homologacao: criacao de lead, bootstrap Chatwoot, mensagem inbound, handoff humano e drawer de diagnostico.

### Plano de rollback da trilha Telegram

1. Se a falha afetar apenas criacao de lead automatica, desligar seletivamente:
   - `TelegramAutomation:ClientsAutomationEnabled=false`
   - `TelegramAutomation:ProvidersAutomationEnabled=false`
2. Se a falha afetar espelhamento bidirecional, desligar seletivamente:
   - `TelegramAutomation:MirrorMessagesEnabled=false` no bridge
   - `TelegramAutomation:MirrorMessagesEnabled=false` no CPM Full
3. Se a falha afetar apenas outbound humano, manter espelhamento inbound e desligar:
   - `TelegramAutomation:RequireHumanHandoffForOutbound=false` para impedir takeover automatico
   - ou `MirrorMessagesEnabled=false` quando precisar congelar toda a trilha bidirecional
4. Em rollback parcial, o bot continua operando com a trilha conversacional propria e, para `Client`, o fluxo principal de `service request` segue funcional.
5. Apos rollback, validar imediatamente login, envio/recebimento no bridge, abertura de pedido do cliente e ausencia de erros novos no drawer `Diagnostico Telegram`.

## Home - botao flutuante de WhatsApp

### Comportamento esperado

- A home (`/`) deve exibir um botao flutuante de WhatsApp fixado no canto inferior direito.
- Em desktop, o CTA deve aparecer com icone e texto `Suporte no WhatsApp`.
- Em mobile, o CTA pode recolher para exibicao apenas do icone.
- Ao clicar, o navegador deve abrir uma nova aba/janela para conversa no WhatsApp com o numero `5513996891738`.
- A mensagem inicial deve chegar preenchida como `Ola! Preciso de suporte no chat da ConsertaPraMim.`.

### Checklist de QA

1. Acessar a home do projeto.
2. Confirmar que o botao aparece acima do conteudo, sem cobrir o header.
3. Validar hover/focus visual em desktop.
4. Clicar no CTA e confirmar abertura do link `wa.me`.
5. Validar que o numero de destino corresponde a `(13) 99689-1738`.
6. Validar que a mensagem inicial chega preenchida.
7. Repetir o teste em viewport mobile e confirmar que o botao continua acessivel.

### Troubleshooting

- Se o botao nao aparecer, validar se a view `Views/Home/Index.cshtml` foi publicada junto com `wwwroot/css/site.css`.
- Se o icone nao renderizar, validar carga local de `bootstrap-icons.min.css` e das fontes em `wwwroot/lib/bootstrap-icons/font/fonts/`.
- Se o clique nao abrir conversa, validar bloqueio de popup/aba do navegador e conferir se o `href` continua no formato `https://wa.me/5513996891738?...`.

## Integracao Chatwoot - configuracao base

### Objetivo desta etapa

- Validar a base tecnica inicial da integracao com Chatwoot antes da sincronizacao de leads e conversas.

### Configuracao minima

Preencher a secao `Chatwoot` via `appsettings.Local.json` ou variaveis de ambiente:

- `BaseUrl`
- `ApiAccessToken`
- `AccountId`
- `ClientsInboxId`
- `ProvidersInboxId`
- `WebhookSecret`
- `Enabled`

### Comportamento esperado

- Com `Chatwoot:Enabled=false`, o projeto deve iniciar normalmente sem exigir credenciais.
- Com `Chatwoot:Enabled=true` e configuracao incompleta, a aplicacao deve falhar de forma explicita no startup.
- O endpoint `/internal/health/chatwoot` deve responder JSON com status da conectividade.
- Quando os inboxes configurados existirem na conta, o health check deve retornar `Healthy`.
- Quando a API responder mas algum inbox configurado nao existir, o health check deve retornar `Degraded`.
- Quando houver falha de conectividade/autenticacao, o endpoint deve retornar `Unhealthy`.

### Checklist de QA

1. Subir a aplicacao com `Chatwoot:Enabled=false`.
2. Confirmar boot normal.
3. Chamar `/internal/health/chatwoot` e validar retorno `Healthy` com mensagem de integracao desabilitada.
4. Subir a aplicacao com `Chatwoot:Enabled=true` e sem `ApiAccessToken`.
5. Confirmar falha explicita no startup.
6. Configurar todos os campos obrigatorios.
7. Chamar `/internal/health/chatwoot`.
8. Confirmar retorno JSON contendo `status` e `checks.chatwoot_connection`.

### Troubleshooting

- `401/403`: validar `ApiAccessToken` do perfil admin no Chatwoot.
- `404` em API: validar `BaseUrl` e `AccountId`.
- `Degraded`: validar se `ClientsInboxId` e `ProvidersInboxId` pertencem a mesma conta configurada.
- `TaskCanceledException`: revisar timeout, DNS, proxy reverso e acesso da aplicacao ao host do Chatwoot.
- `Integracao Chatwoot desabilitada.` em ambiente publicado: validar se o environment do GitHub Actions da branch (`production` para `main/master`, `development` para `dev-local`) possui todos os secrets `CPMFULL_CHATWOOT_*` e se o job `deploy-web-cpmfull` escreveu esses valores em `Backend/.env.vps` antes do deploy.

## Integracao Chatwoot - persistencia de vinculo no funil

### Objetivo desta etapa

- Persistir no lead os IDs tecnicos e o status operacional da sincronizacao com Chatwoot para rastreabilidade no funil.

### Comportamento esperado

- O bootstrap SQL do `SqlAdminKanbanService` deve criar as colunas `ChatwootContactId`, `ChatwootConversationId`, `ChatwootInboxId`, `ChatwootSyncStatus`, `ChatwootLastSyncAt` e `ChatwootLastError` quando elas ainda nao existirem.
- O bootstrap SQL deve criar de forma idempotente o indice `IX_cpm_web_kanban_leads_chatwoot_conversation`.
- Leads antigos, ainda sem IDs do Chatwoot, devem continuar abrindo normalmente no Kanban.
- O endpoint `/admin/funil/lead/{id}/json` deve expor um bloco `chatwoot` com os campos persistidos.
- O modal de detalhes do lead no Kanban deve exibir status, data da ultima sync, IDs tecnicos e ultimo erro, sempre com fallback seguro quando nao houver dados.

### Checklist de QA

1. Subir o `ConsertaPraMim.Web.CpmFull`.
2. Acessar `/admin/funil/clientes` ou `/admin/funil/prestadores`.
3. Abrir o detalhe de um lead legado e confirmar exibicao de `Ainda nao sincronizado`, sem quebra de tela.
4. Identificar o `Id` do lead e executar update manual no banco:
5. `UPDATE dbo.cpm_web_kanban_leads SET ChatwootContactId = 101, ChatwootConversationId = 202, ChatwootInboxId = 1, ChatwootSyncStatus = 'synced', ChatwootLastSyncAt = SYSUTCDATETIME(), ChatwootLastError = NULL WHERE Id = <leadId>;`
6. Reabrir o detalhe do lead.
7. Confirmar preenchimento dos campos `Sync Chatwoot`, `Ultima sync Chatwoot`, `Contato Chatwoot`, `Conversa Chatwoot` e `Inbox Chatwoot`.
8. Chamar `/admin/funil/lead/<leadId>/json` e validar o bloco `chatwoot`.
9. Executar a suite `SqlAdminKanbanServiceChatwootPersistenceTests` em ambiente com SQL Server de teste disponivel.

### Troubleshooting

- `Invalid column name 'Chatwoot...'`: a aplicacao iniciou contra base antiga sem permissao para `ALTER TABLE`; revisar permissao DDL do usuario e reiniciar a aplicacao.
- Campo de Chatwoot vazio no modal mesmo apos atualizar o banco: validar se a publicacao levou `Areas/Admin/Controllers/KanbanController.cs` e `Areas/Admin/Views/Kanban/Index.cshtml`.
- Teste automatizado marcado como `Skipped`: validar se o host possui `MSSQLLocalDB` funcional ou definir `CPMFULL_SQLSERVER_TEST_MASTER_CONNECTION` para um SQL Server de teste acessivel.

## Integracao Chatwoot - sincronizacao ativa do lead

### Objetivo desta etapa

- Criar ou reaproveitar contato no Chatwoot, abrir conversa no inbox correto e permitir reprocessamento manual de leads ja existentes no funil.

### Comportamento esperado

- Ao criar ou editar um lead com telefone ou e-mail valido, o CPM Full deve tentar sincronizar automaticamente com o Chatwoot.
- Leads do funil `clientes` devem usar `ClientsInboxId`; leads do funil `prestadores` devem usar `ProvidersInboxId`.
- O fluxo deve procurar contato existente por `identifier`, e-mail e telefone antes de criar novo contato.
- Quando o contato existir sem vinculo ao inbox do funil atual, o sistema deve criar `contact_inbox` para reutilizar o mesmo contato.
- O contato sincronizado deve receber `custom_attributes` operacionais do CPM e labels gerenciadas pelo prefixo `cpm_`, preservando labels manuais fora desse prefixo.
- O contato e a conversa devem espelhar o canal de origem do lead em atributos estruturados do CPM (`CPM Canal de Origem` e `CPM Canal de Origem Slug`), preservando o valor bruto em `additional_attributes.source`.
- Leads `Source = Telegram` podem sincronizar mesmo sem telefone/e-mail quando houver `TelegramChatId`, `ChatbotConversationId` ou `ChannelConversationId` validos no vinculo tecnico.
- Quando o lead ainda nao possuir `ChatwootConversationId`, o sistema deve criar a conversa e registrar uma primeira mensagem privada com o resumo operacional do lead.
- Em falha externa, o lead local continua salvo e os campos `ChatwootSyncStatus`/`ChatwootLastError` devem refletir o erro sem quebrar o Kanban.
- O modal de detalhe do lead deve oferecer o botao `Sincronizar Chatwoot` para reprocessar leads antigos ou falhas anteriores.
- Quando existir `ChatwootConversationId`, o modal deve exibir o atalho `Abrir no Chatwoot` para navegar direto para a conversa correta.

### Checklist de QA

1. Criar um lead novo no funil de clientes com telefone e e-mail validos.
2. Confirmar retorno do fluxo sem erro funcional na tela.
3. Abrir o detalhe do lead e validar `Sync Chatwoot = Sincronizado`.
4. Confirmar que `Contato Chatwoot`, `Conversa Chatwoot` e `Inbox Chatwoot` estao preenchidos.
5. Entrar em `https://chatwoot.consertapramim.com` e validar o contato/conversa no inbox `CPM Clientes`.
6. Validar que a primeira mensagem da conversa foi criada como anotacao privada com resumo do lead.
7. Abrir a ficha do contato no Chatwoot e validar labels `cpm_` e atributos `CPM Lead ID`, `CPM Board Type`, `CPM Stage Name`, `CPM Stage Slug`, `CPM Canal de Origem` e `CPM Canal de Origem Slug`.
8. Editar o mesmo lead e confirmar que o fluxo reaproveita os IDs ja gravados, sem criar nova conversa.
9. Escolher um lead antigo ainda sem sync e acionar `Sincronizar Chatwoot` no modal.
10. Confirmar atualizacao imediata do status, dos IDs e do atalho `Abrir no Chatwoot` no detalhe do lead.
11. Repetir o fluxo com um lead do funil de prestadores e validar uso do inbox `CPM Prestadores`.
12. Criar um lead sem telefone e sem e-mail.
13. Confirmar que o lead local continua salvo, mas com `Sync Chatwoot = Falha` e `Ultimo erro Chatwoot` explicando a ausencia de dados minimos.
14. Criar ou reaproveitar um lead `Source = Telegram` sem telefone/e-mail, mas com `Vinculo Telegram` preenchido.
15. Confirmar que `Sync Chatwoot = Sincronizado`, que o contato do Chatwoot foi criado com identificador tecnico do bot e que a nota privada informa a ausencia temporaria de telefone/e-mail.

### Troubleshooting

- `Lead sem telefone, e-mail ou identificador Telegram valido`: para leads nao-Telegram, corrigir o cadastro e usar o botao `Sincronizar Chatwoot`; para leads Telegram, validar se o `Vinculo Telegram` possui `TelegramChatId`, `ChatbotConversationId` ou `ChannelConversationId`.
- `Chatwoot retornou erro HTTP 401`: validar token admin, proxy reverso e se o header `api_access_token` continua sendo encaminhado pelo Nginx.
- `Phone number has already been taken`: o contato pode ter sido criado manualmente sem `identifier`; validar busca por telefone/e-mail no Chatwoot e reprocessar o lead.
- O modal nao atualiza apos clicar em `Sincronizar Chatwoot`: validar o endpoint `POST /admin/funil/lead/{id}/chatwoot/sincronizar` e o anti-forgery token da pagina.
- Nova conversa nao aparece: validar `ChatwootConversationId`, `Inbox Chatwoot` e se a chamada de criacao da conversa nao falhou antes da primeira mensagem privada.
- A ficha do contato continua sem atributos: validar se a conta do Chatwoot possui as definicoes `CPM Lead ID`, `CPM Board Type`, `CPM Stage Name`, `CPM Stage Slug`, `CPM Canal de Origem` e `CPM Canal de Origem Slug` em `Settings > Custom Attributes`.

## Integracao Chatwoot - sincronizacao de etapa do Kanban

### Objetivo desta etapa

- Refletir no Chatwoot a mudanca de etapa do card no Kanban, atualizando status da conversa, labels gerenciadas pelo CPM e custom attributes operacionais.

### Comportamento esperado

- Ao mover um card entre etapas no Kanban, o CPM Full deve manter a mudanca local como fonte de verdade e tentar sincronizar a conversa correspondente no Chatwoot.
- A sincronizacao de etapa deve atualizar:
- status da conversa (`open`, `pending` ou `resolved`);
- labels gerenciadas pelo prefixo `cpm_`, preservando labels manuais nao pertencentes ao CPM;
- `custom_attributes` da conversa com `cpm_lead_id`, `cpm_board_type`, `cpm_stage_name`, `cpm_stage_slug`, `cpm_lead_source` e `cpm_lead_source_slug`.
- A sincronizacao de etapa deve registrar uma nota privada adicional na conversa do Chatwoot, para enriquecer a aba de historico do contato com o movimento realizado no funil.
- A mesma etapa atual deve ser espelhada no contato do Chatwoot, atualizando labels `cpm_` e `custom_attributes` equivalentes para facilitar busca operacional fora da conversa.
- Quando o lead ainda nao tiver conversa no Chatwoot, o fluxo de sync de etapa deve primeiro criar/reaproveitar contato e conversa, depois aplicar o mapa da etapa.
- Em falha externa, o card continua movido localmente e o lead deve registrar `ChatwootSyncStatus = failed`, `ChatwootLastError` e evento de historico correspondente.

### Mapeamento inicial aplicado

- `clientes`
- `Novo lead` -> status `open`, labels `cpm_clientes`, `cpm_clientes_novo_lead`
- `Tentativa de contato` -> status `pending`, labels `cpm_clientes`, `cpm_clientes_tentativa_de_contato`
- `Agendado` -> status `pending`, labels `cpm_clientes`, `cpm_clientes_agendado`
- `Em atendimento` -> status `open`, labels `cpm_clientes`, `cpm_clientes_em_atendimento`
- `Concluido` -> status `resolved`, labels `cpm_clientes`, `cpm_clientes_concluido`
- `Perdido` -> status `resolved`, labels `cpm_clientes`, `cpm_clientes_perdido`
- `prestadores`
- `Novo cadastro` -> status `open`, labels `cpm_prestadores`, `cpm_prestadores_novo_cadastro`
- `Primeiro contato` -> status `pending`, labels `cpm_prestadores`, `cpm_prestadores_primeiro_contato`
- `Documentacao pendente` -> status `pending`, labels `cpm_prestadores`, `cpm_prestadores_documentacao_pendente`
- `Validacao tecnica` -> status `pending`, labels `cpm_prestadores`, `cpm_prestadores_validacao_tecnica`
- `Ativo na plataforma` -> status `resolved`, labels `cpm_prestadores`, `cpm_prestadores_ativo_na_plataforma`
- `Inativo/Recusado` -> status `resolved`, labels `cpm_prestadores`, `cpm_prestadores_inativo_recusado`

### Checklist de QA

1. Garantir que o lead testado ja possua `ChatwootConversationId`.
2. Abrir o card no funil e anotar a etapa atual.
3. Mover o card para outra etapa via drag-and-drop.
4. Confirmar que a mudanca local no Kanban continua salva mesmo se houver lentidao na API externa.
5. Abrir o detalhe do lead e validar `Sync Chatwoot = Sincronizado`.
6. Confirmar novo evento de historico `Etapa sincronizada no Chatwoot`.
7. No Chatwoot, abrir a conversa correspondente e validar:
8. novo status da conversa;
9. labels `cpm_` compativeis com a etapa atual;
10. preservacao de labels manuais nao pertencentes ao prefixo `cpm_`.
11. Confirmar que a conversa recebeu uma nova nota privada descrevendo a etapa atualizada no CPM.
12. Abrir a ficha do contato e validar que labels e atributos `CPM Stage Name`/`CPM Stage Slug` e `CPM Canal de Origem` acompanharam a mesma etapa.
13. Repetir o teste com card ainda sem conversa no Chatwoot e confirmar bootstrap automatico antes da sync de etapa.

### Troubleshooting

- O card moveu, mas o Chatwoot nao refletiu a etapa: abrir o detalhe do lead e validar `Ultimo erro Chatwoot`.
- Labels manuais sumiram: revisar se houve label manual usando prefixo `cpm_`; esse prefixo esta reservado para labels gerenciadas pelo CPM.
- Status de conversa inesperado: revisar o mapa fixo em `Integrations/Chatwoot/ChatwootStageMapping.cs`.
- Falha recorrente de sync de etapa: usar `Sincronizar Chatwoot` no modal para reprocessar o lead e confirmar bootstrap de contato/conversa antes de novo drag-and-drop.
- Dificuldade para localizar a conversa apos mover o card: usar o atalho `Abrir no Chatwoot` no modal do lead para abrir diretamente `/app/accounts/{accountId}/conversations/{conversationId}`.
- A conversa mostra so a nota inicial do lead: validar se a aplicacao publicada ja contem o fluxo que cria nota privada por mudanca de etapa e repetir um novo movimento de card apos o deploy.

## Integracao Chatwoot - recepcao de webhooks no funil

### Objetivo

- Receber eventos relevantes do Chatwoot no CPM Full para atualizar `LastContactAt`, enriquecer o historico do lead e manter rastreabilidade operacional do atendimento.

### Comportamento esperado

- O endpoint publico do webhook e `POST /api/integrations/chatwoot/webhook`.
- O endpoint valida os headers `X-Chatwoot-Timestamp` e `X-Chatwoot-Signature` usando HMAC SHA-256 com prefixo `sha256=`.
- Quando o Chatwoot enviar `X-Chatwoot-Delivery`, esse valor vira a chave de idempotencia; sem esse header, o sistema usa fingerprint derivada de timestamp + assinatura.
- Todo payload aceito fica persistido em `dbo.cpm_web_chatwoot_webhook_events`, com `ProcessStatus`, `ProcessedAt` e eventual `ErrorMessage`.
- Eventos suportados nesta etapa:
  - `message_created`
  - `conversation_status_changed`
  - `conversation_updated`
- Eventos duplicados retornam `200` com `processStatus = duplicate`.
- Evento suportado sem conversa local mapeada retorna `200` com `processStatus = ignored`, sem quebrar a entrega do webhook.
- Assinatura invalida retorna `401`.
- Falha interna de processamento retorna `500`, preservando o payload bruto para diagnostico e retentativa operacional.

### Checklist operacional

1. Garantir que `Chatwoot:WebhookSecret` do `appsettings.Local.json` ou das variaveis de ambiente seja exatamente o segredo do webhook cadastrado no Chatwoot.
2. Garantir que o CPM Full publicado esteja acessivel em HTTPS no endpoint `/api/integrations/chatwoot/webhook`.
3. No Chatwoot, cadastrar o webhook apontando para a URL publica do CPM Full e habilitar ao menos:
4. `message_created`
5. `conversation_status_changed`
6. `conversation_updated`
7. Criar ou localizar um lead ja sincronizado no CPM Full, com `ChatwootConversationId` preenchido.
8. Enviar mensagem real na conversa do Chatwoot.
9. Abrir o detalhe do lead no CPM Full e confirmar novo historico `Mensagem recebida no Chatwoot` ou `Resposta enviada no Chatwoot`.
10. Confirmar atualizacao de `Ultimo contato` quando a mensagem tiver timestamp mais recente que o valor atual.
11. Alterar o status da conversa no Chatwoot para `Pending` ou `Resolved`.
12. Reabrir o detalhe do lead e confirmar historico `Status alterado no Chatwoot`.
13. Executar um replay do mesmo webhook e confirmar resposta `duplicate` sem duplicar historico local.
14. Consultar `dbo.cpm_web_chatwoot_webhook_events` e validar preenchimento de `ProviderEventId`, `EventType`, `ConversationId`, `ProcessStatus`, `ProcessedAt` e `ErrorMessage`.

### Troubleshooting

- `401` no webhook: validar se o segredo do webhook no Chatwoot corresponde exatamente a `Chatwoot:WebhookSecret`, incluindo o uso do prefixo `sha256=` na assinatura enviada.
- Webhook aceito, mas sem efeito no lead: validar se o lead possui `ChatwootConversationId` igual ao `conversation_id` do evento recebido.
- `processStatus = ignored` em todo evento: revisar se o evento esta entre os tres suportados nesta fase (`message_created`, `conversation_status_changed`, `conversation_updated`).
- Historico duplicado: validar se o Chatwoot esta enviando `X-Chatwoot-Delivery`; se nao estiver, confirmar se `X-Chatwoot-Timestamp` e `X-Chatwoot-Signature` estao chegando integrais para o fallback de idempotencia.
- `500` no webhook: consultar `dbo.cpm_web_chatwoot_webhook_events`, coluna `ErrorMessage`, e repetir a entrega do payload apos corrigir o mapeamento/local de conversa.
- `404` no destino publico do webhook: validar se o workflow `deploy-vps` da branch certa ja publicou o servico `web-cpmfull` em `https://www.consertapramim.com` e se o endpoint `GET /health` do CPM Full responde na porta `5088/6088`.
- O deploy do CPM Full ficou verde, mas o Kanban ainda mostra integracao desabilitada: chamar `GET /internal/health/chatwoot`; se a descricao vier como `Integracao Chatwoot desabilitada.`, revisar os secrets `CPMFULL_CHATWOOT_*` do environment e o passo `Write VPS env file` do job `deploy-web-cpmfull`.

## Integracao Chatwoot - fila de retentativa e reprocessamento

### Objetivo

- Garantir que falhas transientes entre CPM Full e Chatwoot nao se percam quando a API externa ou a rede estiverem indisponiveis.

### Comportamento esperado

- Falha externa durante `create/sync` do lead ou durante a sync de etapa deve enfileirar item em `dbo.cpm_web_chatwoot_sync_queue`.
- O worker `ChatwootSyncRetryWorker` deve buscar itens `queued/retrying`, marcar como `processing` e reprocessar em lote.
- Politica atual de espera:
  - enfileiramento inicial: `1 minuto`
  - apos 1a tentativa falha no worker: `5 minutos`
  - apos 2a tentativa falha: `15 minutos`
  - apos 3a tentativa falha: `1 hora`
  - tentativas seguintes: `6 horas`
- O limite atual de tentativas e `10`.
- Quando o limite e esgotado, o item deve ser marcado como `dead_letter` e o lead deve receber historico `Retentativa Chatwoot esgotada`.
- O modal do lead no Kanban deve exibir o botao `Enfileirar retentativa` para forcar reprocessamento imediato sem depender da proxima falha automatica.
- Quando uma retentativa concluir com sucesso, o historico do lead deve registrar `Retentativa Chatwoot concluida`.

### Checklist de QA

1. Garantir que `Chatwoot:Enabled=true` e que o worker esteja habilitado (`RetryWorkerEnabled=true`).
2. Simular falha externa temporaria:
3. opcao A: derrubar a conectividade da aplicacao com o Chatwoot;
4. opcao B: usar temporariamente token invalido em ambiente de QA/local.
5. Criar ou editar um lead com telefone ou e-mail valido.
6. Confirmar que o lead local continua salvo, mas com `Sync Chatwoot = Falha`.
7. Abrir o detalhe do lead e validar evento `Retentativa Chatwoot enfileirada`.
8. Consultar `dbo.cpm_web_chatwoot_sync_queue` e validar um item ativo para o `LeadId`.
9. Restaurar a conectividade/configuracao do Chatwoot.
10. Aguardar o worker ou clicar em `Enfileirar retentativa` no modal do lead.
11. Confirmar mudanca do item da fila para `processed` e historico `Retentativa Chatwoot concluida`.
12. Repetir o teste com falha permanente de dados minimos (lead sem telefone e sem e-mail).
13. Confirmar que o lead falha localmente, mas a fila nao fica em looping infinito; quando reprocessado pelo worker, o item deve terminar em `dead_letter`.

### Troubleshooting

- Item nao sai de `queued`: validar se o processo publicado esta com `RetryWorkerEnabled=true` e se o host iniciou `ChatwootSyncRetryWorker`.
- Item nao e adquirido pelo worker: validar `NextAttemptAt` em UTC e se o horario do servidor esta sincronizado.
- Mesmo lead gera muitos itens: revisar se o indice unico `UX_cpm_web_chatwoot_sync_queue_active` existe na base.
- `dead_letter` recorrente: abrir o detalhe do lead, revisar `Ultimo erro Chatwoot` e corrigir causa raiz antes de acionar nova retentativa manual.
- Botao `Enfileirar retentativa` falha no modal: validar o endpoint `POST /admin/funil/lead/{id}/chatwoot/retentativa` e o anti-forgery token da pagina.

## Integracao Chatwoot - backfill incremental do backlog

### Objetivo

- Sincronizar leads legados que ainda nao possuem `ChatwootConversationId`, sem interromper o uso do funil e sem abrir conversas duplicadas no Chatwoot.

### Comportamento esperado

- O Kanban deve exibir o botao `Backfill Chatwoot` no cabecalho dos funis `clientes` e `prestadores`.
- O modal de backfill deve permitir:
  - executar somente no funil atual ou em `Clientes e prestadores`;
  - configurar `Tamanho do lote` entre `1` e `200`;
  - informar `Comecar apos o Lead ID` para override manual do checkpoint;
  - rodar `dry-run` sem criar/alterar contatos e conversas.
- O checkpoint deve ficar persistido em `dbo.cpm_web_chatwoot_backfill_checkpoints`, por escopo:
  - `board:clientes`
  - `board:prestadores`
  - `all`
- O backfill deve selecionar apenas leads ativos sem `ChatwootConversationId`.
- Quando o lead ja tiver contato no Chatwoot e esse contato possuir conversa no inbox correto, o CPM Full deve reaproveitar essa conversa em vez de abrir uma nova.
- A execucao real deve atualizar o checkpoint a cada lead processado, preservando retomada incremental em lotes curtos.
- O resumo final do modal deve exibir:
  - total selecionado;
  - sucesso;
  - falha;
  - pendente.

### Checklist de QA

1. Acessar `/admin/funil/clientes` ou `/admin/funil/prestadores`.
2. Abrir `Backfill Chatwoot`.
3. Rodar primeiro com `Executar apenas dry-run` marcado.
4. Confirmar que o modal exibe resumo com `Total selecionado`, `Sucesso`, `Falha` e `Pendente`.
5. Validar que o `dry-run` nao cria novos eventos de historico nos leads nem altera `ChatwootConversationId`.
6. Desmarcar `dry-run` e executar lote pequeno.
7. Confirmar que leads elegiveis passam a receber `ChatwootContactId`/`ChatwootConversationId`.
8. Confirmar que o resumo retorna `Ultimo Lead ID processado`.
9. Consultar `dbo.cpm_web_chatwoot_backfill_checkpoints` e validar atualizacao do escopo usado.
10. Reexecutar o backfill sem informar `Comecar apos o Lead ID`.
11. Confirmar que a execucao continua a partir do checkpoint salvo.
12. Informar manualmente `Comecar apos o Lead ID` com valor maior que o checkpoint salvo.
13. Confirmar que o override manual prevalece so para aquela execucao.
14. Validar em lead que ja tinha contato e conversa no Chatwoot que nenhuma conversa duplicada foi criada no inbox correspondente.

### Troubleshooting

- `Integracao com Chatwoot desabilitada no ambiente atual.`: use `dry-run` para diagnostico e confirme os secrets `CPMFULL_CHATWOOT_*` do ambiente publicado antes da execucao real.
- O resumo mostra muitos `Pendente`: revisar se os erros foram enfileirados para a fila de retentativa e abrir o detalhe do lead para conferir `Ultimo erro Chatwoot`.
- O backfill parece ignorar leads antigos: consultar `dbo.cpm_web_chatwoot_backfill_checkpoints` e conferir se o escopo ja esta adiantado; use `Comecar apos o Lead ID` para override controlado.
- Conversa duplicada apareceu: revisar se o contato possuia conversa no mesmo inbox do funil; o reaproveitamento ocorre por `contact_id + inbox_id`.
- Dry-run trouxe lead como falha: normalmente indica ausencia de telefone e e-mail validos no cadastro do lead.

## Integracao Chatwoot - observabilidade e diagnostico no Kanban

### Objetivo

- Dar visibilidade operacional imediata sobre sync, fila, dead-letter e erros recentes do Chatwoot sem sair do painel admin do funil.

### Comportamento esperado

- Toda requisicao web do CPM Full deve responder com header `X-Correlation-ID`.
- Chamadas HTTP ao Chatwoot, webhook inbound, retentativas do worker e backfill devem reutilizar ou gerar `CorrelationId` estruturado nos logs.
- O cabecalho do Kanban deve exibir contadores locais de:
  - `Sincronizados`
  - `Pendentes`
  - `Falhas`
- Cada card do funil deve exibir badge de status do Chatwoot:
  - `Sincronizado`
  - `Pendente`
  - `Falha`
  - `Ignorado`
  - `Desabilitado`
  - `Ainda nao sincronizado`
- O cabecalho do funil deve expor o botao `Diagnostico Chatwoot`.
- O diagnostico deve abrir em drawer lateral (`offcanvas`) com filtros de:
  - `Escopo`
  - `Limite por lista`
- O drawer deve exibir:
  - resumo com `Leads monitorados`, `Sincronizados`, `Pendentes`, `Falhas`, `Fila ativa` e `Dead-letter`;
  - tabela de falhas recentes;
  - tabela de fila/dead-letter recentes.
- Cada linha do diagnostico deve permitir:
  - `Ver lead`
  - `Reprocessar`
  - `Abrir no Chatwoot` quando houver `ChatwootConversationId`

### Checklist de QA

1. Acessar `/admin/funil/clientes` ou `/admin/funil/prestadores`.
2. Confirmar que o topo mostra os contadores `Sincronizados`, `Pendentes` e `Falhas`.
3. Validar que cards sincronizados exibem badge verde e cards com falha exibem badge vermelha.
4. Abrir o detalhe de um lead ja sincronizado, acionar `Sincronizar Chatwoot` e confirmar que o badge do card atualiza sem precisar recarregar a pagina.
5. Clicar em `Diagnostico Chatwoot`.
6. Confirmar abertura do drawer lateral com os filtros `Escopo` e `Limite por lista`.
7. Validar carregamento do resumo e das listas de falhas/fila.
8. Alterar o `Escopo` para outro funil e clicar em `Aplicar filtros`.
9. Confirmar que os cards do resumo e as tabelas passam a refletir o novo escopo.
10. Clicar em `Limpar filtros` e validar retorno ao escopo padrao da tela atual.
11. Em uma linha de falha, usar `Ver lead` e confirmar abertura do modal do lead correto.
12. Em uma linha de falha ou fila, usar `Reprocessar` e confirmar exibicao de mensagem de sucesso/erro no proprio drawer.
13. Se houver conversa vinculada, usar `Abrir no Chatwoot` e confirmar abertura da conversa em nova aba.
14. Em ambiente publicado, validar `curl -I https://www.consertapramim.com/admin/funil/clientes` autenticado no browser e conferir o header `X-Correlation-ID` no DevTools.
15. Validar em logs da aplicacao que um mesmo fluxo de sync/retry/webhook carrega o mesmo `CorrelationId`.

### Troubleshooting

- Drawer abre vazio: validar `GET /admin/funil/chatwoot/diagnostico/json` e conferir se o usuario esta autenticado no portal admin.
- Contadores do topo nao batem com a tabela: confirmar se houve alteracao manual de banco sem refresh da pagina; o resumo do topo e recalculado no DOM conforme os cards atuais.
- `Reprocessar` falha no drawer: validar `POST /admin/funil/lead/{id}/chatwoot/retentativa`, anti-forgery da pagina e o estado atual do lead.
- `Abrir no Chatwoot` nao aparece: validar `ChatwootConversationId` no detalhe do lead e se `Chatwoot:BaseUrl` esta configurado.
- Logs sem `CorrelationId`: validar se `CorrelationIdMiddleware` esta registrado logo apos `UseForwardedHeaders()` no `Program.cs`.
- Correlation id diferente entre worker/backfill: validar se o fluxo foi disparado fora da requisicao HTTP; nesses casos o CPM Full gera um novo `CorrelationId` proprio por ciclo.

## Integracao Chatwoot - seguranca e conformidade

### Objetivo

- Reduzir exposicao de dados pessoais e segredos operacionais na trilha Chatwoot do CPM Full sem perder capacidade de diagnostico.

### Comportamento esperado

- Telefone, e-mail, `ApiAccessToken`, `WebhookSecret` e cabecalhos equivalentes nao devem aparecer em claro em erros persistidos, logs tecnicos nem no drawer `Diagnostico Chatwoot`.
- O endpoint `POST /api/integrations/chatwoot/webhook` continua exigindo assinatura HMAC valida e, quando `Chatwoot:AllowedWebhookIps` estiver preenchido, tambem deve rejeitar origens fora da allowlist com `403`.
- A allowlist aceita IP individual e faixa CIDR separados por virgula, ponto e virgula ou quebra de linha.
- O payload bruto e a assinatura do webhook devem ser preservados apenas dentro da janela de retention configurada; apos esse prazo, o worker deve substituir `PayloadJson` pelo marcador de redacao e limpar `Signature`, mantendo `ProviderEventId`, `EventType`, `ConversationId`, `ProcessStatus`, `ReceivedAt`, `ProcessedAt`, `ErrorMessage` e `PayloadPurgedAt`.
- O worker `ChatwootWebhookRetentionWorker` deve executar em background somente quando `Chatwoot:WebhookPayloadCleanupEnabled=true`.

### Configuracao operacional

Campos novos da secao `Chatwoot`:

- `AllowedWebhookIps`
- `WebhookPayloadCleanupEnabled`
- `WebhookPayloadRetentionDays`
- `WebhookPayloadCleanupIntervalMinutes`

Equivalentes no deploy VPS:

- `CPMFULL_CHATWOOT_ALLOWED_WEBHOOK_IPS`
- `CPMFULL_CHATWOOT_WEBHOOK_PAYLOAD_CLEANUP_ENABLED`
- `CPMFULL_CHATWOOT_WEBHOOK_PAYLOAD_RETENTION_DAYS`
- `CPMFULL_CHATWOOT_WEBHOOK_PAYLOAD_CLEANUP_INTERVAL_MINUTES`

### Checklist de QA

1. Forcar erro operacional do Chatwoot contendo telefone, e-mail ou token e confirmar no modal/drawer do funil que os valores aparecem mascarados.
2. Chamar `/admin/funil/chatwoot/diagnostico/json` e validar que `lastError` nao expoe PII nem segredos.
3. Configurar `Chatwoot:AllowedWebhookIps` com faixa controlada e enviar webhook de origem fora da allowlist.
4. Confirmar resposta `403` com mensagem `Webhook do Chatwoot rejeitado por origem nao autorizada.`.
5. Restaurar a allowlist correta ou esvaziar o campo e confirmar que o webhook volta a ser aceito.
6. Inserir ou reaproveitar evento antigo em `dbo.cpm_web_chatwoot_webhook_events` com `ReceivedAt` anterior ao prazo de retention.
7. Executar o worker ou aguardar o intervalo configurado.
8. Confirmar que `PayloadJson` virou `{\"redacted\":true,\"reason\":\"retention\"}`, `Signature` ficou `NULL` e `PayloadPurgedAt` foi preenchido em UTC.
9. Validar `GET /internal/health/chatwoot` para garantir que a integracao continua `Healthy` apos a configuracao de seguranca.

### Runbook de rotacao de token e segredo

1. Gerar novo `Personal Access Token` no usuario admin do Chatwoot.
2. Se necessario, editar o webhook `CPM Full Funil Webhook` no Chatwoot e definir novo `WebhookSecret`.
3. Atualizar os secrets do GitHub Actions no environment correto:
4. `CPMFULL_CHATWOOT_API_ACCESS_TOKEN`
5. `CPMFULL_CHATWOOT_WEBHOOK_SECRET`
6. Se houver endurecimento por IP, revisar tambem `CPMFULL_CHATWOOT_ALLOWED_WEBHOOK_IPS`.
7. Reexecutar o deploy da branch correspondente (`dev-local` ou `main/master`).
8. Validar `GET /internal/health/chatwoot`.
9. Disparar um webhook real ou replay controlado e confirmar `processStatus = processed`.

### Troubleshooting

- `403` apos ativar allowlist: conferir o IP real que chega ao CPM Full pelo `X-Forwarded-For` do Nginx antes de endurecer `AllowedWebhookIps`.
- Payloads antigos nao sao expurgados: validar `WebhookPayloadCleanupEnabled`, `WebhookPayloadRetentionDays`, `WebhookPayloadCleanupIntervalMinutes` e se o processo publicado iniciou `ChatwootWebhookRetentionWorker`.
- Diagnostico ainda mostra PII em erro antigo: reprocessar o lead/evento ou limpar o erro historico antigo; a sanitizacao passa a valer automaticamente para novas persistencias.
- Token novo nao surtiu efeito em producao: validar se o environment do GitHub Actions correto recebeu o secret atualizado e se o deploy da branch alvo concluiu com sucesso.

## Integracao Chatwoot - deploy da VPS

### Estado atual do ambiente

- URL publica: `https://chatwoot.consertapramim.com`
- Stack Docker: `/opt/chatwoot`
- Servicos: `chatwoot-rails`, `chatwoot-sidekiq`, `chatwoot-postgres`, `chatwoot-redis`
- Proxy reverso Nginx: `/etc/nginx/sites-available/chatwoot.consertapramim.com.conf`
- Certificado TLS: `/etc/letsencrypt/live/chatwoot.consertapramim.com/fullchain.pem`
- Renovacao TLS: job global em `/etc/cron.d/profinder-certbot-renew`
- Signup publico: desabilitado apos criacao do primeiro admin (`ENABLE_ACCOUNT_SIGNUP=false`)
- Proxy da API: `underscores_in_headers on`, `ignore_invalid_headers off` e forward explicito de `api_access_token`
- Definicoes customizadas do CPM: `cpm_lead_id`, `cpm_board_type`, `cpm_stage_name`, `cpm_stage_slug`, `cpm_lead_source` e `cpm_lead_source_slug` provisionadas para `conversation_attribute` e `contact_attribute`
- Catalogo global de labels do CPM provisionado na conta: labels `cpm_clientes*` e `cpm_prestadores*` com `show_on_sidebar=true`
- Webhook da conta `1`: `CPM Full Funil Webhook`, subscriptions `message_created`, `conversation_status_changed`, `conversation_updated`, apontando para `https://www.consertapramim.com/api/integrations/chatwoot/webhook`

### Comportamento esperado

- A URL publica deve responder em HTTPS e redirecionar para `/installation/onboarding` enquanto o primeiro admin nao for criado.
- Apos a criacao do primeiro admin e o endurecimento da instancia, a raiz deve responder com a experiencia autenticada/login do Chatwoot, sem onboarding aberto.
- O container `chatwoot-rails` deve responder internamente em `127.0.0.1:3300`.
- Postgres e Redis nao devem ficar expostos publicamente; o acesso e somente pela rede Docker.
- O host deve manter `vm.overcommit_memory = 1` para estabilidade do Redis.

### Checklist operacional

1. Acessar `https://chatwoot.consertapramim.com`.
2. Confirmar abertura da tela de onboarding inicial do Chatwoot.
3. Criar o primeiro usuario admin pelo onboarding.
4. Apos concluir o onboarding, validar login no painel.
5. Na VPS, validar `cd /opt/chatwoot && docker compose ps`.
6. Confirmar os quatro servicos em `Up`.
7. Validar `curl -I https://chatwoot.consertapramim.com`.
8. Confirmar resposta `302` ou `200` valida do Chatwoot, sem erro de certificado.

### Endurecimento recomendado apos o primeiro acesso

1. Editar `/opt/chatwoot/.env`.
2. Alterar `ENABLE_ACCOUNT_SIGNUP=true` para `ENABLE_ACCOUNT_SIGNUP=false`.
3. Aplicar `cd /opt/chatwoot && docker compose up -d`.
4. Revalidar login e garantir que novos cadastros publicos nao estejam mais disponiveis.

### Endurecimento aplicado no ambiente atual

1. O primeiro admin ja foi criado no ambiente publicado.
2. O arquivo `/opt/chatwoot/.env` foi atualizado para `ENABLE_ACCOUNT_SIGNUP=false`.
3. A stack foi reaplicada com `cd /opt/chatwoot && docker compose up -d`.
4. A URL `https://chatwoot.consertapramim.com` foi revalidada com sucesso apos o restart.

### Troubleshooting

- `502 Bad Gateway`: validar `docker compose ps`, `docker logs --tail 50 chatwoot-rails` e `docker logs --tail 50 chatwoot-sidekiq`.
- `SSL certificate problem`: validar se o certificado continua presente em `/etc/letsencrypt/live/chatwoot.consertapramim.com/`.
- `erro de memoria` ou reinicio de container: validar `free -h`, `docker stats` e se `vm.overcommit_memory` continua em `1`.
- `pagina em branco` apos login: validar se o `FRONTEND_URL` em `/opt/chatwoot/.env` continua `https://chatwoot.consertapramim.com`.
- `401 Unauthorized` na Application API: validar se o Nginx do Chatwoot continua com `underscores_in_headers on;`, `ignore_invalid_headers off;` e `proxy_set_header api_access_token $http_api_access_token;`.
- Labels nao aparecem abaixo do nome do contato: validar se a conta possui o catalogo global em `Settings > Labels` e fazer refresh completo da tela do contato apos criar novas labels.
- Webhook cadastrado, mas sem entrega: validar se `https://www.consertapramim.com/api/integrations/chatwoot/webhook` deixou de responder `404` apos o deploy publico do CPM Full.

## Publicacao do CPM Full na VPS como site raiz

### Objetivo

- Publicar o `ConsertaPraMim.Web.CpmFull` em `https://www.consertapramim.com`, substituindo o antigo deploy da `Web.Landing` no slot raiz da VPS.

### Comportamento esperado

- O workflow `.github/workflows/deploy-vps.yml` deve detectar mudancas em `Backend/src/ConsertaPraMim.Web.CpmFull/**` e no artefato `Backend/docker-compose.vps.web-cpmfull.yml`.
- O job `deploy-web-cpmfull` deve publicar o container `${CONTAINER_PREFIX}-cpmfull` na porta `LANDING_PORT` (`5088` em prod, `6088` em dev).
- O healthcheck do workflow deve validar `GET /health` no CPM Full antes de liberar os jobs dependentes.
- Em `dev-local`, o healthcheck deve preferir a URL publica configurada em `PUBLIC_LANDING_URL` quando esse secret existir; sem ele, o fallback continua em `http://<VPS_PUBLIC_HOST>:6088/health`.
- O nome `PUBLIC_LANDING_URL` continua existindo por compatibilidade operacional, mas representa a URL publica do site raiz do CPM Full.
- Quando a integracao Chatwoot estiver habilitada em producao, as configuracoes devem vir por secrets `CPMFULL_CHATWOOT_*`, nunca por `appsettings.Local.json`.

### Checklist operacional

1. Confirmar que os environments do GitHub Actions possuem `PUBLIC_LANDING_URL` coerente com cada branch:
2. `production` -> `https://www.consertapramim.com`
3. `development` -> URL HML dedicada, por exemplo `https://hml.consertapramim.com`, ou manter vazio para fallback em `http://<VPS_PUBLIC_HOST>:6088`
4. Se a integracao Chatwoot precisar ficar ativa em producao, cadastrar os secrets `CPMFULL_CHATWOOT_ENABLED`, `CPMFULL_CHATWOOT_BASE_URL`, `CPMFULL_CHATWOOT_API_ACCESS_TOKEN`, `CPMFULL_CHATWOOT_ACCOUNT_ID`, `CPMFULL_CHATWOOT_CLIENTS_INBOX_ID`, `CPMFULL_CHATWOOT_PROVIDERS_INBOX_ID` e `CPMFULL_CHATWOOT_WEBHOOK_SECRET`.
5. Se a trilha de seguranca/conformidade for usada em runtime, cadastrar tambem `CPMFULL_CHATWOOT_ALLOWED_WEBHOOK_IPS`, `CPMFULL_CHATWOOT_WEBHOOK_PAYLOAD_CLEANUP_ENABLED`, `CPMFULL_CHATWOOT_WEBHOOK_PAYLOAD_RETENTION_DAYS` e `CPMFULL_CHATWOOT_WEBHOOK_PAYLOAD_CLEANUP_INTERVAL_MINUTES`.
6. Executar deploy pela branch desejada (`main/master` para producao, `dev-local` para homologacao).
7. Acompanhar no workflow os jobs `deploy-web-cpmfull` e `health-web-cpmfull`.
8. Na VPS, validar `docker ps --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}" | grep cpmfull`.
9. Validar `curl -I http://127.0.0.1:5088/health` em producao ou `curl -I http://127.0.0.1:6088/health` em dev.
10. Validar a URL publica coerente com a branch:
11. producao -> `curl -I https://www.consertapramim.com`
12. homologacao -> `curl -I <PUBLIC_LANDING_URL do environment development>`
13. Validar `curl -I https://www.consertapramim.com/api/integrations/chatwoot/webhook` em producao, e a URL HML equivalente em `dev-local` quando esse endpoint estiver exposto publicamente.
14. Abrir a home publica do CPM Full e o `/admin/login` do proprio projeto para smoke test do site publicado.

### Troubleshooting

- Workflow nao dispara o deploy do site raiz: validar se a alteracao afetou `Backend/src/ConsertaPraMim.Web.CpmFull/**`, `Backend/docker/vps/Dockerfile.web.cpmfull`, `Backend/docker-compose.vps.web-cpmfull.yml` ou arquivos globais de deploy.
- `health-web-cpmfull` falha: abrir logs do container `${CONTAINER_PREFIX}-cpmfull` e validar se a connection string de SQL Server esta correta.
- `health-web-cpmfull` falha so no `dev-local`: confirmar se o secret `PUBLIC_LANDING_URL` do environment `development` aponta para a URL HML correta; se ele estiver vazio, o workflow volta a validar `http://<VPS_PUBLIC_HOST>:6088/health`.
- Site abre, mas Chatwoot fica desabilitado: conferir se os secrets `CPMFULL_CHATWOOT_*` foram cadastrados no environment correto do GitHub.
- Root domain continua mostrando a landing antiga: validar se o container legado `${CONTAINER_PREFIX}-landing` foi removido no primeiro deploy e se o Nginx continua apontando para a porta `5088`.

## Status consolidado do epic Chatwoot e proxima trilha

### Estado atual

- O epic `EPIC-CHATWOOT-001` foi encerrado em `2026-03-14` com a trilha publicada em `https://www.consertapramim.com`.
- O ambiente publicado deve responder `Healthy` em:
- `/health`
- `/internal/health/chatwoot`
- O CPM Full permanece como sistema de verdade do funil; o Chatwoot permanece como camada de atendimento humano.

### Limitacao atual conhecida

- O `ConsertaPraMim.Web.TelegramBridge` ja alimenta automaticamente os funis `clientes` e `prestadores` do CPM Full, preservando a trilha conversacional propria (`ChatbotConversations`, `ChatbotMessages`, `ChatbotContextSnapshots`, `ChatbotActionLogs`) como origem tecnica da conversa.
- O bot publicado agora pode operar por `long polling` ou `webhook` seguro, conforme `TelegramBridge:UpdateTransport`.
- O primeiro ciclo de enriquecimento operacional do bot ja cobre telefone/e-mail, qualificacao inicial com cidade/categoria/intencao e reset operacional do lead para testes recorrentes.

### Proxima evolucao documentada

- A base funcional da automacao publicada continua registrada no documento `EPIC-TELEGRAM-001 - Automacao do Bot Telegram com Funis CPM e Chatwoot`.
- O proximo ciclo de evolucao agora segue em `EPIC-TELEGRAM-002 - Enriquecimento Operacional do Bot Telegram no CPM e Chatwoot`.
- O `EPIC-TELEGRAM-002` ja concluiu `ST-095`, `ST-096`, `ST-099` e `ST-100`.
- As proximas entregas planejadas da trilha sao `ST-097 - Politica operacional de handoff entre Telegram e Chatwoot` e `ST-098 - Observabilidade de negocio do canal Telegram`.
