# Admin Portal Changelog

## Como usar

1. Concluiu uma story: mover para `STORIES/DONE/`.
2. Adicionar uma nova entrada em `Unreleased`.
3. Em release, mover blocos de `Unreleased` para uma secao versionada.

## Unreleased

- (sem itens)

## Released

- [2026-03-13] [CPMFULL-016][CPMFULL-CHATWOOT-DEPLOY-SECRETS-FIX] Deploy do CPM Full passa a publicar os secrets do Chatwoot corretamente
- Tipo: fix
- Resumo: o job `deploy-web-cpmfull` do workflow `deploy-vps` passou a escrever `CPMFULL_CHATWOOT_*` em `Backend/.env.vps`, corrigindo o falso positivo em que o deploy raiz do `ConsertaPraMim.Web.CpmFull` ficava verde, mas a aplicacao publicada continuava com `Chatwoot__Enabled=false`. O healthcheck da pipeline tambem passou a consultar `/internal/health/chatwoot` quando a integracao esta habilitada, para reprovar publicacoes em que o Chatwoot permaneceu desabilitado em runtime.
- Arquivos principais: `.github/workflows/deploy-vps.yml`, `Backend/DEPLOY_VPS.md`, `Backend/src/ConsertaPraMim.Web.CpmFull/documentacao/MANUAL_QA_OPERACAO.md`
- Risco/Impacto: alto

- [2026-03-13] [CPMFULL-015][CPMFULL-VPS-ROOT-DEPLOY-SWAP] Pipeline VPS passa a publicar o CPM Full no dominio raiz
- Tipo: feat
- Resumo: o workflow `deploy-vps`, os scripts de deploy e os artefatos Docker da VPS passaram a publicar o `ConsertaPraMim.Web.CpmFull` no slot raiz da infraestrutura, substituindo o antigo alvo `Web.Landing` na porta `5088/6088`. O projeto tambem passou a responder `GET /health`, interpretar `ForwardedHeaders` atras do Nginx, aceitar configuracao de Chatwoot via secrets `CPMFULL_CHATWOOT_*` no deploy e, em `dev-local`, preferir a `PUBLIC_LANDING_URL` especifica do environment `development` no healthcheck quando houver URL HML distinta.
- Arquivos principais: `.github/workflows/deploy-vps.yml`, `scripts/deploy/vps-deploy-service.sh`, `scripts/deploy/vps-deploy.sh`, `Backend/docker/vps/Dockerfile.web.cpmfull`, `Backend/docker-compose.vps.web-cpmfull.yml`, `Backend/docker-compose.vps.yml`, `Backend/.env.vps.example`, `Backend/DEPLOY_VPS.md`, `Backend/src/ConsertaPraMim.Web.CpmFull/Program.cs`, `Backend/src/ConsertaPraMim.Web.CpmFull/documentacao/MANUAL_QA_OPERACAO.md`
- Risco/Impacto: alto

- [2026-03-13] [CPMFULL-014][CHATWOOT-US-07-RETRY-QUEUE] Fila local de retentativa e webhook publicado para o dominio definitivo do CPM Full
- Tipo: feat
- Resumo: o `ConsertaPraMim.Web.CpmFull` passou a ter fila SQL local `cpm_web_chatwoot_sync_queue` para retentativa automatica de falhas externas com o Chatwoot, worker `ChatwootSyncRetryWorker` com backoff operacional (`1m`, `5m`, `15m`, `1h`, `6h`), limite configuravel de tentativas, limpeza de itens ativos apos sync bem-sucedida e endpoint admin `POST /admin/funil/lead/{id}/chatwoot/retentativa` com botao `Enfileirar retentativa` no modal do lead. Em paralelo, a conta publicada do Chatwoot recebeu o webhook `CPM Full Funil Webhook` apontando para `https://www.consertapramim.com/api/integrations/chatwoot/webhook`, ficando pendente apenas o deploy publico do CPM Full nesse dominio para a entrega ponta a ponta sair do `404`.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.CpmFull/Integrations/Chatwoot/ChatwootLeadSyncService.cs`, `Backend/src/ConsertaPraMim.Web.CpmFull/Integrations/Chatwoot/ChatwootSyncQueueService.cs`, `Backend/src/ConsertaPraMim.Web.CpmFull/Integrations/Chatwoot/ChatwootSyncRetryWorker.cs`, `Backend/src/ConsertaPraMim.Web.CpmFull/Integrations/Chatwoot/IChatwootSyncQueueService.cs`, `Backend/src/ConsertaPraMim.Web.CpmFull/Integrations/Chatwoot/ChatwootDtos.cs`, `Backend/src/ConsertaPraMim.Web.CpmFull/Integrations/Chatwoot/ChatwootOptions.cs`, `Backend/src/ConsertaPraMim.Web.CpmFull/Integrations/Chatwoot/ChatwootOptionsValidator.cs`, `Backend/src/ConsertaPraMim.Web.CpmFull/Services/AdminKanbanModels.cs`, `Backend/src/ConsertaPraMim.Web.CpmFull/Services/IAdminKanbanService.cs`, `Backend/src/ConsertaPraMim.Web.CpmFull/Services/SqlAdminKanbanService.cs`, `Backend/src/ConsertaPraMim.Web.CpmFull/Areas/Admin/Controllers/KanbanController.cs`, `Backend/src/ConsertaPraMim.Web.CpmFull/Areas/Admin/Views/Kanban/Index.cshtml`, `Backend/src/ConsertaPraMim.Web.CpmFull/Program.cs`, `Backend/src/ConsertaPraMim.Web.CpmFull/appsettings.json`, `Backend/src/ConsertaPraMim.Web.CpmFull/documentacao/EPIC_CHATWOOT_FUNIS_CPM.md`, `Backend/src/ConsertaPraMim.Web.CpmFull/documentacao/MANUAL_QA_OPERACAO.md`, `Backend/tests/ConsertaPraMim.Tests.Unit/Integrations/Chatwoot/ChatwootLeadSyncServiceTests.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/Integrations/Chatwoot/ChatwootOptionsValidatorTests.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/Integrations/Chatwoot/ChatwootSyncRetryWorkerTests.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/SqlAdminKanbanServiceChatwootPersistenceTests.cs`
- Risco/Impacto: medio

- [2026-03-13] [CPMFULL-013][CHATWOOT-US-06-WEBHOOK-INBOUND] Recepcao de webhooks do Chatwoot para historico e ultimo contato do funil
- Tipo: feat
- Resumo: o `ConsertaPraMim.Web.CpmFull` passou a expor o endpoint `POST /api/integrations/chatwoot/webhook`, validando assinatura HMAC do Chatwoot com `WebhookSecret`, persistindo o payload bruto em `cpm_web_chatwoot_webhook_events`, aplicando idempotencia por `X-Chatwoot-Delivery` com fallback para fingerprint assinado, e atualizando `LastContactAt` e historico PT-BR do lead para eventos `message_created`, `conversation_status_changed` e `conversation_updated`.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.CpmFull/Controllers/Api/Integrations/ChatwootWebhookController.cs`, `Backend/src/ConsertaPraMim.Web.CpmFull/Integrations/Chatwoot/ChatwootWebhookService.cs`, `Backend/src/ConsertaPraMim.Web.CpmFull/Integrations/Chatwoot/ChatwootWebhookDtos.cs`, `Backend/src/ConsertaPraMim.Web.CpmFull/Integrations/Chatwoot/IChatwootWebhookService.cs`, `Backend/src/ConsertaPraMim.Web.CpmFull/Program.cs`, `Backend/src/ConsertaPraMim.Web.CpmFull/Services/AdminKanbanModels.cs`, `Backend/src/ConsertaPraMim.Web.CpmFull/Services/IAdminKanbanService.cs`, `Backend/src/ConsertaPraMim.Web.CpmFull/Services/SqlAdminKanbanService.cs`, `Backend/src/ConsertaPraMim.Web.CpmFull/Areas/Admin/Controllers/KanbanController.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/Integrations/Chatwoot/ChatwootWebhookServiceTests.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/SqlAdminKanbanServiceChatwootPersistenceTests.cs`, `Backend/src/ConsertaPraMim.Web.CpmFull/documentacao/EPIC_CHATWOOT_FUNIS_CPM.md`, `Backend/src/ConsertaPraMim.Web.CpmFull/documentacao/MANUAL_QA_OPERACAO.md`, `Documentacao/ADMIN_PORTAL/CHANGELOG/CHANGELOG.md`
- Risco/Impacto: medio

- [2026-03-13] [CPMFULL-012][CHATWOOT-LEAD-SOURCE-PROJECTION] Projecao do canal de origem do lead no Chatwoot
- Tipo: feat
- Resumo: o `ConsertaPraMim.Web.CpmFull` passou a normalizar a `Fonte` do lead para atributos estruturados do CPM no Chatwoot, espelhando `CPM Canal de Origem` e `CPM Canal de Origem Slug` tanto no contato quanto na conversa, sem perder o valor bruto original em `additional_attributes.source`. A conta publicada tambem recebeu as definicoes `cpm_lead_source` e `cpm_lead_source_slug` para `conversation_attribute` e `contact_attribute`, e o contato/conversa do Ricardo foram atualizados para validacao imediata.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.CpmFull/Integrations/Chatwoot/ChatwootLeadSourceMapping.cs`, `Backend/src/ConsertaPraMim.Web.CpmFull/Integrations/Chatwoot/ChatwootLeadSyncService.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/Integrations/Chatwoot/ChatwootLeadSyncServiceTests.cs`, `Backend/src/ConsertaPraMim.Web.CpmFull/documentacao/EPIC_CHATWOOT_FUNIS_CPM.md`, `Backend/src/ConsertaPraMim.Web.CpmFull/documentacao/MANUAL_QA_OPERACAO.md`, `Documentacao/ADMIN_PORTAL/CHANGELOG/CHANGELOG.md`
- Risco/Impacto: baixo

- [2026-03-13] [CPMFULL-011][CHATWOOT-LABEL-CATALOG-SEED] Provisionamento do catalogo global de labels do CPM no Chatwoot
- Tipo: fix
- Resumo: a conta `1` do `chatwoot.consertapramim.com` passou a ter o catalogo global das labels `cpm_clientes*` e `cpm_prestadores*`, todas com `show_on_sidebar=true`, eliminando a ausencia de labels cadastradas na conta e preparando a UI do Chatwoot para exibir as marcacoes operacionais do CPM abaixo do nome do contato e da conversa.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.CpmFull/documentacao/MANUAL_QA_OPERACAO.md`, `Documentacao/ADMIN_PORTAL/CHANGELOG/CHANGELOG.md`
- Risco/Impacto: baixo

- [2026-03-13] [CPMFULL-010][CHATWOOT-STAGE-HISTORY-NOTES] Notas privadas de historico por mudanca de etapa no Chatwoot
- Tipo: fix
- Resumo: a sincronizacao de etapa do Kanban no `ConsertaPraMim.Web.CpmFull` passou a registrar uma nota privada adicional na conversa do Chatwoot sempre que o card muda de etapa, enriquecendo a aba de historico do contato com a trilha do funil sem depender apenas da nota inicial de recepcao do lead.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.CpmFull/Integrations/Chatwoot/ChatwootLeadSyncService.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/Integrations/Chatwoot/ChatwootLeadSyncServiceTests.cs`, `Backend/src/ConsertaPraMim.Web.CpmFull/documentacao/EPIC_CHATWOOT_FUNIS_CPM.md`, `Backend/src/ConsertaPraMim.Web.CpmFull/documentacao/MANUAL_QA_OPERACAO.md`, `Documentacao/ADMIN_PORTAL/CHANGELOG/CHANGELOG.md`
- Risco/Impacto: baixo

- [2026-03-13] [CPMFULL-009][CHATWOOT-CONTACT-PROJECTION-UX] Projecao de labels/atributos no contato e atalho direto para a conversa do Chatwoot
- Tipo: fix
- Resumo: o `ConsertaPraMim.Web.CpmFull` passou a espelhar a etapa atual tambem no contato do Chatwoot, sincronizando labels gerenciadas pelo prefixo `cpm_` e `custom_attributes` do contato, enquanto o modal do lead ganhou o atalho `Abrir no Chatwoot` para levar direto a conversa correta. Na conta publicada tambem foram provisionadas as definicoes `cpm_lead_id`, `cpm_board_type`, `cpm_stage_name` e `cpm_stage_slug` para `conversation_attribute` e `contact_attribute`, destravando a exibicao desses campos na UI do Chatwoot.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.CpmFull/Integrations/Chatwoot/ChatwootApiClient.cs`, `Backend/src/ConsertaPraMim.Web.CpmFull/Integrations/Chatwoot/ChatwootLeadSyncService.cs`, `Backend/src/ConsertaPraMim.Web.CpmFull/Integrations/Chatwoot/IChatwootApiClient.cs`, `Backend/src/ConsertaPraMim.Web.CpmFull/Areas/Admin/Controllers/KanbanController.cs`, `Backend/src/ConsertaPraMim.Web.CpmFull/Areas/Admin/Views/Kanban/Index.cshtml`, `Backend/tests/ConsertaPraMim.Tests.Unit/Integrations/Chatwoot/ChatwootLeadSyncServiceTests.cs`, `Backend/src/ConsertaPraMim.Web.CpmFull/documentacao/EPIC_CHATWOOT_FUNIS_CPM.md`, `Backend/src/ConsertaPraMim.Web.CpmFull/documentacao/MANUAL_QA_OPERACAO.md`, `Documentacao/ADMIN_PORTAL/CHANGELOG/CHANGELOG.md`
- Risco/Impacto: medio

- [2026-03-13] [CPMFULL-008][CHATWOOT-STAGE-SYNC] Sincronizacao da etapa do Kanban com status, labels e atributos da conversa no Chatwoot
- Tipo: feat
- Resumo: o drag-and-drop do funil do `ConsertaPraMim.Web.CpmFull` passou a sincronizar a etapa atual do lead com a conversa do Chatwoot, atualizando `conversation status`, labels gerenciadas pelo prefixo `cpm_`, `custom_attributes` operacionais e historico do lead, sem bloquear a persistencia local da mudanca no Kanban.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.CpmFull/Integrations/Chatwoot/ChatwootApiClient.cs`, `Backend/src/ConsertaPraMim.Web.CpmFull/Integrations/Chatwoot/ChatwootLeadSyncService.cs`, `Backend/src/ConsertaPraMim.Web.CpmFull/Integrations/Chatwoot/ChatwootStageMapping.cs`, `Backend/src/ConsertaPraMim.Web.CpmFull/Integrations/Chatwoot/IChatwootApiClient.cs`, `Backend/src/ConsertaPraMim.Web.CpmFull/Integrations/Chatwoot/IChatwootLeadSyncService.cs`, `Backend/src/ConsertaPraMim.Web.CpmFull/Areas/Admin/Controllers/KanbanController.cs`, `Backend/src/ConsertaPraMim.Web.CpmFull/documentacao/EPIC_CHATWOOT_FUNIS_CPM.md`, `Backend/src/ConsertaPraMim.Web.CpmFull/documentacao/MANUAL_QA_OPERACAO.md`, `Documentacao/ADMIN_PORTAL/CHANGELOG/CHANGELOG.md`
- Risco/Impacto: medio

- [2026-03-13] [CPMFULL-007][CHATWOOT-AUTO-LEAD-SYNC] Sincronizacao automatica do lead com Chatwoot no CPM Full
- Tipo: feat
- Resumo: o `ConsertaPraMim.Web.CpmFull` passou a sincronizar automaticamente os leads do Kanban com o Chatwoot durante criacao/edicao, reaproveitando contato quando existente, criando `contact_inbox` quando necessario, abrindo conversa no inbox correto, registrando mensagem privada inicial e oferecendo botao manual `Sincronizar Chatwoot` para reprocessar leads antigos ou falhas operacionais.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.CpmFull/Integrations/Chatwoot/ChatwootApiClient.cs`, `Backend/src/ConsertaPraMim.Web.CpmFull/Integrations/Chatwoot/ChatwootDtos.cs`, `Backend/src/ConsertaPraMim.Web.CpmFull/Integrations/Chatwoot/ChatwootLeadSyncService.cs`, `Backend/src/ConsertaPraMim.Web.CpmFull/Integrations/Chatwoot/IChatwootApiClient.cs`, `Backend/src/ConsertaPraMim.Web.CpmFull/Integrations/Chatwoot/IChatwootLeadSyncService.cs`, `Backend/src/ConsertaPraMim.Web.CpmFull/Areas/Admin/Controllers/KanbanController.cs`, `Backend/src/ConsertaPraMim.Web.CpmFull/Areas/Admin/Views/Kanban/Index.cshtml`, `Backend/src/ConsertaPraMim.Web.CpmFull/Program.cs`, `Backend/src/ConsertaPraMim.Web.CpmFull/Services/IAdminKanbanService.cs`, `Backend/src/ConsertaPraMim.Web.CpmFull/Services/SqlAdminKanbanService.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/Integrations/Chatwoot/ChatwootLeadSyncServiceTests.cs`, `Backend/src/ConsertaPraMim.Web.CpmFull/documentacao/EPIC_CHATWOOT_FUNIS_CPM.md`, `Backend/src/ConsertaPraMim.Web.CpmFull/documentacao/MANUAL_QA_OPERACAO.md`, `Documentacao/ADMIN_PORTAL/CHANGELOG/CHANGELOG.md`
- Risco/Impacto: medio

- [2026-03-13] [CPMFULL-006][CHATWOOT-US-02-LEAD-LINK-PERSISTENCE] Persistencia do vinculo Chatwoot no Kanban do CPM Full
- Tipo: feat
- Resumo: o funil do `ConsertaPraMim.Web.CpmFull` passou a persistir `ChatwootContactId`, `ChatwootConversationId`, `ChatwootInboxId`, `ChatwootSyncStatus`, `ChatwootLastSyncAt` e `ChatwootLastError` em `cpm_web_kanban_leads`, com DDL idempotente, indice por conversa, leitura/escrita no `SqlAdminKanbanService` e exibicao operacional desses dados no detalhe do lead do Kanban.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.CpmFull/Services/AdminKanbanModels.cs`, `Backend/src/ConsertaPraMim.Web.CpmFull/Services/IAdminKanbanService.cs`, `Backend/src/ConsertaPraMim.Web.CpmFull/Services/SqlAdminKanbanService.cs`, `Backend/src/ConsertaPraMim.Web.CpmFull/Areas/Admin/Controllers/KanbanController.cs`, `Backend/src/ConsertaPraMim.Web.CpmFull/Areas/Admin/Views/Kanban/Index.cshtml`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/SqlAdminKanbanServiceChatwootPersistenceTests.cs`, `Backend/src/ConsertaPraMim.Web.CpmFull/documentacao/EPIC_CHATWOOT_FUNIS_CPM.md`, `Backend/src/ConsertaPraMim.Web.CpmFull/documentacao/MANUAL_QA_OPERACAO.md`, `Documentacao/ADMIN_PORTAL/CHANGELOG/CHANGELOG.md`
- Risco/Impacto: medio

- [2026-03-13] [CPMFULL-005][CHATWOOT-API-TOKEN-PROXY-FIX] Correcao do proxy para autenticar a Application API do Chatwoot
- Tipo: fix
- Resumo: o proxy `Nginx` da instancia `chatwoot.consertapramim.com` foi ajustado para aceitar e encaminhar o header `api_access_token`, eliminando `401 Unauthorized` nas chamadas da Application API e permitindo que o CPM Full validasse a conectividade com os inboxes configurados.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.CpmFull/documentacao/MANUAL_QA_OPERACAO.md`, `Documentacao/ADMIN_PORTAL/CHANGELOG/CHANGELOG.md`
- Risco/Impacto: medio

- [2026-03-13] [CPMFULL-004][CHATWOOT-SIGNUP-HARDENING] Desabilitacao do signup publico apos onboarding do Chatwoot
- Tipo: fix
- Resumo: apos a criacao do primeiro admin no `Chatwoot` publicado em `chatwoot.consertapramim.com`, o ambiente foi endurecido com `ENABLE_ACCOUNT_SIGNUP=false` e reaplicacao da stack Docker, encerrando o fluxo de onboarding aberto ao publico.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.CpmFull/documentacao/MANUAL_QA_OPERACAO.md`, `Documentacao/ADMIN_PORTAL/CHANGELOG/CHANGELOG.md`
- Risco/Impacto: baixo

- [2026-03-13] [CPMFULL-003][CHATWOOT-VPS-DEPLOY] Publicacao do Chatwoot na VPS do ConsertaPraMim
- Tipo: feat
- Resumo: a instancia self-hosted do `Chatwoot` foi publicada na VPS em `https://chatwoot.consertapramim.com`, com stack Docker isolada (`rails`, `sidekiq`, `postgres`, `redis`), proxy reverso no `Nginx`, TLS via `Let's Encrypt` e onboarding inicial pronto para criacao do primeiro admin.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.CpmFull/documentacao/MANUAL_QA_OPERACAO.md`, `Documentacao/ADMIN_PORTAL/CHANGELOG/CHANGELOG.md`
- Risco/Impacto: medio

- [2026-03-13] [CPMFULL-002][CHATWOOT-BASE-CONFIG-HEALTHCHECK] Base inicial da integracao Chatwoot no CPM Full
- Tipo: feat
- Resumo: o `ConsertaPraMim.Web.CpmFull` passou a ter configuracao forte para `Chatwoot`, validacao de startup quando habilitado, cliente HTTP tipado com timeout/retentativa e endpoint interno `/internal/health/chatwoot` para diagnostico de conectividade e validacao dos inboxes configurados.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.CpmFull/Program.cs`, `Backend/src/ConsertaPraMim.Web.CpmFull/appsettings.json`, `Backend/src/ConsertaPraMim.Web.CpmFull/Integrations/Chatwoot/ChatwootOptions.cs`, `Backend/src/ConsertaPraMim.Web.CpmFull/Integrations/Chatwoot/ChatwootOptionsValidator.cs`, `Backend/src/ConsertaPraMim.Web.CpmFull/Integrations/Chatwoot/ChatwootApiClient.cs`, `Backend/src/ConsertaPraMim.Web.CpmFull/Integrations/Chatwoot/ChatwootConnectionHealthCheck.cs`, `Backend/src/ConsertaPraMim.Web.CpmFull/README.md`, `Backend/src/ConsertaPraMim.Web.CpmFull/documentacao/MANUAL_QA_OPERACAO.md`, `Backend/tests/ConsertaPraMim.Tests.Unit/Integrations/Chatwoot/ChatwootOptionsValidatorTests.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/ConsertaPraMim.Tests.Unit.csproj`
- Risco/Impacto: medio

- [2026-03-13] [CPMFULL-001][CPMFULL-HOME-WHATSAPP-SUPPORT] Botao flutuante de WhatsApp na home do CPM Full
- Tipo: feat
- Resumo: a home do `ConsertaPraMim.Web.CpmFull` passou a exibir um botao flutuante de WhatsApp com CTA de suporte, abrindo conversa direta com o numero `(13) 99689-1738` e mensagem inicial pronta, sem dependencia de CDN ou asset externo adicional.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.CpmFull/Views/Home/Index.cshtml`, `Backend/src/ConsertaPraMim.Web.CpmFull/wwwroot/css/site.css`, `Backend/src/ConsertaPraMim.Web.CpmFull/README.md`, `Backend/src/ConsertaPraMim.Web.CpmFull/documentacao/MANUAL_QA_OPERACAO.md`
- Risco/Impacto: baixo

- [2026-03-13] [GOV-005][SOLUTION-IMPORT-CPM-FULL] Importacao do projeto legado cpm-full para a solution
- Tipo: feat
- Resumo: a solution `ConsertaPraMim` passou a incluir o projeto standalone `ConsertaPraMim.Web.CpmFull` em `Backend/src`, preservando a base do `cpm-full` para migracao gradual; a importacao tambem alinhou o projeto para `net9.0`, removeu dependencia de CDN para `bootstrap-icons`/`SortableJS` e passou a suportar `appsettings.Local.json` ignorado pelo Git para uso temporario de connection string sensivel fora do versionamento.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.CpmFull/ConsertaPraMim.Web.CpmFull.csproj`, `Backend/src/ConsertaPraMim.Web.CpmFull/Program.cs`, `Backend/src/ConsertaPraMim.Web.CpmFull/appsettings.json`, `Backend/src/ConsertaPraMim.Web.CpmFull/.gitignore`, `Backend/src/ConsertaPraMim.Web.CpmFull/Views/Shared/_Layout.cshtml`, `Backend/src/ConsertaPraMim.Web.CpmFull/Areas/Admin/Views/Shared/_LayoutAdmin.cshtml`, `Backend/src/ConsertaPraMim.Web.CpmFull/Areas/Admin/Views/Kanban/Index.cshtml`, `Backend/ConsertaPraMim.sln`, `Backend/src/src.sln`, `Documentacao/ADMIN_PORTAL/RUNBOOKS/RUNBOOK_IMPORTACAO_CPM_FULL_GOV-005.md`
- Risco/Impacto: medio

- [2026-03-13] [GOV-004][GITIGNORE-TEMP-ARTIFACTS-CLEANUP] Limpeza dos artefatos temporarios locais exibidos no Git do Visual Studio
- Tipo: fix
- Resumo: o repositorio passou a ignorar a pasta raiz `tmp/` e arquivos `.tmp_pr_body_*.md`, eliminando a exibicao indevida de mais de 99 arquivos locais temporarios no painel Git do Visual Studio quando nao ha alteracoes reais para commit.
- Arquivos principais: `.gitignore`, `Documentacao/ADMIN_PORTAL/CHANGELOG/CHANGELOG.md`
- Risco/Impacto: baixo

- [2026-03-11] [ST-078][ADMIN-ER-DIAGRAM-AUTO-LAYOUT] Botao de auto-layout no Diagramar ER
- Tipo: feat
- Resumo: a tela `Diagramar ER` passou a expor o botao `Reaplicar auto-layout`, usando `dagre` local para reorganizar automaticamente o grafo atual do `ReactFlow` conforme dependencias entre tabelas, reduzindo empilhamento manual e melhorando leitura por recorte/contexto.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminErDiagram/Index.cshtml`, `Backend/src/ConsertaPraMim.Web.Admin/wwwroot/js/admin-er-diagram.js`, `Backend/src/ConsertaPraMim.Web.Admin/wwwroot/css/admin-er-diagram.css`, `Backend/src/ConsertaPraMim.Web.Admin/wwwroot/lib/dagre/dagre.min.js`, `Backend/src/ConsertaPraMim.Web.Admin/wwwroot/lib/dagre/LICENSE`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`, `Documentacao/ADMIN_PORTAL/STORIES/DONE/ST-078-diagramador-er-reactflow-portal-admin.md`
- Risco/Impacto: medio

- [2026-03-11] [ST-078][ADMIN-ER-DIAGRAM-REACTFLOW] Novo modulo Diagramar ER com ReactFlow
- Tipo: feat
- Resumo: o Portal Admin ganhou o menu `Diagramar ER`, uma tela dedicada para leitura ER real do schema via `ReactFlow`, usando tabelas e relacionamentos do `DbContext` com recorte por dominio/contexto, cards por tabela, foco local, `MiniMap`, `Controls` e assets locais compativeis com a CSP atual.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Admin/Controllers/AdminErDiagramController.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminErDiagram/Index.cshtml`, `Backend/src/ConsertaPraMim.Web.Admin/wwwroot/js/admin-er-diagram.js`, `Backend/src/ConsertaPraMim.Web.Admin/wwwroot/css/admin-er-diagram.css`, `Backend/src/ConsertaPraMim.Web.Admin/Views/Shared/_Layout.cshtml`, `Backend/tests/ConsertaPraMim.Tests.Unit/Controllers/AdminErDiagramControllerTests.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`, `Documentacao/ADMIN_PORTAL/STORIES/DONE/ST-078-diagramador-er-reactflow-portal-admin.md`
- Risco/Impacto: medio

- [2026-03-11] [ST-077][ADMIN-DATABASE-SCHEMA-ER-DOMAIN-CONTEXT] Recorte por dominio/contexto com preview ER por tabelas
- Tipo: feat
- Resumo: o `Mapa de Dados` passou a agrupar o inventario por dominio/contexto e ganhou seletor de estilo do preview para alternar entre `Fluxo tecnico` e `ER por tabelas`, reaproveitando o metadata real do EF Core nos recortes geral e por dominio, sem perder o foco local por tabela.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Admin/Services/AdminDatabaseSchemaService.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Models/AdminDatabaseSchemaViewModels.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminDatabaseSchema/Index.cshtml`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/AdminDatabaseSchemaServiceTests.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`, `Documentacao/ADMIN_PORTAL/STORIES/DONE/ST-077-mapa-relacional-tabelas-banco-admin.md`
- Risco/Impacto: medio

- [2026-03-11] [ST-077][ADMIN-DATABASE-SCHEMA-ER-CARD-STYLING] Estilizacao do foco por tabela em layout ER
- Tipo: feat
- Resumo: o foco por tabela do `Mapa de Dados` passou a renderizar cards visuais no estilo de diagrama ER, com cabecalho destacado, colunas alinhadas por nome/tipo, badges sutis de `PK/FK/NULL` e conectores mais limpos para leitura tecnica.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminDatabaseSchema/Index.cshtml`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`, `Documentacao/ADMIN_PORTAL/STORIES/DONE/ST-077-mapa-relacional-tabelas-banco-admin.md`
- Risco/Impacto: baixo

- [2026-03-11] [ST-077][ADMIN-DATABASE-SCHEMA-COLUMN-TYPES] Colunas e tipos exibidos no diagrama focado por tabela
- Tipo: feat
- Resumo: o `Mapa de Dados` passou a exibir no foco por tabela as colunas com tipo SQL e marcadores (`PK`/`FK`/nullability), em formato de diagrama tecnico; tambem foi corrigida a leitura de metadados de colunas no servico para evitar `0 colunas` em tabelas com schema default.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Admin/Services/AdminDatabaseSchemaService.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Models/AdminDatabaseSchemaViewModels.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminDatabaseSchema/Index.cshtml`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/AdminDatabaseSchemaServiceTests.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`
- Risco/Impacto: medio

- [2026-03-11] [ST-077][ADMIN-DATABASE-SCHEMA-SVG-MAXWIDTH] Correcao de SVG pequeno no foco por tabela
- Tipo: fix
- Resumo: o `Mapa de Dados` passou a neutralizar `max-width` inline injetado pelo Mermaid no `svg` renderizado e a limitar zoom inicial em diagramas com poucas tabelas, evitando preview encolhido ao clicar em cards de `Tabelas mapeadas`.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminDatabaseSchema/Index.cshtml`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`
- Risco/Impacto: baixo

- [2026-03-11] [ST-077][ADMIN-DATABASE-SCHEMA-TABLE-FOCUS] Cards de tabelas mapeadas com foco de diagrama por clique
- Tipo: feat
- Resumo: a lista de `Tabelas mapeadas` no `Mapa de Dados` passou a ser interativa; ao clicar em um card, a tela gera um diagrama focado na tabela selecionada e nos relacionamentos diretos (vizinhanca), com destaque visual e acao para voltar ao modo global selecionado.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminDatabaseSchema/Index.cshtml`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`, `Documentacao/ADMIN_PORTAL/STORIES/DONE/ST-077-mapa-relacional-tabelas-banco-admin.md`
- Risco/Impacto: baixo

- [2026-03-11] [ST-077][ADMIN-DATABASE-SCHEMA-INITIAL-ZOOM-CLIPPING] Correcao de clipping vertical no preview do diagrama
- Tipo: fix
- Resumo: ajustado o render base do `svg` do `Mapa de Dados` para ocupar integralmente a area do preview e recalibrado o zoom inicial (com `fit/center` antes da leitura) para evitar abertura com diagrama cortado no topo em modo macro.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminDatabaseSchema/Index.cshtml`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`
- Risco/Impacto: baixo

- [2026-03-11] [ST-077][ADMIN-DATABASE-SCHEMA-CANVAS-HEIGHT] Ajuste de altura e escala inicial do canvas no Mapa de Dados
- Tipo: fix
- Resumo: o container de preview do `Mapa de Dados` passou a usar altura efetiva maior por viewport (`height` com `clamp`) e o comportamento inicial de escala foi ajustado para melhorar legibilidade em grafos largos.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminDatabaseSchema/Index.cshtml`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`
- Risco/Impacto: baixo

- [2026-03-11] [ST-077][ADMIN-DATABASE-SCHEMA-LAYOUT-MODES] Reorganizacao do diagrama do Mapa de Dados para melhor leitura
- Tipo: feat
- Resumo: a tela `Mapa de Dados` passou a oferecer modos de visualizacao (`Visao macro por dominios`, `Visao geral` e recortes por dominio), com zoom inicial orientado para legibilidade em grafos amplos e botao de enquadramento completo, reduzindo a percepcao de diagrama linear/comprimido no canvas.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Admin/Services/AdminDatabaseSchemaService.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminDatabaseSchema/Index.cshtml`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/AdminDatabaseSchemaServiceTests.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`, `Documentacao/ADMIN_PORTAL/STORIES/DONE/ST-077-mapa-relacional-tabelas-banco-admin.md`
- Risco/Impacto: medio

- [2026-03-11] [ST-077][ADMIN-DIAGRAM-ASSET-HOTFIX] Correcao do carregamento de pan/zoom nos diagramas do Portal Admin
- Tipo: fix
- Resumo: os modulos `Diagramas Mermaid` e `Mapa de Dados` passaram a usar asset local versionado (`~/lib/svg-pan-zoom/svg-pan-zoom.min.js`) da biblioteca correta `svg-pan-zoom`, substituindo a variante CDN anterior que resultava em erro `SVG is not defined` no browser e quebrava pan/zoom.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminDiagrams/Index.cshtml`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminDatabaseSchema/Index.cshtml`, `Backend/src/ConsertaPraMim.Web.Admin/wwwroot/lib/svg-pan-zoom/svg-pan-zoom.min.js`, `Backend/src/ConsertaPraMim.Web.Admin/wwwroot/lib/svg-pan-zoom/LICENSE`
- Risco/Impacto: baixo

- [2026-03-11] [ST-077][ADMIN-DATABASE-SCHEMA-MAP] Novo modulo Mapa de Dados com diagrama relacional no Portal Admin
- Tipo: feat
- Resumo: o portal admin ganhou o modulo `Mapa de Dados`, que gera automaticamente inventario de tabelas e relacionamentos (FK) a partir do modelo EF Core e renderiza diagrama ER em Mermaid com pan/zoom, alem de tabela detalhada de constraints para QA/operacao tecnica.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Admin/Controllers/AdminDatabaseSchemaController.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Services/AdminDatabaseSchemaService.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminDatabaseSchema/Index.cshtml`, `Backend/src/ConsertaPraMim.Web.Admin/Views/Shared/_Layout.cshtml`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/AdminDatabaseSchemaServiceTests.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`
- Risco/Impacto: medio
- [2026-03-10] [ST-076][ADMIN-HML-PORTAL-LINKS] Correcao dos links de Portal Cliente/Prestador no menu admin em homologacao
- Tipo: fix
- Resumo: o resolvedor de URLs publicas do Admin passou a reconhecer hosts com prefixo de ambiente (`hml`, `dev`, `qa`, `stg`) e montar subdominios irmaos corretamente, eliminando geracao incorreta de links como `cliente.admin.consertapramim.com` e `prestador.admin.consertapramim.com` quando o acesso ocorre por `hml.admin.consertapramim.com`.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Admin/Services/AdminPublicUrlResolver.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/AdminPublicUrlResolverTests.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`
- Risco/Impacto: baixo

- [2026-03-10] [ST-075][ADMIN-APPLICATIONS-APK-CHANNEL] Links de APK da tela Aplicativos agora respeitam canal HML/PRD
- Tipo: fix
- Resumo: a tela `AdminApplications` passou a normalizar automaticamente a base de download para o diretorio do ambiente ativo (`/files/apks/hml` em `DEPLOY_PROFILE=development` e `/files/apks/prd` em `DEPLOY_PROFILE=production`), corrigindo o apontamento legado para `/files/apks` sem sufixo; tambem foram adicionados testes de regressao para os dois perfis.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Admin/Controllers/AdminApplicationsController.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/Controllers/AdminApplicationsControllerTests.cs`, `Backend/DEPLOY_VPS.md`, `Documentacao/ADMIN_PORTAL/EPICS/EPIC-031-deploy-dual-stack-dev-prod-na-mesma-vps.md`, `Documentacao/ADMIN_PORTAL/STORIES/DONE/ST-075-admin-applications-apk-por-ambiente-hml-prd.md`
- Risco/Impacto: baixo

- [2026-03-10] [ST-074][WEB-HML-FOOTER-BANNER] Rodape fixo de ambiente em homologacao nos portais web
- Tipo: feat
- Resumo: os projetos `Web.Admin`, `Web.Client`, `Web.Provider` e `Web.Landing` passaram a renderizar um rodape fixo de aviso de homologacao somente quando `DEPLOY_PROFILE=development`; o deploy VPS tambem passou a injetar `DEPLOY_PROFILE` nos servicos web para garantir comportamento consistente entre HML e PRD.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Admin/Views/Shared/_Layout.cshtml`, `Backend/src/ConsertaPraMim.Web.Client/Views/Shared/_Layout.cshtml`, `Backend/src/ConsertaPraMim.Web.Provider/Views/Shared/_Layout.cshtml`, `Backend/src/ConsertaPraMim.Web.Landing/Views/Shared/_Layout.cshtml`, `Backend/src/ConsertaPraMim.Web.Landing/wwwroot/css/site.css`, `Backend/docker-compose.vps.yml`, `Backend/docker-compose.vps.web-admin.yml`, `Backend/docker-compose.vps.web-client.yml`, `Backend/docker-compose.vps.web-provider.yml`, `Backend/docker-compose.vps.web-landing.yml`, `Backend/DEPLOY_VPS.md`, `Documentacao/ADMIN_PORTAL/EPICS/EPIC-031-deploy-dual-stack-dev-prod-na-mesma-vps.md`, `Documentacao/ADMIN_PORTAL/STORIES/DONE/ST-074-rodape-fixo-indicador-hml-portais-web.md`
- Risco/Impacto: baixo
- [2026-03-10] [ST-072][OPS-VPS-DEV-RESPECT-PUBLIC-URL-SECRETS] Workflow dev-local passa a respeitar URLs publicas HTTPS dos environments
- Tipo: fix
- Resumo: os blocos de escrita do `.env.vps` no workflow `deploy-vps` deixaram de sobrescrever `PUBLIC_*_URL` em `development`; agora o pipeline respeita os valores configurados em `development`/`production` para `PUBLIC_*_URL` e `PUBLIC_MOBILE_*_WEBVIEW_URL`, aplicando fallback para `http://<VPS_PUBLIC_HOST>:<porta>` somente quando algum secret estiver vazio.
- Arquivos principais: `.github/workflows/deploy-vps.yml`, `Backend/DEPLOY_VPS.md`
- Risco/Impacto: medio

- [2026-03-10] [ST-072][ADMIN-APPS-WEBVIEW-LINKS-PUBLIC-URL] Correcao dos links de WebView na tela Aplicativos do Admin
- Tipo: fix
- Resumo: a tela `AdminApplications` deixou de montar os links de WebView com `Context.Request.Scheme` (que em producao gerava `https://...:5181/5182/5183` e quebrava acesso) e passou a resolver URLs publicas por configuracao (`MobileWebViews:*`) com fallback explicito para HTTP por porta; o deploy VPS tambem passou a expor as variaveis `PUBLIC_MOBILE_*_WEBVIEW_URL` para override por ambiente.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Admin/Controllers/AdminApplicationsController.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminApplications/Index.cshtml`, `Backend/src/ConsertaPraMim.Web.Admin/Models/AdminApplicationsViewModels.cs`, `Backend/docker-compose.vps.web-admin.yml`, `Backend/docker-compose.vps.yml`, `Backend/.env.vps.example`, `Backend/DEPLOY_VPS.md`
- Risco/Impacto: medio

- [2026-03-10] [ST-072][OPS-VPS-PRD-PENDING-MODEL-HOTFIX] Hotfix de boot da API em producao por sincronizacao de snapshot EF
- Tipo: fix
- Resumo: o deploy da `main` voltou a falhar no healthcheck da API por `PendingModelChangesWarning` em modo estrito de `production`; para estabilizar sem risco de alteracao estrutural indevida no banco, foi adicionada a migration `SyncPendingModelChangesAfterDeployVpsRefactor` como `no-op` (apenas sincronizacao de `ModelSnapshot`) e documentado o preflight de validacao com `dotnet ef migrations has-pending-model-changes` antes de promover para `main`.
- Arquivos principais: `Backend/src/ConsertaPraMim.Infrastructure/Migrations/20260310153442_SyncPendingModelChangesAfterDeployVpsRefactor.cs`, `Backend/src/ConsertaPraMim.Infrastructure/Migrations/20260310153442_SyncPendingModelChangesAfterDeployVpsRefactor.Designer.cs`, `Backend/src/ConsertaPraMim.Infrastructure/Migrations/ConsertaPraMimDbContextModelSnapshot.cs`, `Backend/DEPLOY_VPS.md`, `Documentacao/ADMIN_PORTAL/STORIES/DONE/ST-072-deploy-branch-aware-dev-local-main-na-mesma-vps.md`
- Risco/Impacto: medio

- [2026-03-10] [ST-073][OPS-VPS-APK-PARALLEL-BUILD-HML-PRD-SEGREGATION] Otimizacao de build APK e segregacao por ambiente no fileserver
- Tipo: feat
- Resumo: o workflow `deploy-vps` passou a executar os 3 builds de APK em paralelo (`client`, `provider`, `admin`) com cache Gradle habilitado, removendo encadeamento sequencial entre apps; a publicacao no fileserver tambem foi segmentada por ambiente para eliminar sobrescrita cruzada (`dev-local` em `/files/apks/hml` e `main/master` em `/files/apks/prd`), com atualizacao dos links no resumo de deploy e no push de release.
- Arquivos principais: `.github/workflows/deploy-vps.yml`, `Backend/DEPLOY_VPS.md`, `Documentacao/ADMIN_PORTAL/EPICS/EPIC-031-deploy-dual-stack-dev-prod-na-mesma-vps.md`, `Documentacao/ADMIN_PORTAL/STORIES/DONE/ST-073-otimizacao-pipeline-apk-segregacao-hml-prd.md`
- Risco/Impacto: medio

- [2026-03-10] [ST-072][OPS-VPS-APK-METADATA-LOCAL-ENDPOINT] Publicacao de metadados de APK via endpoint interno da API
- Tipo: fix
- Resumo: os steps `Publish APK metadata` (client/provider/admin) e `Notify APK release push` (provider) passaram a usar endpoint interno da API no proprio runner da VPS (`http://127.0.0.1:<API_PORT>`), removendo dependencia de `PUBLIC_API_URL`/`VPS_PUBLIC_HOST` para chamadas internas e eliminando warning de `HTTP 000` em ambiente de producao com bind da API em `127.0.0.1`.
- Arquivos principais: `.github/workflows/deploy-vps.yml`, `Backend/DEPLOY_VPS.md`, `Documentacao/ADMIN_PORTAL/STORIES/DONE/ST-072-deploy-branch-aware-dev-local-main-na-mesma-vps.md`
- Risco/Impacto: medio

- [2026-03-10] [ST-072][OPS-VPS-APK-UPLOAD-PERMISSION-ROOT] Publicacao de APK sem falha por permissao no filebrowser
- Tipo: fix
- Resumo: os steps `Publish APK fileserver` (client/provider/admin) passaram a executar `docker exec` com `--user 0` para criacao de diretorios e ajuste de ownership/permissoes em `/srv/apks`, eliminando falha por `Operation not permitted` no `chown` sem uso de fallback silencioso; a etapa permanece estrita e deve falhar apenas em erro real de filesystem/permissao.
- Arquivos principais: `.github/workflows/deploy-vps.yml`, `Backend/DEPLOY_VPS.md`, `Documentacao/ADMIN_PORTAL/STORIES/DONE/ST-072-deploy-branch-aware-dev-local-main-na-mesma-vps.md`
- Risco/Impacto: medio

- [2026-03-10] [ST-072][OPS-VPS-CONTAINER-NAMING-PRD-HML] Padronizacao dos nomes Docker para producao e homologacao
- Tipo: fix
- Resumo: o deploy VPS passou a adotar nomes operacionais padronizados por ambiente para projetos e containers Docker, com `cpm-prd-*` em `main/master` e `cpm-hml-*` em `dev-local`; os sufixos agora refletem o dominio de negocio (`admin`, `cliente`, `prestador`, `landing`, `app-*`) e o script de deploy passou a mapear explicitamente o nome de projeto compose por servico para manter isolamento sem nomes tecnicos legados.
- Arquivos principais: `.github/workflows/deploy-vps.yml`, `scripts/deploy/vps-deploy-service.sh`, `Backend/docker-compose.vps.api.yml`, `Backend/docker-compose.vps.web-landing.yml`, `Backend/docker-compose.vps.web-admin.yml`, `Backend/docker-compose.vps.web-client.yml`, `Backend/docker-compose.vps.web-provider.yml`, `Backend/docker-compose.vps.mobile-webview-client.yml`, `Backend/docker-compose.vps.mobile-webview-provider.yml`, `Backend/docker-compose.vps.mobile-webview-admin.yml`, `Backend/docker-compose.vps.yml`, `Backend/.env.vps.example`, `Backend/DEPLOY_VPS.md`, `Documentacao/ADMIN_PORTAL/STORIES/DONE/ST-072-deploy-branch-aware-dev-local-main-na-mesma-vps.md`
- Risco/Impacto: medio

- [2026-03-09] [ST-072][OPS-VPS-DEV-TIMEOUT-BOOT-BIND] Correcao de timeout no dev-local por bind interno e boot da API
- Tipo: fix
- Resumo: os compose files web do deploy VPS passaram a publicar explicitamente `URLS` junto de `ASPNETCORE_URLS`, garantindo bind interno na porta do ambiente (`6151/6069/6140`) e eliminando timeout em `IP:porta` quando `appsettings.Development` continha portas legadas; adicionalmente, a API passou a ignorar `PendingModelChangesWarning` somente no perfil `DEPLOY_PROFILE=development`, e o compose da API agora injeta `DEPLOY_PROFILE` no container para que esse comportamento seja aplicado no `dev-local` sem relaxar o modo estrito em `production`.
- Arquivos principais: `Backend/docker-compose.vps.api.yml`, `Backend/docker-compose.vps.web-admin.yml`, `Backend/docker-compose.vps.web-client.yml`, `Backend/docker-compose.vps.web-provider.yml`, `Backend/docker-compose.vps.web-landing.yml`, `Backend/docker-compose.vps.yml`, `Backend/src/ConsertaPraMim.Infrastructure/DependencyInjection.cs`, `Backend/DEPLOY_VPS.md`, `Documentacao/ADMIN_PORTAL/STORIES/DONE/ST-072-deploy-branch-aware-dev-local-main-na-mesma-vps.md`
- Risco/Impacto: medio

- [2026-03-09] [ST-072][OPS-VPS-DEV-HEALTHCHECK-PROJECT-ISOLATION] Isolamento compose DEV/PROD e healthcheck robusto no dev-local
- Tipo: fix
- Resumo: o deploy service passou a executar `docker compose` com `-p <CONTAINER_PREFIX>-<servico>` para isolar projeto compose por ambiente e por servico, evitando colisao/remocao cruzada entre stacks `dev-local` e `main` na mesma VPS; o workflow tambem passou a fazer healthcheck por `VPS_PUBLIC_HOST` no perfil `development` e agora publica diagnostico automatico (`docker ps` + `docker logs`) quando houver falha, reduzindo tempo de analise de timeout/reset em portas DEV.
- Arquivos principais: `scripts/deploy/vps-deploy-service.sh`, `.github/workflows/deploy-vps.yml`, `Backend/DEPLOY_VPS.md`, `Documentacao/ADMIN_PORTAL/STORIES/DONE/ST-072-deploy-branch-aware-dev-local-main-na-mesma-vps.md`
- Risco/Impacto: medio
- [2026-03-09] [ST-072][OPS-VPS-APK-UPLOAD-LOCAL-RUNNER] Upload de APK sem SSH externo no pipeline
- Tipo: fix
- Resumo: os jobs `Upload APK Mobile Client/Provider/Admin` foram movidos para `self-hosted Linux` e passaram a publicar os APKs no fileserver via `docker cp` local (`filebrowser`), removendo a dependencia de acesso SSH externo na porta 22 a partir de `windows-latest` e eliminando falhas por timeout de conectividade de rede entre runner hospedado e VPS.
- Arquivos principais: `.github/workflows/deploy-vps.yml`, `Backend/DEPLOY_VPS.md`
- Risco/Impacto: medio

- [2026-03-09] [ST-072][OPS-VPS-WARNINGS-APK-METADATA-SUMMARY] Reducao de warnings opcionais no pipeline de deploy
- Tipo: fix
- Resumo: o workflow `deploy-vps` passou a resolver a URL da API de forma robusta para publicacao de metadados de APK (`PUBLIC_API_URL` com fallback para `VPS_PUBLIC_HOST`), corrigiu a montagem da URL de push de release para evitar `Invalid URI`, e moveu o push de resumo final para endpoint local da API (`127.0.0.1`); falhas desses passos opcionais agora sao registradas como `notice`, evitando ruidos de warning no run quando a API/webhook estiver indisponivel.
- Arquivos principais: `.github/workflows/deploy-vps.yml`, `Backend/DEPLOY_VPS.md`
- Risco/Impacto: baixo

- [2026-03-09] [ST-072][OPS-VPS-DEPLOY-HOTFIX-ENVFILE] Hotfix de leitura do `.env.vps` no deploy service
- Tipo: fix
- Resumo: corrigido o `vps-deploy-service.sh` para nao executar (`source`) o arquivo `.env.vps` durante resolucao de `CONTAINER_PREFIX`, evitando quebra de deploy quando secrets possuem caracteres especiais; a etapa de push de resumo no workflow tambem passou a tolerar indisponibilidade da API sem falhar o job de `summary`.
- Arquivos principais: `scripts/deploy/vps-deploy-service.sh`, `.github/workflows/deploy-vps.yml`
- Risco/Impacto: medio

- [2026-03-09] [ST-072][OPS-VPS-DEV-PROD-BRANCH-DEPLOY] Deploy branch-aware `dev-local` e `main` na mesma VPS
- Tipo: feat
- Resumo: o pipeline `deploy-vps` passou a suportar dois perfis automaticos por branch, com `dev-local` publicando stack DEV por `IP:porta` e `main/master` mantendo stack PROD por dominio/subdominios; o isolamento agora usa portas dedicadas, `CONTAINER_PREFIX`, `VOLUME_PREFIX`, `DB_NAME`, bind host por ambiente e escrita branch-aware do `.env.vps`.
- Arquivos principais: `.github/workflows/deploy-vps.yml`, `Backend/docker-compose.vps.api.yml`, `Backend/docker-compose.vps.web-landing.yml`, `Backend/docker-compose.vps.web-admin.yml`, `Backend/docker-compose.vps.web-client.yml`, `Backend/docker-compose.vps.web-provider.yml`, `Backend/docker-compose.vps.mobile-webview-client.yml`, `Backend/docker-compose.vps.mobile-webview-provider.yml`, `Backend/docker-compose.vps.mobile-webview-admin.yml`, `Backend/docker-compose.vps.yml`, `scripts/deploy/vps-deploy-service.sh`, `Backend/.env.vps.example`, `Backend/DEPLOY_VPS.md`, `Documentacao/ADMIN_PORTAL/EPICS/EPIC-031-deploy-dual-stack-dev-prod-na-mesma-vps.md`, `Documentacao/ADMIN_PORTAL/STORIES/DONE/ST-072-deploy-branch-aware-dev-local-main-na-mesma-vps.md`
- Risco/Impacto: alto

- [2026-03-09] [ST-071][ADMIN-FIRETV-BACK-TO-MENU] Botao voltar do controle retorna ao menu central
- Tipo: fix
- Resumo: nas views `Metricas da landing` e `Visao operacional`, o app Fire TV passou a interceptar o botao de voltar do controle remoto (listener nativo `@capacitor/app` com fallback por teclado/webview) para sempre retornar ao `Menu` sem encerrar o app.
- Arquivos principais: `conserta-pra-mim-firetv app/App.tsx`, `conserta-pra-mim-firetv app/package.json`, `conserta-pra-mim-firetv app/package-lock.json`, `Documentacao/ADMIN_PORTAL/RUNBOOKS/RUNBOOK_FIRE_TV_DASHBOARD_ST-066.md`
- Risco/Impacto: baixo

- [2026-03-09] [ST-060][ADMIN-LANDING-ANALYTICS-BOT-FILTER] Filtro de bots/datacenter no Analytics Landing
- Tipo: fix
- Resumo: o modulo `Analytics Landing` passou a excluir por padrao sessoes suspeitas de automacao (bot/datacenter), com novo toggle de filtro (`Incluir trafego suspeito`) no drawer para reintroduzir esse trafego somente quando necessario para investigacao.
- Arquivos principais: `Backend/src/ConsertaPraMim.Application/Services/AdminLandingAnalyticsService.cs`, `Backend/src/ConsertaPraMim.API/Controllers/AdminLandingAnalyticsController.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminLandingAnalytics/Index.cshtml`, `Backend/src/ConsertaPraMim.Web.Admin/Controllers/AdminLandingAnalyticsController.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Services/AdminOperationsApiClient.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/AdminLandingAnalyticsServiceTests.cs`
- Risco/Impacto: medio

- [2026-03-09] [ST-071][ADMIN-FIRETV-OPS-ENTER-SFX] Sinal sonoro ao entrar na visao operacional
- Tipo: feat
- Resumo: a tela `Visao Operacional` do app Fire TV passou a tocar um alerta sonoro unico no momento de entrada (`mount`), usando o asset local `public/sounds/operational-enter.mp3`, sem repetir durante polling/realtime e com fallback silencioso para eventuais restricoes de autoplay.
- Arquivos principais: `conserta-pra-mim-firetv app/components/OperationsDashboardScreen.tsx`, `conserta-pra-mim-firetv app/public/sounds/operational-enter.mp3`
- Risco/Impacto: baixo

- [2026-03-09] [ST-071][ADMIN-FIRETV-ICON-BRANDING] Icone Android TV atualizado com logo oficial
- Tipo: feat
- Resumo: o app `ConsertaPraMim TV` passou a usar a arte `so-logo-consertapramim-fundo-branco.png` como base dos recursos de launcher Android (`ic_launcher`, `ic_launcher_round` e `ic_launcher_foreground`) em todas as densidades `mipmap`, alinhando a identidade visual do app na home do Fire Stick.
- Arquivos principais: `conserta-pra-mim-firetv app/android/app/src/main/res/mipmap-mdpi/ic_launcher.png`, `conserta-pra-mim-firetv app/android/app/src/main/res/mipmap-hdpi/ic_launcher.png`, `conserta-pra-mim-firetv app/android/app/src/main/res/mipmap-xhdpi/ic_launcher.png`, `conserta-pra-mim-firetv app/android/app/src/main/res/mipmap-xxhdpi/ic_launcher.png`, `conserta-pra-mim-firetv app/android/app/src/main/res/mipmap-xxxhdpi/ic_launcher.png`
- Risco/Impacto: baixo

- [2026-03-09] [ST-071][ADMIN-FIRETV-TV-CENTERING] Centralizacao do canvas 16:9 no Fire Stick
- Tipo: fix
- Resumo: a shell fixa da app Fire TV passou a calcular `offsetX/offsetY` em runtime e aplicar o stage com `transform-origin: top left`, eliminando deslocamento lateral/vertical observado em TV real quando a viewport do WebView nao casa exatamente com o canvas base.
- Arquivos principais: `conserta-pra-mim-firetv app/App.tsx`, `conserta-pra-mim-firetv app/styles.css`
- Risco/Impacto: baixo

- [2026-03-09] [ST-071][ADMIN-FIRETV-OPS-UX] Refino visual e estabilidade operacional da visao Fire TV
- Tipo: fix
- Resumo: a visao operacional do app Fire TV recebeu ajuste fino de layout 10-foot (cards compactos, tipografia e alinhamentos), troca do grafico diario para linha responsiva, consolidacao de badges de status no topo, lista de `Ultimos servicos`, melhor ajuste de bounds/zoom no mapa, persistencia da legenda no refresh e reequilibrio das proporcoes dos cards para evitar overflow.
- Arquivos principais: `conserta-pra-mim-firetv app/components/OperationsDashboardScreen.tsx`, `conserta-pra-mim-firetv app/styles.css`, `conserta-pra-mim-firetv app/package.json`, `conserta-pra-mim-firetv app/package-lock.json`, `Documentacao/ADMIN_PORTAL/RUNBOOKS/RUNBOOK_FIRE_TV_DASHBOARD_ST-066.md`
- Risco/Impacto: medio

- [2026-03-09] [GOV-003][SHELL-WINDOWS-PADRAO] Diretrizes obrigatorias para execucao atomica e escrita deterministica no shell
- Tipo: docs
- Resumo: o `AGENTS.md` da solution passou a formalizar padrao operacional no Windows para evitar reincidencia de falhas em edicoes e automacoes (`pipeline aninhada`, escapes com backtick, comando monolitico bloqueado por politica, `spawn EPERM`, lock em `.git`), impondo execucao em etapas atomicas, validacao por etapa e escrita deterministica de arquivos.
- Arquivos principais: `AGENTS.md`
- Risco/Impacto: baixo

- [2026-03-09] [ST-070][ADMIN-FIRETV-REALTIME] Realtime, health check e atualizacao continua do Fire TV
- Tipo: feat
- Resumo: o ecossistema Fire TV passou a contar com um hub SignalR dedicado, pulse server-side configuravel e health checks configuraveis para API e portais, exibidos na nova visao operacional com resumo de latencia, conectividade e fallback de refresh por timer.
- Arquivos principais: `Backend/src/ConsertaPraMim.API/BackgroundJobs/FireTvDashboardPulseWorker.cs`, `Backend/src/ConsertaPraMim.Infrastructure/Hubs/FireTvDashboardHub.cs`, `Backend/src/ConsertaPraMim.Infrastructure/Services/FireTvDashboardHealthProbe.cs`, `Backend/src/ConsertaPraMim.API/Program.cs`, `conserta-pra-mim-firetv app/components/OperationsDashboardScreen.tsx`, `Documentacao/ADMIN_PORTAL/STORIES/DONE/ST-070-realtime-signalr-health-check-e-atualizacao-fire-tv.md`
- Risco/Impacto: medio

- [2026-03-09] [ST-069][ADMIN-FIRETV-OPS-VIEW] Menu central e segunda visao operacional no Fire TV
- Tipo: feat
- Resumo: o app `ConsertaPraMim TV` passou a abrir um menu central apos o login e ganhou uma segunda view operacional, com health strip, relogio, KPIs executivos, categorias, mapa georreferenciado, barras diarias, receita mensal, SLA e chamados cancelados em layout 10-foot inspirado no cockpit de TV.
- Arquivos principais: `Backend/src/ConsertaPraMim.API/Controllers/AdminFireTvOperationsDashboardController.cs`, `Backend/src/ConsertaPraMim.Application/Services/AdminFireTvDashboardService.cs`, `Backend/src/ConsertaPraMim.Application/DTOs/AdminFireTvDashboardDTOs.cs`, `conserta-pra-mim-firetv app/App.tsx`, `conserta-pra-mim-firetv app/components/MenuScreen.tsx`, `conserta-pra-mim-firetv app/components/OperationsDashboardScreen.tsx`, `conserta-pra-mim-firetv app/styles.css`, `Documentacao/ADMIN_PORTAL/STORIES/DONE/ST-069-menu-central-e-segunda-visao-operacional-fire-tv.md`
- Risco/Impacto: medio

- [2026-03-09] [ST-068][ADMIN-FIRETV-SCROLLMAP] Scrollmap e ranking de elementos no app Fire TV
- Tipo: feat
- Resumo: o ecossistema Fire TV da landing passou a consumir `scrollmap` por milestones e ranking dos elementos mais clicados, calculados no backend a partir da telemetria existente; o app ganhou paines 10-foot dedicados para profundidade de scroll e elementos ranqueados.
- Arquivos principais: `Backend/src/ConsertaPraMim.Application/DTOs/AdminLandingAnalyticsDTOs.cs`, `Backend/src/ConsertaPraMim.Application/Services/AdminLandingAnalyticsService.cs`, `Backend/src/ConsertaPraMim.Application/Services/AdminFireTvDashboardService.cs`, `conserta-pra-mim-firetv app/components/DashboardScreen.tsx`, `conserta-pra-mim-firetv app/styles.css`, `Documentacao/ADMIN_PORTAL/STORIES/DONE/ST-068-fire-tv-scrollmap-ranking-elementos.md`
- Risco/Impacto: medio

- [2026-03-09] [ST-067][ADMIN-FIRETV-PHASE2] Filtros comparativos e UI 10-foot no dashboard Fire TV
- Tipo: feat
- Resumo: o dashboard Fire TV passou a expor filtros de `Janela`, `Origem` e `Comparacao`, com snapshot comparativo contra periodo anterior, 8 KPIs com delta e uma UI mais legivel para TV, sustentada por parametros runtime persistidos em banco e editaveis na tela de `Configuracoes` do Admin.
- Arquivos principais: `Backend/src/ConsertaPraMim.Application/DTOs/AdminFireTvDashboardDTOs.cs`, `Backend/src/ConsertaPraMim.Application/Services/AdminFireTvDashboardService.cs`, `Backend/src/ConsertaPraMim.Infrastructure/Services/FireTvDashboardRuntimeSettings.cs`, `Backend/src/ConsertaPraMim.Application/Constants/RuntimeConfigSections.cs`, `Backend/src/ConsertaPraMim.API/Controllers/AdminFireTvDashboardController.cs`, `conserta-pra-mim-firetv app/components/DashboardScreen.tsx`, `conserta-pra-mim-firetv app/services/dashboard.ts`, `conserta-pra-mim-firetv app/types.ts`, `Documentacao/ADMIN_PORTAL/STORIES/DONE/ST-067-fire-tv-filtros-comparativos-e-ui-10-foot.md`
- Risco/Impacto: medio

- [2026-03-08] [ST-066][ADMIN-FIRETV-APK] Build padrao e instalacao do APK Fire TV
- Tipo: feat
- Resumo: o script oficial `scripts/build_apks.py` passou a gerar os artefatos `ConsertaPraMim-FireTV-debug.apk` e `ConsertaPraMim-FireTV-compat.apk`, usando por padrao `https://api.consertapramim.com`, com runbook de instalacao via `adb` no Fire Stick / Fire TV.
- Arquivos principais: `scripts/build_apks.py`, `conserta-pra-mim-firetv app/README.md`, `Documentacao/ADMIN_PORTAL/STORIES/DONE/ST-066-build-apk-instalacao-fire-stick.md`, `Documentacao/ADMIN_PORTAL/RUNBOOKS/RUNBOOK_FIRE_TV_DASHBOARD_ST-066.md`
- Risco/Impacto: medio

- [2026-03-08] [ST-065][ADMIN-FIRETV-APP] App Fire TV para acompanhamento da landing
- Tipo: feat
- Resumo: criado o app `ConsertaPraMim TV` em React + Capacitor para Fire TV / Android TV, com login admin, leitura continua dos 8 KPIs principais da landing, heatmap fase 1, top origens/localidades, sessoes recentes, auto refresh e manifesto Android TV com `LEANBACK_LAUNCHER`.
- Arquivos principais: `conserta-pra-mim-firetv app/App.tsx`, `conserta-pra-mim-firetv app/components/DashboardScreen.tsx`, `conserta-pra-mim-firetv app/components/LoginScreen.tsx`, `conserta-pra-mim-firetv app/services/auth.ts`, `conserta-pra-mim-firetv app/services/dashboard.ts`, `conserta-pra-mim-firetv app/android/app/src/main/AndroidManifest.xml`, `Documentacao/ADMIN_PORTAL/STORIES/DONE/ST-065-app-fire-tv-kpis-landing.md`
- Risco/Impacto: medio

- [2026-03-08] [ST-064][ADMIN-FIRETV-API] Endpoint e runtime config do dashboard Fire TV
- Tipo: feat
- Resumo: a API passou a expor `GET /api/admin/fire-tv/landing-dashboard`, com snapshot executivo da landing para TV, e a secao runtime `FireTvDashboard` passou a ficar persistida em `SystemSettings`, com defaults seguros e edicao pela tela de `Configuracoes` do Admin.
- Arquivos principais: `Backend/src/ConsertaPraMim.API/Controllers/AdminFireTvDashboardController.cs`, `Backend/src/ConsertaPraMim.Application/DTOs/AdminFireTvDashboardDTOs.cs`, `Backend/src/ConsertaPraMim.Application/Services/AdminFireTvDashboardService.cs`, `Backend/src/ConsertaPraMim.Infrastructure/Services/FireTvDashboardRuntimeSettings.cs`, `Backend/src/ConsertaPraMim.Application/Constants/RuntimeConfigSections.cs`, `Documentacao/ADMIN_PORTAL/STORIES/DONE/ST-064-api-runtime-config-dashboard-fire-tv.md`
- Risco/Impacto: medio

- [2026-03-08] [ST-060][ADMIN-LANDING-ANALYTICS] Analytics comportamental da landing no Portal Admin
- Tipo: feat
- Resumo: o Portal Admin passou a expor o modulo `Analytics Landing`, com menu proprio, filtros em drawer/offcanvas, KPI de sessoes/visitantes/GeoIP/heartbeat/scroll/cliques/leads, breakdown por pagina/origem/geografia/eventos, heatmap agregado fase 1 e detalhe operacional por sessao com timeline, metadados tecnicos e correlacao com lead quando existir.
- Arquivos principais: `Backend/src/ConsertaPraMim.API/Controllers/AdminLandingAnalyticsController.cs`, `Backend/src/ConsertaPraMim.Application/DTOs/AdminLandingAnalyticsDTOs.cs`, `Backend/src/ConsertaPraMim.Application/Services/AdminLandingAnalyticsService.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Controllers/AdminLandingAnalyticsController.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminLandingAnalytics/Index.cshtml`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminLandingAnalytics/Details.cshtml`, `Backend/src/ConsertaPraMim.Web.Admin/Views/Shared/_Layout.cshtml`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`, `Documentacao/ADMIN_PORTAL/STORIES/DONE/ST-060-dashboard-e-detalhe-operacional-de-analytics-da-landing.md`
- Risco/Impacto: medio

- [2026-03-08] [ST-004][LANDING-TELEMETRY-GEOIP] Telemetria fase 1 e GeoIP da landing
- Tipo: feat
- Resumo: a landing publica passou a capturar `sessionId`, heartbeat de aba visivel, marcos de scroll, cliques em elementos interativos e localidade estimada por IP, com configuracao runtime persistida em banco (`Landing Analytics`), endpoint publico de config/ingestao e base historica para heatmap fase 1 e correlacao com leads.
- Arquivos principais: `Backend/src/ConsertaPraMim.API/Controllers/LandingAnalyticsController.cs`, `Backend/src/ConsertaPraMim.Application/DTOs/LandingAnalyticsDTOs.cs`, `Backend/src/ConsertaPraMim.Application/Services/LandingAccessEventService.cs`, `Backend/src/ConsertaPraMim.Application/Services/LandingTelemetryEventService.cs`, `Backend/src/ConsertaPraMim.Domain/Entities/LandingAccessEvent.cs`, `Backend/src/ConsertaPraMim.Domain/Entities/LandingTelemetryEvent.cs`, `Backend/src/ConsertaPraMim.Infrastructure/Services/LandingAnalyticsRuntimeSettings.cs`, `Backend/src/ConsertaPraMim.Infrastructure/Services/LandingGeoIpService.cs`, `Backend/src/ConsertaPraMim.Web.Landing/Controllers/HomeController.cs`, `Backend/src/ConsertaPraMim.Web.Landing/wwwroot/js/site.js`, `Documentacao/LANDING_PAGE/STORIES/DONE/ST-004-telemetria-fase-1-e-geoip-da-landing.md`
- Risco/Impacto: medio

- [2026-03-08] [ST-059][ADMIN-LANDING-RECURRING-VISITORS] KPI de visitas com recorrencia da landing
- Tipo: feat
- Resumo: o card `Visitas` da landing na home admin passou a detalhar, alem de `Visitantes unicos`, a quantidade de `Visitantes recorrentes`, calculada por `visitorId` estavel da landing em vez de IP bruto compartilhado; com isso, a leitura do topo de funil fica mais confiavel para retorno de visitantes no periodo filtrado.
- Arquivos principais: `Backend/src/ConsertaPraMim.Application/DTOs/AdminDashboardDTOs.cs`, `Backend/src/ConsertaPraMim.Application/Services/AdminDashboardService.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminHome/Index.cshtml`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/AdminDashboardServiceTests.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`, `Documentacao/LANDING_PAGE/MANUAL_QA_OPERACAO_LANDING.md`, `Documentacao/ADMIN_PORTAL/STORIES/DONE/ST-059-kpis-visitas-cadastros-e-conversao-landing-dashboard.md`
- Risco/Impacto: baixo

- [2026-03-08] [ST-059][ADMIN-LANDING-KPIS] KPIs da landing na home do dashboard admin
- Tipo: feat
- Resumo: a landing passou a persistir cada acesso relevante (`/`, `/Cliente`, `/Prestador`) em `LandingAccessEvents` com `visitorId` estavel por navegador, e a home do portal admin passou a exibir os KPIs incrementais `Visitas`, `Cadastros Prestador`, `Cadastros Cliente` e `Taxa de Conversao`; os cards respeitam o recorte global de periodo do dashboard, `Visitas` detalha visitantes unicos e `Taxa de Conversao` detalha cadastros totais e visitantes convertidos correlacionados entre acesso e lead.
- Arquivos principais: `Backend/src/ConsertaPraMim.Domain/Entities/LandingAccessEvent.cs`, `Backend/src/ConsertaPraMim.Infrastructure/Migrations/20260308213916_AddLandingAccessEventsAnalytics.cs`, `Backend/src/ConsertaPraMim.Application/Services/LandingAccessEventService.cs`, `Backend/src/ConsertaPraMim.Application/Services/AdminDashboardService.cs`, `Backend/src/ConsertaPraMim.Web.Landing/Controllers/HomeController.cs`, `Backend/src/ConsertaPraMim.Web.Landing/wwwroot/js/site.js`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminHome/Index.cshtml`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`, `Documentacao/LANDING_PAGE/MANUAL_QA_OPERACAO_LANDING.md`, `Documentacao/ADMIN_PORTAL/STORIES/DONE/ST-059-kpis-visitas-cadastros-e-conversao-landing-dashboard.md`
- Risco/Impacto: medio

- [2026-03-08] [ST-003][LANDING-ADMIN-PUSH] Push admin para acesso publico e lead captado na landing
- Tipo: feat
- Resumo: a landing publica passou a publicar cada acesso de `/`, `/Cliente` e `/Prestador` em um webhook interno autenticado por token, e a API passou a fan-out esses eventos para admins ativos usando o barramento existente de notificacoes, cobrindo portal admin em tempo real e app admin quando houver device registrado; alem disso, a captura de leads `Cliente` e `Prestador` agora dispara notificacao administrativa com contexto comercial e link para o detalhe do lead, enquanto o endpoint interno `POST /api/internal/landing/access` permanece fora do Swagger com `ApiExplorerSettings(IgnoreApi = true)`.
- Arquivos principais: `Backend/src/ConsertaPraMim.Application/Services/LandingAdminNotificationService.cs`, `Backend/src/ConsertaPraMim.Application/DTOs/LandingAdminNotificationDTOs.cs`, `Backend/src/ConsertaPraMim.Application/Interfaces/ILandingAdminNotificationService.cs`, `Backend/src/ConsertaPraMim.Application/Services/LandingLeadService.cs`, `Backend/src/ConsertaPraMim.API/Controllers/InternalLandingNotificationsController.cs`, `Backend/src/ConsertaPraMim.Web.Landing/Controllers/HomeController.cs`, `Backend/src/ConsertaPraMim.Web.Landing/Services/LandingAdminNotificationsClient.cs`, `Backend/docker-compose.vps.web-landing.yml`, `Backend/docker-compose.vps.yml`, `Documentacao/LANDING_PAGE/MANUAL_QA_OPERACAO_LANDING.md`, `Documentacao/LANDING_PAGE/STORIES/DONE/ST-003-push-admin-para-acesso-publico-e-lead-captado-na-landing.md`
- Risco/Impacto: medio

- [2026-03-08] [LANDING-FOOTER-CLEANUP] Rodape da landing sem links operacionais
- Tipo: fix
- Resumo: o rodape da landing deixou de exibir os links `Cliente`, `Prestador`, `Admin` e `Swagger`, mantendo apenas o copyright institucional para reduzir ruido de navegacao e concentrar a jornada principal nos CTAs da home e no header.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Landing/Views/Shared/_Layout.cshtml`, `Backend/tests/ConsertaPraMim.Tests.Unit/Frontend/LandingPageRegressionTests.cs`, `Documentacao/LANDING_PAGE/MANUAL_QA_OPERACAO_LANDING.md`
- Risco/Impacto: baixo

- [2026-03-08] [LANDING-PUBLIC-URL-RESOLVER] Landing resolve URLs publicas HTTPS a partir do host publicado
- Tipo: fix
- Resumo: a landing passou a resolver `LeadCaptureUrl`, `ApiBaseUrl`, `ApiSwaggerUrl` e links de portal a partir do host real da requisicao quando a configuracao ainda trouxer `localhost` ou IP HTTP legado da VPS; com isso, o browser deixa de enviar leads para `http://187.77.48.150:5193` e passa a usar `https://api.consertapramim.com`, eliminando o erro amigavel recorrente no submit causado por destino stale no HTML publicado.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Landing/Services/LandingPublicUrlResolver.cs`, `Backend/src/ConsertaPraMim.Web.Landing/Controllers/HomeController.cs`, `Backend/src/ConsertaPraMim.Web.Landing/Views/Shared/_Layout.cshtml`, `Backend/src/ConsertaPraMim.Web.Landing/Program.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/Controllers/LandingHomeControllerTests.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/LandingPublicUrlResolverTests.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/Frontend/LandingPageRegressionTests.cs`, `Documentacao/LANDING_PAGE/MANUAL_QA_OPERACAO_LANDING.md`
- Risco/Impacto: medio

- [2026-03-08] [LANDING-LEADS-FEEDBACK] Mensagens amigaveis e confirmacao visual no envio de leads
- Tipo: fix
- Resumo: o envio dos formularios `Cliente` e `Prestador` da landing passou a traduzir falhas tecnicas de rede para mensagens amigaveis, exibir confirmacao visual `Dados enviados com sucesso!` e fechar automaticamente o modal apos submissao bem-sucedida.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Landing/wwwroot/js/site.js`, `Backend/src/ConsertaPraMim.Web.Landing/Views/Home/Index.cshtml`, `Backend/src/ConsertaPraMim.Web.Landing/wwwroot/css/site.css`, `Backend/tests/ConsertaPraMim.Tests.Unit/Frontend/LandingPageRegressionTests.cs`, `Documentacao/LANDING_PAGE/MANUAL_QA_OPERACAO_LANDING.md`
- Risco/Impacto: baixo

- [2026-03-08] [LANDING-FAVICON-LOGO] Favicon da landing com logo quadrada oficial
- Tipo: fix
- Resumo: a landing passou a usar `og-logo-consertapramim.png` tambem como favicon e `apple-touch-icon`, alinhando a identidade visual da aba do navegador com o preview social publicado em `Open Graph` e `Twitter Card`.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Landing/Views/Shared/_Layout.cshtml`, `Backend/src/ConsertaPraMim.Web.Landing/wwwroot/og-logo-consertapramim.png`, `Backend/tests/ConsertaPraMim.Tests.Unit/Frontend/LandingPageRegressionTests.cs`, `Documentacao/LANDING_PAGE/MANUAL_QA_OPERACAO_LANDING.md`
- Risco/Impacto: baixo

- [2026-03-08] [LANDING-OG-LOGO] Preview social da landing com logo quadrada oficial
- Tipo: fix
- Resumo: a landing passou a apontar `og:image` e `twitter:image` para a arte `og-logo-consertapramim.png`, baseada na logo quadrada oficial, para melhorar a visualizacao do preview no WhatsApp e outras plataformas que consomem `Open Graph`.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Landing/Controllers/HomeController.cs`, `Backend/src/ConsertaPraMim.Web.Landing/wwwroot/og-logo-consertapramim.png`, `Backend/tests/ConsertaPraMim.Tests.Unit/Controllers/LandingHomeControllerTests.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/Frontend/LandingPageRegressionTests.cs`, `Documentacao/LANDING_PAGE/MANUAL_QA_OPERACAO_LANDING.md`
- Risco/Impacto: baixo

- [2026-03-08] [LANDING-TOPBAR-BRANDING] Wordmark oficial na topbar da landing
- Tipo: feat
- Resumo: a topbar da landing passou a usar a arte oficial `logo-top-bar-consertapramim.png` como wordmark unico da marca, removendo o texto duplicado ao lado do logo e alinhando a identidade visual publicada em `www.consertapramim.com`.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Landing/Views/Shared/_Layout.cshtml`, `Backend/src/ConsertaPraMim.Web.Landing/wwwroot/css/site.css`, `Backend/src/ConsertaPraMim.Web.Landing/wwwroot/images/logo-top-bar-consertapramim.png`, `Backend/tests/ConsertaPraMim.Tests.Unit/Frontend/LandingPageRegressionTests.cs`, `Documentacao/LANDING_PAGE/MANUAL_QA_OPERACAO_LANDING.md`
- Risco/Impacto: baixo

- [2026-03-08] [LANDING-SEO-DEEPLINKS] Open Graph da landing e rotas diretas de captacao
- Tipo: feat
- Resumo: a landing publica passou a expor metadados `Open Graph` e `Twitter Card` com imagem publica `og-image.jpg`, titulo/descricao prontos para compartilhamento e URLs dedicadas `https://www.consertapramim.com/Cliente` e `https://www.consertapramim.com/Prestador` que abrem automaticamente o modal do formulario correspondente.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Landing/Controllers/HomeController.cs`, `Backend/src/ConsertaPraMim.Web.Landing/Models/LandingPageViewModel.cs`, `Backend/src/ConsertaPraMim.Web.Landing/Views/Shared/_Layout.cshtml`, `Backend/src/ConsertaPraMim.Web.Landing/wwwroot/js/site.js`, `Backend/src/ConsertaPraMim.Web.Landing/Program.cs`, `Backend/src/ConsertaPraMim.Web.Landing/wwwroot/og-image.jpg`, `Backend/tests/ConsertaPraMim.Tests.Unit/Frontend/LandingPageRegressionTests.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/Controllers/LandingHomeControllerTests.cs`, `Documentacao/LANDING_PAGE/MANUAL_QA_OPERACAO_LANDING.md`
- Risco/Impacto: medio

- [2026-03-08] [LANDING-LEADS-MODAL] Captacao da landing migrada para modal Bootstrap
- Tipo: fix
- Resumo: os formularios de lead `Cliente` e `Prestador` deixaram de ser renderizados no fim da pagina e passaram a abrir em um modal Bootstrap local, sem scroll ate `#captacao`; o link `Contato` do header agora reutiliza o mesmo fluxo por query string, mantendo a landing limpa e o CSP compativel com `script-src 'self'`.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Landing/Views/Shared/_Layout.cshtml`, `Backend/src/ConsertaPraMim.Web.Landing/Views/Home/Index.cshtml`, `Backend/src/ConsertaPraMim.Web.Landing/wwwroot/js/site.js`, `Backend/src/ConsertaPraMim.Web.Landing/wwwroot/css/site.css`, `Backend/tests/ConsertaPraMim.Tests.Unit/Frontend/LandingPageRegressionTests.cs`, `Documentacao/LANDING_PAGE/MANUAL_QA_OPERACAO_LANDING.md`
- Risco/Impacto: medio

- [2026-03-08] [LANDING-TESTEMUNHOS] Secao publica de testemunhos com clientes e prestadores
- Tipo: feat
- Resumo: a landing publica passou a exibir, logo abaixo do bloco institucional, uma secao de prova social com 20 depoimentos estaticos em PT-BR, sendo 10 de clientes e 10 de prestadores, distribuidos em duas colunas com visual proprio para reforcar confianca e previsibilidade operacional.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Landing/Views/Home/Index.cshtml`, `Backend/src/ConsertaPraMim.Web.Landing/wwwroot/css/site.css`, `Backend/tests/ConsertaPraMim.Tests.Unit/Frontend/LandingPageRegressionTests.cs`, `Documentacao/LANDING_PAGE/MANUAL_QA_OPERACAO_LANDING.md`
- Risco/Impacto: baixo

- [2026-03-08] [LANDING-LEADS-UX-VISIBILITY] Captacao da landing sem toggles e com heading visivel apenas no clique
- Tipo: fix
- Resumo: a secao de captacao da landing foi refinada para nao exibir toggles `Cliente/Prestador` acima dos formularios; o bloco `Contato` passou a ficar oculto no carregamento inicial e so aparece junto com o formulario correspondente quando um CTA principal ou o link `Contato` do header e acionado.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Landing/Views/Home/Index.cshtml`, `Backend/src/ConsertaPraMim.Web.Landing/wwwroot/js/site.js`, `Backend/src/ConsertaPraMim.Web.Landing/wwwroot/css/site.css`, `Backend/tests/ConsertaPraMim.Tests.Unit/Frontend/LandingPageRegressionTests.cs`, `Documentacao/LANDING_PAGE/MANUAL_QA_OPERACAO_LANDING.md`
- Risco/Impacto: baixo

- [2026-03-08] [LANDING-LEADS-UX-CSP] Formularios da landing ocultos no load e sem script inline
- Tipo: fix
- Resumo: a landing publica passou a respeitar o estado oculto da secao de captacao no carregamento inicial, exibindo apenas o formulario correspondente ao CTA acionado (`Cliente` ou `Prestador`); a configuracao do endpoint de captura deixou de ser injetada por `<script>` inline e passou a usar `data-*` no `body`, eliminando bloqueio de `Content-Security-Policy` com `script-src 'self'`.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Landing/Views/Shared/_Layout.cshtml`, `Backend/src/ConsertaPraMim.Web.Landing/wwwroot/js/site.js`, `Backend/src/ConsertaPraMim.Web.Landing/wwwroot/css/site.css`, `Backend/tests/ConsertaPraMim.Tests.Unit/Frontend/LandingPageRegressionTests.cs`, `Documentacao/LANDING_PAGE/MANUAL_QA_OPERACAO_LANDING.md`
- Risco/Impacto: medio

- [2026-03-08] [ST-058][ADMIN-LANDING-LEADS] Modulo administrativo para leads captados na landing
- Tipo: feat
- Resumo: o portal admin passou a ter o item de menu `Leads Landing`, com grid paginado, filtros em drawer offcanvas por origem/busca/cidade/UF/periodo, totalizadores por origem e tela de detalhe para consulta da localidade real do lead (`bairro - cidade/UF`), contexto comercial, UTM e metadados tecnicos capturados na landing; a API recebeu endpoints administrativos autenticados para listagem e detalhe desse backlog comercial.
- Arquivos principais: `Backend/src/ConsertaPraMim.API/Controllers/AdminLandingLeadsController.cs`, `Backend/src/ConsertaPraMim.Application/DTOs/AdminLandingLeadDTOs.cs`, `Backend/src/ConsertaPraMim.Application/Services/AdminLandingLeadService.cs`, `Backend/src/ConsertaPraMim.Infrastructure/Repositories/LandingLeadRepository.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Controllers/AdminLandingLeadsController.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminLandingLeads/Index.cshtml`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminLandingLeads/Details.cshtml`, `Backend/src/ConsertaPraMim.Web.Admin/Views/Shared/_Layout.cshtml`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`, `Documentacao/ADMIN_PORTAL/EPICS/EPIC-026-leads-publicos-landing-admin.md`, `Documentacao/ADMIN_PORTAL/STORIES/DONE/ST-058-gestao-admin-leads-landing.md`
- Risco/Impacto: medio

- [2026-03-08] [ST-002][LANDING-LEADS] Captura publica de leads cliente/prestador na landing
- Tipo: feat
- Resumo: os CTAs principais da landing `www.consertapramim.com` deixaram de redirecionar direto para os portais e passaram a abrir formularios ocultos no fim da pagina para captacao de leads `Cliente` e `Prestador`; a API recebeu o endpoint anonimo `POST /api/landing-leads/public`, tabela dedicada `LandingLeads`, persistencia de cidade/UF/bairro e metadados tecnicos de navegacao (`IP`, `X-Forwarded-For`, `User-Agent`, `Accept-Language`, `Referer`, host, path, query/UTM, idioma, resolucao e plataforma), com CORS/CSP/deploy alinhados para envio browser -> API em HTTPS.
- Arquivos principais: `Backend/src/ConsertaPraMim.API/Controllers/LandingLeadsController.cs`, `Backend/src/ConsertaPraMim.Application/DTOs/LandingLeadDTOs.cs`, `Backend/src/ConsertaPraMim.Application/Services/LandingLeadService.cs`, `Backend/src/ConsertaPraMim.Application/Validators/LandingLeadValidators.cs`, `Backend/src/ConsertaPraMim.Domain/Entities/LandingLead.cs`, `Backend/src/ConsertaPraMim.Infrastructure/Data/ConsertaPraMimDbContext.cs`, `Backend/src/ConsertaPraMim.Infrastructure/Migrations/20260308132932_AddLandingLeadCapture.cs`, `Backend/src/ConsertaPraMim.Web.Landing/Views/Home/Index.cshtml`, `Backend/src/ConsertaPraMim.Web.Landing/wwwroot/js/site.js`, `Backend/docker-compose.vps.yml`, `Backend/docker-compose.vps.api.yml`, `Backend/docker-compose.vps.web-landing.yml`, `Documentacao/LANDING_PAGE/MANUAL_QA_OPERACAO_LANDING.md`, `Documentacao/LANDING_PAGE/STORIES/DONE/ST-002-captura-leads-publicos-landing.md`
- Risco/Impacto: alto

- [2026-03-08] [LANDING-HOME-REFATORACAO] Refatoracao visual da home publica da landing
- Tipo: feat
- Resumo: a home publica `https://www.consertapramim.com` foi redesenhada para um layout mais direto e comercial, com header claro, hero centralizado, duas cards principais de entrada (`Para Clientes` e `Para Profissionais`), ilustracoes locais versionadas em `SVG`, secoes compactas de `Sobre`, `Contato`, `Termos`, `Privacidade` e `FAQ`, alem de footer simplificado; a entrega preserva CSP estrita sem depender de assets externos.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Landing/Views/Home/Index.cshtml`, `Backend/src/ConsertaPraMim.Web.Landing/Views/Shared/_Layout.cshtml`, `Backend/src/ConsertaPraMim.Web.Landing/wwwroot/css/site.css`, `Backend/src/ConsertaPraMim.Web.Landing/wwwroot/images/landing-client-card.svg`, `Backend/src/ConsertaPraMim.Web.Landing/wwwroot/images/landing-provider-card.svg`, `Backend/src/ConsertaPraMim.Web.Landing/Controllers/HomeController.cs`, `Documentacao/LANDING_PAGE/MANUAL_QA_OPERACAO_LANDING.md`
- Risco/Impacto: medio

- [2026-03-08] [LANDING-WWW-VPS] Landing page publica em `www.consertapramim.com`
- Tipo: feat
- Resumo: criado o projeto `ConsertaPraMim.Web.Landing` para servir a home institucional publica em `https://www.consertapramim.com`, com `healthcheck`, `robots.txt`, `sitemap.xml`, CTA para os portais existentes e deploy integrado na VPS via Docker, Nginx, Certbot, scripts de deploy e workflow seletivo do GitHub Actions; o dominio raiz `consertapramim.com` passou a ser tratado como redirect para `www`.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Landing/Program.cs`, `Backend/src/ConsertaPraMim.Web.Landing/Views/Home/Index.cshtml`, `Backend/docker/vps/Dockerfile.web.landing`, `Backend/docker-compose.vps.web-landing.yml`, `Backend/docker-compose.vps.yml`, `Backend/docker/vps/nginx.portals.https.conf.example`, `Backend/DEPLOY_VPS.md`, `.github/workflows/deploy-vps.yml`, `Documentacao/LANDING_PAGE/MANUAL_QA_OPERACAO_LANDING.md`, `Documentacao/LANDING_PAGE/STORIES/DONE/ST-001-landing-page-publica-www.md`
- Risco/Impacto: alto

- [2026-03-08] [OPS-VPS-CLIENT-MIXED-CONTENT] URL publica HTTPS da API no portal cliente
- Tipo: fix
- Resumo: o portal cliente passou a resolver a URL publica HTTPS da API por host da requisicao antes de montar o layout browser-side e o `Content-Security-Policy`, eliminando `Mixed Content` em `notificationHub` e `chatHub` quando a configuracao ainda trouxer `localhost` ou o IP HTTP legado da VPS.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Client/Services/ClientPublicUrlResolver.cs`, `Backend/src/ConsertaPraMim.Web.Client/Program.cs`, `Backend/src/ConsertaPraMim.Web.Client/Views/Shared/_Layout.cshtml`, `Backend/src/ConsertaPraMim.Web.Client/Views/ServiceRequests/Details.cshtml`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/ClientPublicUrlResolverTests.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`
- Risco/Impacto: medio

- [2026-03-08] [OPS-VPS-ADMIN-CSP] CSP do portal admin alinhado com a URL publica HTTPS da API
- Tipo: fix
- Resumo: o portal admin passou a resolver a URL publica HTTPS da API por host da requisicao antes de montar o `Content-Security-Policy`, eliminando o bloqueio de `notificationHub`/SignalR causado por `connect-src` com IP HTTP legado; a tela `AdminMonitoring` tambem passou a consumir a mesma URL publica resolvida no browser.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Admin/Program.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminMonitoring/Index.cshtml`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/AdminPublicUrlResolverTests.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`
- Risco/Impacto: medio

- [2026-03-08] [OPS-VPS-PROVIDER-MIXED-CONTENT] URL publica HTTPS da API no portal prestador
- Tipo: fix
- Resumo: o portal do prestador passou a resolver a URL publica HTTPS da API a partir do host da requisicao quando a configuracao browser ainda trouxer `localhost` ou o IP HTTP legado da VPS; a correcao elimina `Mixed Content` em `Profile`, `notificationHub` e `chatHub`, mantendo o fallback local para desenvolvimento.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Provider/Services/ProviderPublicUrlResolver.cs`, `Backend/src/ConsertaPraMim.Web.Provider/Views/Shared/_Layout.cshtml`, `Backend/src/ConsertaPraMim.Web.Provider/Views/Profile/Index.cshtml`, `Backend/src/ConsertaPraMim.Web.Provider/Program.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/ProviderPublicUrlResolverTests.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`
- Risco/Impacto: medio

- [2026-03-07] [OPS-VPS-MENU-LINKS] Links publicos HTTPS no menu lateral do portal admin
- Tipo: fix
- Resumo: os atalhos `Portal Cliente`, `Portal Prestador` e `Swagger API` do menu lateral do portal admin passaram a priorizar URLs publicas HTTPS com dominio (`cliente.consertapramim.com`, `prestador.consertapramim.com`, `api.consertapramim.com/swagger`), evitando sobrescrita por IP/porta legado vindo de configuracao runtime.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Admin/Services/AdminPublicUrlResolver.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Services/AdminPortalLinksService.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Views/Shared/_Layout.cshtml`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/AdminPublicUrlResolverTests.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`
- Risco/Impacto: medio

- [2026-03-07] [OPS-VPS-API-URL-SPLIT] Separacao entre URL interna e publica da API nos portais web
- Tipo: fix
- Resumo: os portais web da VPS passaram a usar `ApiBaseUrl` interno via rede Docker (`http://cpm-api:8080`) para chamadas server-side e `BrowserApiBaseUrl` publico (`https://api.consertapramim.com`) para chat, upload, SignalR e links Swagger no navegador, eliminando falha de autenticacao causada pelo uso do IP publico legado `http://187.77.48.150:5193` dentro dos containers.
- Arquivos principais: `Backend/docker-compose.vps.web-admin.yml`, `Backend/docker-compose.vps.web-client.yml`, `Backend/docker-compose.vps.web-provider.yml`, `Backend/docker-compose.vps.yml`, `Backend/.env.vps.example`, `Backend/src/ConsertaPraMim.Web.Admin/Program.cs`, `Backend/src/ConsertaPraMim.Web.Client/Program.cs`, `Backend/src/ConsertaPraMim.Web.Provider/Program.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Views/Shared/_Layout.cshtml`, `Backend/src/ConsertaPraMim.Web.Client/Views/Shared/_Layout.cshtml`, `Backend/src/ConsertaPraMim.Web.Provider/Views/Shared/_Layout.cshtml`, `Backend/src/ConsertaPraMim.Web.Provider/Views/Profile/Index.cshtml`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminMonitoring/Index.cshtml`, `Backend/src/ConsertaPraMim.Web.Admin/Controllers/AdminApplicationsController.cs`, `Backend/DEPLOY_VPS.md`
- Risco/Impacto: alto

- [2026-03-07] [OPS-HTTPS-VPS] Reverse proxy HTTPS para API e portais web na VPS
- Tipo: feat
- Resumo: a stack de deploy da VPS passou a suportar publicacao segura via Nginx + Certbot para API, portal admin, portal cliente e portal prestador, com `ForwardedHeaders` nos apps ASP.NET, bind local em `127.0.0.1`, URLs publicas HTTPS parametrizadas no compose e workflow ajustado para gerar `.env.vps` com os novos hosts publicos.
- Arquivos principais: `Backend/src/ConsertaPraMim.API/Program.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Program.cs`, `Backend/src/ConsertaPraMim.Web.Client/Program.cs`, `Backend/src/ConsertaPraMim.Web.Provider/Program.cs`, `Backend/docker-compose.vps.api.yml`, `Backend/docker-compose.vps.web-admin.yml`, `Backend/docker-compose.vps.web-client.yml`, `Backend/docker-compose.vps.web-provider.yml`, `Backend/docker-compose.vps.yml`, `Backend/docker/vps/nginx.portals.https.conf.example`, `Backend/DEPLOY_VPS.md`, `.github/workflows/deploy-vps.yml`
- Risco/Impacto: alto

- [2026-03-05] [ST-019][API-PROVIDER-GALLERY] Endpoint publico de fotos da galeria em Base64 por prestador
- Tipo: feat
- Resumo: adicionada a rota anonima `GET /api/provider-gallery/public/providers/{providerId}/albums/photos/base64` para retornar todas as fotos (`image/*`) dos albuns do prestador agrupadas por album, com conteudo em Base64 e contadores de fotos indisponiveis quando o arquivo fisico nao existir no storage.
- Arquivos principais: `Backend/src/ConsertaPraMim.API/Controllers/ProviderGalleryController.cs`, `Backend/src/ConsertaPraMim.Application/DTOs/ProviderGalleryDTOs.cs`, `Backend/src/ConsertaPraMim.API/Swagger/ApiEndpointDocumentationCatalog.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/Controllers/ProviderGalleryControllerTests.cs`, `Documentacao/OPERACAO_SERVICO_POS_AGENDAMENTO/RUNBOOK_QA_GALERIA_PUBLICA_BASE64_ST-019.md`, `Documentacao/DIAGRAMAS/OPERACAO_SERVICO_POS_AGENDAMENTO/ST-019-galeria-publica-base64-prestador/fluxo-galeria-publica-base64-prestador.mmd`
- Risco/Impacto: medio

- [2026-03-04] [ST-002][API-SERVICE-APPOINTMENTS] Endpoint publico para disponibilidade agregada de prestadores (15 dias)
- Tipo: feat
- Resumo: adicionada a rota anonima `GET /api/service-appointments/public/providers/slots/next-15-days` para listar, em uma unica consulta, os horarios disponiveis dos prestadores ativos nos proximos 15 dias; o retorno foi padronizado em UTC com janela `fromUtc/toUtc` e slots por prestador, reutilizando as mesmas regras de disponibilidade, bloqueios e conflitos de agenda do fluxo autenticado.
- Arquivos principais: `Backend/src/ConsertaPraMim.API/Controllers/ServiceAppointmentsController.cs`, `Backend/src/ConsertaPraMim.Application/Services/ServiceAppointmentService.cs`, `Backend/src/ConsertaPraMim.Application/DTOs/ServiceAppointmentDTOs.cs`, `Backend/src/ConsertaPraMim.Application/Interfaces/IServiceAppointmentService.cs`, `Backend/src/ConsertaPraMim.API/Swagger/ApiEndpointDocumentationCatalog.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/ServiceAppointmentServiceTests.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/Controllers/ServiceAppointmentsControllerTests.cs`, `Documentacao/AGENDA_SERVICOS_JANELAS_LEMBRETES/MANUAL_ADMIN_QA_AGENDA_ST-008.md`, `Documentacao/DIAGRAMAS/AGENDA_SERVICOS_JANELAS_LEMBRETES/ST-008-observabilidade-qa-runbook-agenda/sequencia-consulta-publica-slots-15-dias.mmd`
- Risco/Impacto: medio

- [2026-03-03] [ST-009][WEB-CLIENT] Ocultacao da secao de cancelamento quando pedido nao elegivel
- Tipo: fix
- Resumo: na tela `ServiceRequests/Details` do portal cliente, a secao/card `Cancelar pedido` passou a ser renderizada somente quando o cancelamento e permitido; para estados bloqueados (ex.: pedido concluido), a secao nao aparece mais na interface.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Client/wwwroot/js/views/service-requests/details.js`, `Documentacao/AGENDA_SERVICOS_JANELAS_LEMBRETES/MANUAL_ADMIN_QA_AGENDA_ST-008.md`
- Risco/Impacto: baixo

- [2026-03-03] [WEB-CLIENT][PAYMENTS] Correcao de timezone na exibicao de atualizacao de pagamento
- Tipo: fix
- Resumo: a tela `ServiceRequests/Details` do portal cliente passou a interpretar timestamps de pagamento como UTC de forma explicita e exibir datas/horarios no fuso de negocio `America/Sao_Paulo`, eliminando desvio de `+3h/-3h` na linha `Metodo: PIX · Atualizado`.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Client/wwwroot/js/views/service-requests/details.js`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`
- Risco/Impacto: baixo

- [2026-03-03] [WEB-CLIENT][API-PAYMENTS] Correcao definitiva da simulacao de pagamento mock (`Simular pago`)
- Tipo: fix
- Resumo: a simulacao de pagamento no portal cliente deixou de depender de segredo local duplicado (`Payments:Mock:WebhookSecret`) e passou a usar o endpoint autenticado `POST /api/payments/simulate/mock`; a API agora assina internamente o webhook mock com o proprio segredo, eliminando `401 invalid_signature` e destravando a transicao para `Paid`.
- Arquivos principais: `Backend/src/ConsertaPraMim.API/Controllers/PaymentsController.cs`, `Backend/src/ConsertaPraMim.Application/DTOs/PaymentIntegrationDTOs.cs`, `Backend/src/ConsertaPraMim.API/Swagger/ApiEndpointDocumentationCatalog.cs`, `Backend/src/ConsertaPraMim.Web.Client/Controllers/ServiceRequestsController.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/Controllers/PaymentsControllerTests.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`
- Risco/Impacto: medio

- [2026-03-02] [ST-057] Backfill de bairros em pedidos seedados sem `AddressNeighborhood`
- Tipo: fix
- Resumo: o bootstrap da API passou a corrigir pedidos legados sem bairro preenchido, priorizando extracao pelo sufixo de `AddressStreet` e usando geocoding por CEP como fallback; o seed de novos pedidos tambem passou a gravar `AddressNeighborhood` na insercao inicial.
- Arquivos principais: `Backend/src/ConsertaPraMim.Infrastructure/Data/DbInitializer.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`
- Risco/Impacto: baixo

- [2026-03-02] [ST-057] QA, manual e fechamento da cobertura por bairros no Mapa Operacional
- Tipo: test
- Resumo: a `ST-057` foi encerrada com caso de QA para validar consolidacao de bairros atendidos e nao atendidos no `Mapa Operacional`, ajuste do manual/indice e movimentacao da story para `DONE`.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`, `Documentacao/ADMIN_PORTAL/INDEX.md`, `Documentacao/ADMIN_PORTAL/STORIES/DONE/ST-057-mapa-operacional-bairros-atendidos-nao-atendidos.md`
- Risco/Impacto: baixo

- [2026-03-02] [ST-057] Tabelas de bairros atendidos e nao atendidos no Mapa Operacional
- Tipo: feat
- Resumo: a tela `Mapa Operacional` do portal admin passou a calcular, no front, a cobertura por bairro com base no raio dos prestadores visiveis e exibir duas tabelas operacionais: `Bairros atendidos` (100% cobertos) e `Bairros nao atendidos` (com gap parcial ou sem cobertura).
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminCoverageMap/Index.cshtml`, `Backend/src/ConsertaPraMim.Web.Admin/wwwroot/js/views/admin-coverage-map/index.js`
- Risco/Impacto: baixo

- [2026-03-02] [ST-057] Snapshot do Mapa Operacional com bairro por pedido
- Tipo: feat
- Resumo: o endpoint `GET /api/admin/dashboard/coverage-map` passou a incluir `AddressNeighborhood` nos pedidos retornados, preservando o filtro de cidade e a narrativa Swagger para suportar a consolidacao de cobertura por bairro na UI admin.
- Arquivos principais: `Backend/src/ConsertaPraMim.Application/DTOs/AdminDashboardDTOs.cs`, `Backend/src/ConsertaPraMim.Application/Services/AdminDashboardService.cs`, `Backend/src/ConsertaPraMim.API/Controllers/AdminDashboardController.cs`, `Backend/src/ConsertaPraMim.API/Swagger/ApiEndpointDocumentationCatalog.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/AdminDashboardServiceTests.cs`
- Risco/Impacto: baixo

- [2026-03-02] [ST-057] Backlog inicial da cobertura por bairros no Mapa Operacional
- Tipo: docs
- Resumo: aberta a `ST-057` para evoluir o `Mapa Operacional` do portal admin com visao tabular de bairros atendidos e nao atendidos, incluindo story, atualizacao de epic/indice e diagrama Mermaid inicial da analise de cobertura por bairro.
- Arquivos principais: `Documentacao/ADMIN_PORTAL/STORIES/IN_PROGRESS/ST-057-mapa-operacional-bairros-atendidos-nao-atendidos.md`, `Documentacao/ADMIN_PORTAL/EPICS/EPIC-001-admin-portal-unificado.md`, `Documentacao/ADMIN_PORTAL/INDEX.md`, `Documentacao/DIAGRAMAS/ADMIN_PORTAL/ST-057-mapa-operacional-bairros-atendidos-nao-atendidos/fluxo-mapa-operacional-bairros-cobertura.mmd`
- Risco/Impacto: baixo

- [2026-03-02] [ADMIN-GROWTH-AI] Overlay visual durante a geracao da analise IA
- Tipo: fix
- Resumo: a tela `AI Copilot Growth` passou a exibir um overlay fullscreen com icone animado e mensagem de processamento ao submeter `Gerar analise IA`, evitando clique repetido e deixando claro que a analise esta em execucao.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminGrowthAi/Index.cshtml`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`
- Risco/Impacto: baixo

- [2026-03-02] [ADMIN-GROWTH-AI] Datas do AI Copilot Growth exibidas em America/Sao_Paulo
- Tipo: fix
- Resumo: a tela `AI Copilot Growth` deixou de depender do fuso local do servidor para renderizar as datas das analises, passando a exibir historico, badges e opcoes de comparacao no fuso de negocio `America/Sao_Paulo`; os rotulos de comparacao gerados pelo servico tambem foram ajustados para a mesma regra.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminGrowthAi/Index.cshtml`, `Backend/src/ConsertaPraMim.Application/Services/AdminGrowthAiService.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/AdminGrowthAiServiceTests.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`
- Risco/Impacto: baixo

- [2026-03-02] [ST-018] Testes, QA e fechamento documental do bloqueio por avaliacao pendente
- Tipo: test
- Resumo: a entrega do bloqueio de novo pedido por avaliacao pendente foi encerrada com teste de regressao do `ServiceRequestsController`, atualizacao do runbook de avaliacao bilateral para cobrir o modal bloqueante e movimentacao da `ST-018` para `DONE` na trilha de operacao pos-agendamento.
- Arquivos principais: `Backend/tests/ConsertaPraMim.Tests.Unit/Controllers/ClientServiceRequestsCreateReviewGateControllerTests.cs`, `Documentacao/OPERACAO_SERVICO_POS_AGENDAMENTO/RUNBOOK_QA_AVALIACAO_BILATERAL_ST-013.md`, `Documentacao/OPERACAO_SERVICO_POS_AGENDAMENTO/INDEX.md`, `Documentacao/OPERACAO_SERVICO_POS_AGENDAMENTO/STORIES/DONE/ST-018-bloqueio-novo-pedido-por-avaliacao-pendente.md`
- Risco/Impacto: baixo

- [2026-03-02] [ST-018] Modal bloqueante no wizard de novo pedido com avaliacao inline
- Tipo: feat
- Resumo: a tela `ServiceRequests/Create` do portal cliente passou a abrir um modal bloqueante quando houver servicos concluidos sem avaliacao, exibindo a fila de pendencias, nota obrigatoria de 1 a 5 e submissao inline da avaliacao ate liberar o wizard para um novo pedido.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Client/Views/ServiceRequests/Create.cshtml`, `Backend/src/ConsertaPraMim.Web.Client/wwwroot/js/views/service-requests/create.js`, `Backend/src/ConsertaPraMim.Web.Client/Controllers/ServiceRequestsController.cs`, `Documentacao/OPERACAO_SERVICO_POS_AGENDAMENTO/STORIES/IN_PROGRESS/ST-018-bloqueio-novo-pedido-por-avaliacao-pendente.md`
- Risco/Impacto: medio

- [2026-03-02] [ST-018] Bloqueio server-side da criacao de pedido com avaliacao pendente
- Tipo: feat
- Resumo: o portal do cliente passou a consultar reviews pendentes ao abrir e ao postar `ServiceRequests/Create`, bloqueando a criacao de novo pedido quando houver servicos concluidos sem avaliacao; tambem foi criada uma acao web dedicada para enviar a avaliacao pendente sem sair do fluxo de abertura.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Client/Controllers/ServiceRequestsController.cs`, `Documentacao/OPERACAO_SERVICO_POS_AGENDAMENTO/STORIES/IN_PROGRESS/ST-018-bloqueio-novo-pedido-por-avaliacao-pendente.md`
- Risco/Impacto: medio

- [2026-03-02] [ST-018] Backlog inicial do bloqueio de novo pedido por avaliacao pendente
- Tipo: docs
- Resumo: criada a story `ST-018` na trilha de operacao pos-agendamento para bloquear a abertura de novo pedido no portal cliente enquanto houver servicos concluidos sem avaliacao; a `EPIC-005` foi atualizada, o indice da trilha passou a listar a entrega em andamento e um diagrama Mermaid inicial documenta o fluxo bloqueante.
- Arquivos principais: `Documentacao/OPERACAO_SERVICO_POS_AGENDAMENTO/INDEX.md`, `Documentacao/OPERACAO_SERVICO_POS_AGENDAMENTO/EPICS/EPIC-005-qualidade-reputacao-e-garantia-pos-servico.md`, `Documentacao/OPERACAO_SERVICO_POS_AGENDAMENTO/STORIES/IN_PROGRESS/ST-018-bloqueio-novo-pedido-por-avaliacao-pendente.md`, `Documentacao/DIAGRAMAS/OPERACAO_SERVICO_POS_AGENDAMENTO/ST-018-bloqueio-novo-pedido-por-avaliacao-pendente/fluxo-bloqueio-novo-pedido-por-avaliacao-pendente.mmd`
- Risco/Impacto: baixo

- [2026-03-01] [WEB-PROVIDER] Correcao de mojibake e reforco de governanca UTF-8
- Tipo: fix
- Resumo: os ultimos arquivos alterados do portal prestador que passaram a exibir textos corrompidos (`Descricao`, `Servicos`, `Distancia`, etc.) foram revisados e corrigidos para PT-BR com acentuacao valida; os arquivos impactados foram regravados em UTF-8 e a governanca de encoding foi reforcada com varredura obrigatoria por caracteres quebrados antes do encerramento da task.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Provider/Views/Home/Index.cshtml`, `Backend/src/ConsertaPraMim.Web.Provider/Views/ServiceRequests/Agenda.cshtml`, `Backend/src/ConsertaPraMim.Web.Provider/Views/ServiceRequests/Details.cshtml`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`, `AGENTS.md`
- Risco/Impacto: medio

- [2026-03-01] [WEB-CLIENT] Avaliacao do prestador com motivo especifico e feedback em SweetAlert
- Tipo: fix
- Resumo: no portal do cliente, o envio de avaliacao em `ServiceRequests/Details` passou a retornar motivo objetivo quando bloqueado (ex.: pagamento pendente, prazo expirado, duplicidade, ownership), e o feedback de sucesso/erro agora aparece em `SweetAlert` em vez de alerts inline duplicados.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Client/Controllers/ServiceRequestsController.cs`, `Backend/src/ConsertaPraMim.Web.Client/Views/ServiceRequests/Details.cshtml`, `Backend/src/ConsertaPraMim.Web.Client/Services/ClientApiCaller.cs`, `Backend/src/ConsertaPraMim.Web.Client/Services/ClientApiReviewService.cs`, `Backend/src/ConsertaPraMim.Application/Services/ReviewService.cs`, `Backend/src/ConsertaPraMim.API/Controllers/ReviewsController.cs`, `Backend/src/ConsertaPraMim.API/Swagger/ApiEndpointDocumentationCatalog.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/ReviewServiceTests.cs`
- Risco/Impacto: medio

- [2026-03-01] [WEB-PROVIDER] Mapas do prestador iniciam enquadrados pelo raio de atendimento
- Tipo: fix
- Resumo: os mapas do portal do prestador que exibem a area de cobertura passaram a iniciar o zoom pelo bounds do circulo de atendimento, priorizando o enquadramento visual do raio atual em vez de abrir afastados pela combinacao de pins e limites mais amplos.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Provider/wwwroot/js/views/home/index.js`, `Backend/src/ConsertaPraMim.Web.Provider/wwwroot/js/views/profile/index.js`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`
- Risco/Impacto: baixo

- [2026-03-01] [MAPAS-HOME] Mapas de home em largura total com proporcao 1:1
- Tipo: fix
- Resumo: os mapas exibidos na home do portal admin e na home do portal prestador passaram a ocupar toda a largura util da row/card correspondente, mantendo altura igual a largura (proporcao 1:1) no modo padrao; o fullscreen continua liberando a altura para uso da tela inteira.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminHome/Index.cshtml`, `Backend/src/ConsertaPraMim.Web.Provider/wwwroot/css/site.css`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`
- Risco/Impacto: baixo

- [2026-03-01] [WEB-PROVIDER] Mapa da home do prestador em formato quadrado e apenas no raio
- Tipo: fix
- Resumo: na home do portal do prestador, o widget `Mapa de Cobertura e Oportunidades` passou a renderizar em formato quadrado no card e a exibir apenas oportunidades dentro do raio de atendimento do prestador; a copy do bloco foi ajustada para refletir que o mapa e a lista ficam sincronizados somente com pedidos cobertos.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Provider/Views/Home/Index.cshtml`, `Backend/src/ConsertaPraMim.Web.Provider/wwwroot/js/views/home/index.js`, `Backend/src/ConsertaPraMim.Web.Provider/wwwroot/css/site.css`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`
- Risco/Impacto: baixo

- [2026-03-01] [ADMIN-HOME] Mapa da home em formato quadrado com pedidos cobertos por raio
- Tipo: fix
- Resumo: na home do portal admin, o widget `Mapa de Pedidos e Prestadores` passou a renderizar em formato quadrado no modo padrao e a exibir apenas os pedidos cuja localizacao esteja dentro do raio de atendimento de pelo menos um prestador visivel no mapa; a legenda e o resumo do widget foram ajustados para refletir que a contagem agora considera somente pedidos cobertos.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminHome/Index.cshtml`, `Backend/src/ConsertaPraMim.Web.Admin/wwwroot/js/views/admin-home/index.js`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`
- Risco/Impacto: baixo

- [2026-02-28] [GOV-ENCODING] Normalizacao de arquivos textuais para UTF-8 e enforcement por diretriz
- Tipo: fix
- Resumo: os arquivos textuais versionados que ainda estavam em `ANSI/Windows-1252` foram convertidos para `UTF-8`, eliminando artefatos como `esta` no ambiente publicado; o repositorio tambem passou a ter enforcement tecnico via `.editorconfig` e diretriz formal em `AGENTS.md` para impedir regressao de encoding.
- Arquivos principais: `AGENTS.md`, `.editorconfig`, `Backend/src/ConsertaPraMim.Web.Provider/Views/Home/Index.cshtml`, `Backend/src/ConsertaPraMim.Web.Provider/Views/Shared/_Layout.cshtml`, `Backend/src/ConsertaPraMim.Web.Provider/Views/Account/Register.cshtml`, `Backend/src/ConsertaPraMim.Web.Provider/Views/SupportTickets/Index.cshtml`, `Backend/src/ConsertaPraMim.Web.Provider/wwwroot/js/views/profile/index.js`, `Backend/src/ConsertaPraMim.Web.Client/wwwroot/js/views/service-requests/details.js`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminMonitoring/Index.cshtml`, `Documentacao/PROVIDER_DASHBOARD_MAPA_COBERTURA/EPICS/EPIC-001-mapa-cobertura-dashboard-prestador.md`
- Risco/Impacto: medio

- [2026-02-28] [WEB-PROVIDER] Criacao de chamado de suporte em PT-BR com anexos no primeiro envio
- Tipo: feat
- Resumo: a tela `SupportTickets/Create` do portal do prestador deixou de expor labels/opcoes em ingles e passou a operar integralmente em PT-BR; o formulario agora aceita anexos opcionais (fotos, videos e documentos) no primeiro envio, com upload previo para a pasta `support` e persistencia dos metadados ja na mensagem inicial do chamado.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Provider/Views/SupportTickets/Create.cshtml`, `Backend/src/ConsertaPraMim.Web.Provider/Controllers/SupportTicketsController.cs`, `Backend/src/ConsertaPraMim.Web.Provider/Models/SupportTicketsViewModels.cs`, `Backend/src/ConsertaPraMim.Web.Provider/wwwroot/js/views/support-tickets/create.js`, `Backend/src/ConsertaPraMim.Application/DTOs/MobileProviderSupportTicketDTOs.cs`, `Backend/src/ConsertaPraMim.Application/Services/MobileProviderService.cs`, `Backend/src/ConsertaPraMim.Application/Validators/SupportTicketValidators.cs`, `Backend/src/ConsertaPraMim.API/Controllers/MobileProviderController.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`
- Risco/Impacto: medio

- [2026-02-28] [WEB-PROVIDER] Botao de chat com cliente no cabecalho do detalhe do pedido
- Tipo: feat
- Resumo: a tela `ServiceRequests/Details` do portal do prestador passou a exibir o botao `Conversar` abaixo da badge de status no cabecalho do pedido, reutilizando o chat existente com o cliente mesmo quando ainda nao houver proposta enviada; o gatilho duplicado dentro do card de proposta enviada foi removido para reduzir redundancia.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Provider/Views/ServiceRequests/Details.cshtml`, `Backend/src/ConsertaPraMim.Web.Provider/wwwroot/js/views/service-requests/details.js`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`
- Risco/Impacto: baixo

- [2026-02-28] [WEB-PROVIDER] Erro de proposta validada exibido em SweetAlert com mensagem amigavel
- Tipo: fix
- Resumo: ao enviar proposta sem preencher os dados obrigatorios em `ServiceRequests/Details`, o portal do prestador deixou de exibir o JSON cru da API em um alerta inline e passou a mostrar um SweetAlert com texto objetivo; o client HTTP do prestador tambem passou a extrair `errors/detail/title` de respostas ProblemDetails para evitar payload tecnico bruto na interface.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Provider/Controllers/ProposalsController.cs`, `Backend/src/ConsertaPraMim.Web.Provider/Services/ProviderBackendApiClient.cs`, `Backend/src/ConsertaPraMim.Web.Provider/Views/ServiceRequests/Details.cshtml`, `Backend/src/ConsertaPraMim.Web.Provider/wwwroot/js/views/service-requests/details.js`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/ProviderProposalsControllerTests.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`
- Risco/Impacto: baixo

- [2026-02-28] [WEB-PROVIDER] Campo de proposta apresentado como valor de visita tecnica
- Tipo: fix
- Resumo: na aba `Geral` de `ServiceRequests/Details` do portal do prestador, o campo `Valor Estimado (opcional)` passou a ser exibido como `Valor Visita Tecnica (opcional)`, com texto de apoio explicando que o valor final do servico pode ser combinado depois com o cliente; o dado continua persistido no campo atual de proposta para manter compatibilidade com os demais modulos.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Provider/Views/ServiceRequests/Details.cshtml`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`
- Risco/Impacto: baixo

- [2026-02-28] [WEB-PROVIDER] Ocultacao de latitude/longitude na tela de perfil
- Tipo: fix
- Resumo: a tela `Configuracoes` (`Profile/Index`) do portal do prestador deixou de exibir os campos visiveis `Latitude (auto)` e `Longitude (auto)`, mantendo apenas os valores ocultos usados internamente no submit; a experiencia fica mais limpa para o usuario sem alterar o fluxo de localizacao por CEP/mapa.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Provider/Views/Profile/Index.cshtml`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`
- Risco/Impacto: baixo

- [2026-02-28] [WEB-CLIENT] Persistencia de Data Protection no portal cliente para evitar 400 no login
- Tipo: fix
- Resumo: o portal cliente passou a persistir chaves de `DataProtection` em volume dedicado nos containers, alinhando o comportamento ao portal prestador e evitando invalidacao recorrente do token antiforgery em `POST /Account/Login` apos restart/deploy do `cpm-web-client`.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Client/Program.cs`, `Backend/docker-compose.vps.yml`, `Backend/docker-compose.vps.web-client.yml`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`
- Risco/Impacto: medio

- [2026-02-28] [WEB-CLIENT] Correcao do HTTP 500 em `ServiceRequests/Create` por ativacao do controller
- Tipo: fix
- Resumo: o portal cliente voltou a abrir `ServiceRequests/Create` sem erro 500 quando autenticado; o `ServiceRequestsController` passou a registrar os adapters faltantes de suporte contextual e upload, a API ganhou endpoints para o atendimento do cliente vinculado ao pedido, o upload generico passou a aceitar a pasta `support` e a tela de login recebeu antiforgery para evitar falha 400 quando a sessao expira e o usuario precisa se autenticar novamente.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Client/Program.cs`, `Backend/src/ConsertaPraMim.Web.Client/Services/ClientApiClientSupportTicketService.cs`, `Backend/src/ConsertaPraMim.Web.Client/Services/ClientApiFileStorageService.cs`, `Backend/src/ConsertaPraMim.API/Controllers/ClientSupportTicketsController.cs`, `Backend/src/ConsertaPraMim.API/Controllers/FilesController.cs`, `Backend/src/ConsertaPraMim.Web.Client/Views/Account/Login.cshtml`, `Backend/src/ConsertaPraMim.API/Swagger/ApiEndpointDocumentationCatalog.cs`
- Risco/Impacto: medio

- [2026-02-28] [DEPLOY-VPS] Recriacao segura de containers com nome fixo no deploy seletivo
- Tipo: fix
- Resumo: o script `vps-deploy-service.sh` passou a executar o build antes da troca do container e remover explicitamente o container fixo do servico alvo (`cpm-api`, `cpm-web-admin`, etc.) antes do `docker compose up`, eliminando conflitos de `container_name` em migracoes entre projetos compose e recriacoes seletivas no runner.
- Arquivos principais: `scripts/deploy/vps-deploy-service.sh`, `Backend/DEPLOY_VPS.md`
- Risco/Impacto: medio

- [2026-02-28] [ST-009] QA, runbook e fechamento documental do cancelamento de pedido
- Tipo: docs
- Resumo: a trilha de agenda recebeu cobertura operacional do cancelamento de pedido em cascata, com manual QA, plano E2E e runbook atualizados para validar regra agregada de 48h, bloqueios por estado e fan-out de notificacao; a story ST-009 tambem foi encerrada e movida para `DONE`.
- Arquivos principais: `Documentacao/AGENDA_SERVICOS_JANELAS_LEMBRETES/MANUAL_ADMIN_QA_AGENDA_ST-008.md`, `Documentacao/AGENDA_SERVICOS_JANELAS_LEMBRETES/PLANO_TESTES_E2E_ST-008.md`, `Documentacao/AGENDA_SERVICOS_JANELAS_LEMBRETES/RUNBOOK_SUPORTE_ROLLBACK_ST-008.md`, `Documentacao/AGENDA_SERVICOS_JANELAS_LEMBRETES/INDEX.md`, `Documentacao/AGENDA_SERVICOS_JANELAS_LEMBRETES/STORIES/DONE/ST-009-cancelamento-pedido-cascata-48h.md`
- Risco/Impacto: baixo

- [2026-02-28] [ST-009] UI do cliente para cancelamento de pedido com impacto multiagendamento
- Tipo: feat
- Resumo: a tela `ServiceRequests/Details` do portal cliente passou a exibir a acao propria `Cancelar pedido`, com resumo visual do impacto por agendamento, bloqueio preventivo quando houver janela abaixo de 48h ou estados irreversiveis, supressao de novos agendamentos em pedidos encerrados e consumo do novo endpoint MVC `POST /ServiceRequests/CancelRequest`.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Client/Controllers/ServiceRequestsController.cs`, `Backend/src/ConsertaPraMim.Web.Client/wwwroot/js/views/service-requests/details.js`, `Documentacao/AGENDA_SERVICOS_JANELAS_LEMBRETES/STORIES/DONE/ST-009-cancelamento-pedido-cascata-48h.md`
- Risco/Impacto: medio

- [2026-02-28] [ST-009] Backend do cancelamento de pedido em cascata com regra agregada de 48h
- Tipo: feat
- Resumo: adicionada operacao de cancelamento do pedido em nivel de dominio/API, com endpoint `POST /api/service-requests/{id}/cancel`, validacao de 48h para todos os agendamentos ativos, bloqueio para estados nao elegiveis, cancelamento em cascata dos agendamentos validos, invalidacao de propostas, persistencia final do pedido em `Canceled` e fan-out de notificacao para prestadores com interacao; o cancelamento individual por cliente tambem passou a respeitar minimo efetivo de 48h.
- Arquivos principais: `Backend/src/ConsertaPraMim.Application/DTOs/ServiceRequestDTOs.cs`, `Backend/src/ConsertaPraMim.Application/Interfaces/IServiceRequestService.cs`, `Backend/src/ConsertaPraMim.Application/Services/ServiceRequestService.cs`, `Backend/src/ConsertaPraMim.Application/Services/ServiceAppointmentService.cs`, `Backend/src/ConsertaPraMim.Infrastructure/Repositories/ServiceRequestRepository.cs`, `Backend/src/ConsertaPraMim.API/Controllers/ServiceRequestsController.cs`, `Backend/src/ConsertaPraMim.API/Swagger/ApiEndpointDocumentationCatalog.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/ServiceRequestServiceTests.cs`
- Risco/Impacto: alto

- [2026-02-28] [ST-009] Backlog e diagrama inicial do cancelamento de pedido em cascata
- Tipo: docs
- Resumo: criada a trilha documental da nova entrega de cancelamento de pedido com politica de 48h, incluindo epic propria, story com tasks, atualizacao do indice da agenda e diagrama Mermaid inicial do fluxo do cliente.
- Arquivos principais: `Documentacao/AGENDA_SERVICOS_JANELAS_LEMBRETES/INDEX.md`, `Documentacao/AGENDA_SERVICOS_JANELAS_LEMBRETES/EPICS/EPIC-002-cancelamento-pedido-cascata-multi-prestador.md`, `Documentacao/AGENDA_SERVICOS_JANELAS_LEMBRETES/STORIES/DONE/ST-009-cancelamento-pedido-cascata-48h.md`, `Documentacao/DIAGRAMAS/AGENDA_SERVICOS_JANELAS_LEMBRETES/ST-009-cancelamento-pedido-cascata-48h/fluxo-cancelamento-pedido-cliente.mmd`
- Risco/Impacto: baixo

- [2026-02-28] [OPS-SCRIPT] Push de resumo com config local persistente (sem depender de env)
- Tipo: feat
- Resumo: o script `send_admin_summary_push.py` passou a aceitar `--config`, buscar automaticamente arquivo local persistente no perfil do usuario (`%USERPROFILE%\\.codex\\consertapramim\\push-config.json`), suportar `--init-config` para gerar automaticamente o JSON padrao e fallback opcional no repo (`scripts/send_admin_summary_push.local.json`), reduzindo dependencia de variaveis de ambiente efemeras; tambem foi adicionado arquivo de exemplo versionado.
- Arquivos principais: `scripts/send_admin_summary_push.py`, `scripts/send_admin_summary_push.example.json`, `.gitignore`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`
- Risco/Impacto: baixo

- [2026-02-27] [WEB-CLIENT] Status do pedido em PT-BR e diretriz de idioma no front
- Tipo: fix
- Resumo: o badge `Status do Pedido` em `ServiceRequests/Details` passou a traduzir todos os estados exibidos para PT-BR (`Criado`, `Buscando prestadores`, `Agendado`, `Em atendimento`, `Aguardando aceite de conclusao`, `Concluido`, `Validado`, `Cancelado`), e o repositorio recebeu diretriz formal para impedir exposicao de enums/status tecnicos em qualquer front.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Client/Views/ServiceRequests/Details.cshtml`, `AGENTS.md`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`
- Risco/Impacto: baixo

- [2026-02-27] [ST-056] Chat de ajuda entre cliente e admin no detalhe do pedido
- Tipo: feat
- Resumo: a aba `Precisa de ajuda?` em `ServiceRequests/Details` passou a usar atendimento E2E ligado ao pedido, com historico cliente x admin, envio de anexos (imagem/video/audio/documento), preview em lightbox fullscreen e polling leve para detectar novas mensagens quando a aba estiver aberta; o portal admin passou a reconhecer chamados originados por cliente e tambem visualizar anexos no mesmo padrao.
- Arquivos principais: `Backend/src/ConsertaPraMim.Application/DTOs/ClientSupportTicketDTOs.cs`, `Backend/src/ConsertaPraMim.Application/Interfaces/IClientSupportTicketService.cs`, `Backend/src/ConsertaPraMim.Application/Services/ClientSupportTicketService.cs`, `Backend/src/ConsertaPraMim.Application/Services/AdminSupportTicketService.cs`, `Backend/src/ConsertaPraMim.Application/DependencyInjection.cs`, `Backend/src/ConsertaPraMim.Domain/Repositories/ISupportTicketRepository.cs`, `Backend/src/ConsertaPraMim.Infrastructure/Repositories/SupportTicketRepository.cs`, `Backend/src/ConsertaPraMim.Web.Client/Controllers/ServiceRequestsController.cs`, `Backend/src/ConsertaPraMim.Web.Client/Views/ServiceRequests/Details.cshtml`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminSupportTickets/Index.cshtml`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminSupportTickets/Details.cshtml`
- Risco/Impacto: medio

- [2026-02-27] [WEB-CLIENT] Abas de detalhes do pedido com visual em botoes (estilo pills)
- Tipo: fix
- Resumo: a navegacao por abas em `ServiceRequests/Details` foi refinada para o estilo visual de botoes/pills, com aba ativa destacada em azul e abas inativas com comportamento visual de acao, aproximando a experiencia do padrao Bootstrap exibido como referencia.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Client/Views/ServiceRequests/Details.cshtml`
- Risco/Impacto: baixo

- [2026-02-27] [WEB-CLIENT] Abas de detalhes do pedido quebram linha sem scroll horizontal
- Tipo: fix
- Resumo: a navegacao por abas em `ServiceRequests/Details` passou a distribuir os botoes em multiplas linhas, com quebra responsiva e sem rolagem lateral, melhorando a leitura quando houver muitas secoes.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Client/Views/ServiceRequests/Details.cshtml`
- Risco/Impacto: baixo

- [2026-02-27] [WEB-CLIENT] ServiceRequests/Details com secoes operacionais em abas
- Tipo: feat
- Resumo: a tela de detalhes do pedido no portal do cliente foi redesenhada para navegacao por abas, substituindo o fluxo em blocos sequenciais para `Agendamento`, `Aditivos`, `Garantia`, `Disputas`, `Evidencias`, `Propostas`, `Comparador`, `Comprovantes`, `Pagamento`, `Dicas de seguranca` e `Ajuda`, preservando os mesmos IDs consumidos pelo JavaScript existente.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Client/Views/ServiceRequests/Details.cshtml`
- Risco/Impacto: medio

- [2026-02-27] [PROVIDER-APP] Header/footer priorizados sobre mapa e raio dinamico no perfil
- Tipo: fix
- Resumo: no app mobile do prestador, o `Dashboard` passou a usar `z-index` elevado no header e no footer para impedir sobreposicao do mapa durante o scroll; na tela `Perfil`, o mapa de base foi migrado para Leaflet com pin + circulo de cobertura atualizado em tempo real conforme o slider de raio de atendimento.
- Arquivos principais: `conserta-pra-mim-provider app/components/Dashboard.tsx`, `conserta-pra-mim-provider app/components/Profile.tsx`
- Risco/Impacto: baixo

- [2026-02-27] [ADM-RUNTIME] Toggle dedicado de Swagger na tela de configuracoes runtime
- Tipo: feat
- Resumo: adicionada acao visual dedicada para habilitar/desabilitar `Swagger.EnabledInProduction` no modulo `AdminRuntimeConfig`, com switch sincronizado ao JSON da secao `Swagger` para reduzir erro manual de edicao; manual QA ganhou o caso `QA-ADM-060` cobrindo persistencia + restart + validacao do endpoint `/swagger`.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Admin/wwwroot/js/views/admin-runtime-config/index.js`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`
- Risco/Impacto: baixo

- [2026-02-27] [ST-055] Correcao de SRI do Leaflet no passo `Onde?` do wizard cliente
- Tipo: fix
- Resumo: atualizados os hashes `integrity` (SRI) de `leaflet.css` e `leaflet.js` para os valores efetivos do `cdnjs`, eliminando bloqueio do browser e restaurando a exibicao do mapa com pin/raio de 1km na etapa 3.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Client/Views/ServiceRequests/Create.cshtml`
- Risco/Impacto: baixo

- [2026-02-27] [GOV-DOCS] Diretriz obrigatoria para prevenir bloqueio de assets por CSP
- Tipo: docs
- Resumo: adicionada diretriz em `AGENTS.md` exigindo validacao de compatibilidade CSP para qualquer asset externo (CSS/JS/img/font), com preferencia por origens homologadas/assets locais e checagem funcional antes do encerramento da task.
- Arquivos principais: `AGENTS.md`
- Risco/Impacto: baixo

- [2026-02-27] [ST-055] Correcao de carregamento do mapa na etapa `Onde?` (wizard cliente)
- Tipo: fix
- Resumo: corrigido o carregamento do Leaflet na criacao de pedido do cliente, trocando import de `unpkg` para `cdnjs` (alinhado ao CSP atual) e liberando tiles do OpenStreetMap no `img-src`; com isso o mapa com pin e raio de 1km volta a ser exibido abaixo do endereco resolvido por CEP.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Client/Views/ServiceRequests/Create.cshtml`, `Backend/src/ConsertaPraMim.Web.Client/Program.cs`
- Risco/Impacto: baixo

- [2026-02-27] [GOV-DOCS] Diretriz obrigatoria de bloco Markdown com commit detalhado por task
- Tipo: docs
- Resumo: adicionado no `AGENTS.md` o padrao de encerramento por task entregue, exigindo bloco Markdown com sugestao de commit detalhada (`titulo`, `tipo`, `contexto`, `arquivos`, `validacoes`, `risco`) ao final de cada conclusao de solicitacao.
- Arquivos principais: `AGENTS.md`
- Risco/Impacto: baixo

- [2026-02-27] [ST-055] Etapa `Onde?` do wizard cliente com bairro persistido + mapa de referencia (pin + raio 1km)
- Tipo: feat
- Resumo: o fluxo `ServiceRequests/Create` passou a resolver `bairro` junto de rua/cidade via CEP, preencher coordenadas no formulario, exibir mapa com pin e circulo de 1km na etapa 3 e persistir `AddressNeighborhood` no pedido; tambem foi adicionado aviso operacional de privacidade informando que o endereco real deve ser compartilhado somente no agendamento.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Client/Views/ServiceRequests/Create.cshtml`, `Backend/src/ConsertaPraMim.Web.Client/wwwroot/js/views/service-requests/create.js`, `Backend/src/ConsertaPraMim.Web.Client/Controllers/ServiceRequestsController.cs`, `Backend/src/ConsertaPraMim.Web.Client/Services/ClientApiZipGeocodingService.cs`, `Backend/src/ConsertaPraMim.API/Controllers/ServiceRequestsController.cs`, `Backend/src/ConsertaPraMim.Application/Interfaces/IZipGeocodingService.cs`, `Backend/src/ConsertaPraMim.Infrastructure/Services/ZipGeocodingService.cs`, `Backend/src/ConsertaPraMim.Domain/Entities/ServiceRequest.cs`, `Backend/src/ConsertaPraMim.Application/DTOs/ServiceRequestDTOs.cs`, `Backend/src/ConsertaPraMim.Application/Services/ServiceRequestService.cs`, `Backend/src/ConsertaPraMim.Infrastructure/Data/ConsertaPraMimDbContext.cs`, `Backend/src/ConsertaPraMim.Infrastructure/Migrations/20260227115658_AddServiceRequestAddressNeighborhood.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`, `Documentacao/ADMIN_PORTAL/STORIES/DONE/ST-055-analise-ia-wizard-criacao-pedido-cliente.md`
- Risco/Impacto: medio

- [2026-02-27] [ST-055] Persistencia da analise IA no pedido com exibicao ao prestador
- Tipo: feat
- Resumo: o resumo/highlights gerados na etapa `Analise do problema` passaram a ser enviados no `CreateServiceRequest`, persistidos no `ServiceRequest` (colunas dedicadas) e exibidos no detalhe do chamado do portal prestador para melhorar contexto tecnico antes da proposta.
- Arquivos principais: `Backend/src/ConsertaPraMim.Domain/Entities/ServiceRequest.cs`, `Backend/src/ConsertaPraMim.Application/DTOs/ServiceRequestDTOs.cs`, `Backend/src/ConsertaPraMim.Application/Services/ServiceRequestService.cs`, `Backend/src/ConsertaPraMim.Application/Validators/ServiceRequestValidator.cs`, `Backend/src/ConsertaPraMim.Infrastructure/Data/ConsertaPraMimDbContext.cs`, `Backend/src/ConsertaPraMim.Infrastructure/Migrations/20260227114430_AddServiceRequestProblemAnalysisFields.cs`, `Backend/src/ConsertaPraMim.Web.Client/Views/ServiceRequests/Create.cshtml`, `Backend/src/ConsertaPraMim.Web.Client/wwwroot/js/views/service-requests/create.js`, `Backend/src/ConsertaPraMim.Web.Provider/Views/ServiceRequests/Details.cshtml`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`, `Backend/src/ConsertaPraMim.API/Swagger/ApiEndpointDocumentationCatalog.cs`
- Risco/Impacto: medio

- [2026-02-27] [ST-055] Ajuste de tom da analise IA no wizard do cliente (sem narracao)
- Tipo: fix
- Resumo: refinado o prompt e o pos-processamento da analise IA para retornar resumo tecnico direto, removendo aberturas narrativas como `O cliente relata...`; fallback textual tambem foi padronizado para formato objetivo.
- Arquivos principais: `Backend/src/ConsertaPraMim.Application/Services/ServiceRequestProblemAnalysisService.cs`
- Risco/Impacto: baixo

- [2026-02-27] [ST-055] Manual QA atualizado e story encerrada
- Tipo: docs
- Resumo: adicionado caso `QA-ADM-059` no manual com cobertura E2E do wizard cliente em 4 etapas, smoke checklist foi atualizado e a `ST-055` foi movida para `DONE` com fechamento do `EPIC-024`.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`, `Documentacao/ADMIN_PORTAL/STORIES/DONE/ST-055-analise-ia-wizard-criacao-pedido-cliente.md`, `Documentacao/ADMIN_PORTAL/EPICS/EPIC-024-analise-ia-abertura-pedido-cliente.md`, `Documentacao/ADMIN_PORTAL/INDEX.md`
- Risco/Impacto: baixo

- [2026-02-27] [ST-055] Wizard do portal cliente evoluido para 4 passos com etapa de analise IA
- Tipo: feat
- Resumo: a tela `ServiceRequests/Create` agora possui 4 etapas (`O que precisa?`, `Analise do problema`, `Onde?`, `Revisar`), com chamada AJAX protegida por antiforgery para `AnalyzeProblem`, loading/retry no passo 2 e exibicao do resumo IA na revisao final.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Client/Views/ServiceRequests/Create.cshtml`, `Backend/src/ConsertaPraMim.Web.Client/wwwroot/js/views/service-requests/create.js`, `Backend/src/ConsertaPraMim.Web.Client/Controllers/ServiceRequestsController.cs`, `Documentacao/ADMIN_PORTAL/STORIES/IN_PROGRESS/ST-055-analise-ia-wizard-criacao-pedido-cliente.md`
- Risco/Impacto: medio

- [2026-02-27] [ST-055] Endpoint de analise IA no backend para criacao de pedido do cliente
- Tipo: feat
- Resumo: implementado fluxo backend de analise do problema com `POST /api/service-requests/problem-analysis`, incluindo validacao de categoria/descricao, geracao de resumo via OpenAI e fallback operacional quando a IA estiver indisponivel; Swagger recebeu narrativa especifica do novo endpoint.
- Arquivos principais: `Backend/src/ConsertaPraMim.Application/DTOs/ServiceRequestProblemAnalysisDTOs.cs`, `Backend/src/ConsertaPraMim.Application/Interfaces/IServiceRequestProblemAnalysisService.cs`, `Backend/src/ConsertaPraMim.Application/Services/ServiceRequestProblemAnalysisService.cs`, `Backend/src/ConsertaPraMim.Application/DependencyInjection.cs`, `Backend/src/ConsertaPraMim.API/Controllers/ServiceRequestsController.cs`, `Backend/src/ConsertaPraMim.API/Swagger/ApiEndpointDocumentationCatalog.cs`, `Documentacao/ADMIN_PORTAL/STORIES/IN_PROGRESS/ST-055-analise-ia-wizard-criacao-pedido-cliente.md`
- Risco/Impacto: medio

- [2026-02-27] [ST-055] Backlog da etapa de analise IA no wizard de criacao de pedido do cliente
- Tipo: docs
- Resumo: criado `EPIC-024` e iniciada `ST-055` para adicionar a etapa `Analise do problema` entre os passos `O que precisa?` e `Onde?`, com endpoint dedicado e feedback IA no portal cliente.
- Arquivos principais: `Documentacao/ADMIN_PORTAL/EPICS/EPIC-024-analise-ia-abertura-pedido-cliente.md`, `Documentacao/ADMIN_PORTAL/STORIES/IN_PROGRESS/ST-055-analise-ia-wizard-criacao-pedido-cliente.md`, `Documentacao/ADMIN_PORTAL/INDEX.md`
- Risco/Impacto: baixo

- [2026-02-26] [ST-054] Eventos Recentes da home admin migrado para widget incremental dedicado
- Tipo: feat
- Resumo: o bloco `Eventos Recentes` foi extraido para componente independente com carregamento via `GET /AdminHome/Widgets/recent-events`, mantendo filtros locais, ordenacao por coluna e estado vazio coerente durante refresh incremental.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminHome/_WidgetRecentEvents.cshtml`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminHome/Index.cshtml`, `Backend/src/ConsertaPraMim.Web.Admin/wwwroot/js/views/admin-home/index.js`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`, `Documentacao/ADMIN_PORTAL/STORIES/DONE/ST-054-widgets-analiticos-incrementais-home-admin.md`, `Documentacao/ADMIN_PORTAL/EPICS/EPIC-023-dashboard-admin-widgets-incrementais.md`, `Documentacao/ADMIN_PORTAL/INDEX.md`
- Risco/Impacto: medio

- [2026-02-26] [ST-054] Widgets analiticos da home admin componentizados com carga incremental isolada
- Tipo: feat
- Resumo: os blocos de receita, status, categoria, operacao, status de prestadores, rankings, outliers e falhas de pagamento passaram a usar componentes Razor independentes com `skeleton`, `spinner` e falha localizada, consumindo endpoints dedicados por widget (`/AdminHome/Widgets/{widgetKey}`) no refresh da home.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminHome/Index.cshtml`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminHome/_WidgetMonthlyRevenue.cshtml`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminHome/_WidgetRequestStatus.cshtml`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminHome/_WidgetRequestCategory.cshtml`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminHome/_WidgetOperationalStatus.cshtml`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminHome/_WidgetProviderOperationalStatus.cshtml`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminHome/_WidgetProviderReviewRanking.cshtml`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminHome/_WidgetClientReviewRanking.cshtml`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminHome/_WidgetReviewOutliers.cshtml`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminHome/_WidgetPaymentFailuresByProvider.cshtml`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminHome/_WidgetPaymentFailuresByChannel.cshtml`, `Backend/src/ConsertaPraMim.Web.Admin/wwwroot/js/views/admin-home/index.js`
- Risco/Impacto: medio

- [2026-02-26] [ST-054] Endpoints dedicados para widgets analiticos e operacionais da home admin
- Tipo: feat
- Resumo: adicionados contrato `AdminDashboardWidgetDto`, mapeamento no `AdminDashboardService`, endpoint `GET /api/admin/dashboard/widgets/{widgetKey}` e proxy autenticado `GET /AdminHome/Widgets/{widgetKey}` para receita, status, reputacao, falhas de pagamento e eventos recentes da home admin.
- Arquivos principais: `Backend/src/ConsertaPraMim.Application/DTOs/AdminDashboardWidgetDTOs.cs`, `Backend/src/ConsertaPraMim.Application/Services/AdminDashboardService.cs`, `Backend/src/ConsertaPraMim.API/Controllers/AdminDashboardController.cs`, `Backend/src/ConsertaPraMim.API/Swagger/ApiEndpointDocumentationCatalog.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Controllers/AdminHomeController.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Services/AdminDashboardApiClient.cs`
- Risco/Impacto: medio

- [2026-02-26] [ST-054] Backlog inicial dos widgets incrementais restantes da home admin
- Tipo: docs
- Resumo: criado o `EPIC-023` e iniciada a `ST-054` para modularizar os widgets analiticos e operacionais restantes da home admin em componentes independentes com endpoints dedicados, skeleton, spinner e refresh seletivo.
- Arquivos principais: `Documentacao/ADMIN_PORTAL/EPICS/EPIC-023-dashboard-admin-widgets-incrementais.md`, `Documentacao/ADMIN_PORTAL/STORIES/IN_PROGRESS/ST-054-widgets-analiticos-incrementais-home-admin.md`, `Documentacao/ADMIN_PORTAL/INDEX.md`
- Risco/Impacto: baixo

- [2026-02-26] [ADM-HOME] Painel de no-show reposicionado acima do mapa operacional
- Tipo: feat
- Resumo: a home admin foi reorganizada para exibir o `Painel Operacional de No-show` imediatamente abaixo da grade principal de KPIs, deixando o `Mapa de Pedidos e Prestadores` em terceiro plano na hierarquia visual da tela.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminHome/Index.cshtml`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`
- Risco/Impacto: baixo

- [2026-02-26] [ADM-HOME] Botao de tela cheia no mapa de pedidos e prestadores
- Tipo: feat
- Resumo: o mapa operacional da home admin passou a exibir um botao overlay de `Tela cheia`, expandindo o painel completo do widget com suporte a retorno, atualizacao visual do controle e `invalidateSize()` do Leaflet para manter renderizacao correta.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminHome/Index.cshtml`, `Backend/src/ConsertaPraMim.Web.Admin/wwwroot/js/views/admin-home/index.js`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`
- Risco/Impacto: baixo

- [2026-02-26] [ADM-HOME] Correcao do summary da home admin apos componentizacao dos KPIs
- Tipo: fix
- Resumo: removidas referencias JavaScript residuais a elementos antigos da secao de reincidencia/no-show, evitando excecao em runtime no refresh do snapshot da home admin e eliminando o erro generico `Nao foi possivel atualizar o dashboard no momento`.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Admin/wwwroot/js/views/admin-home/index.js`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`
- Risco/Impacto: baixo

- [2026-02-26] [ADM-HOME] KPIs reposicionados para o topo da home admin
- Tipo: feat
- Resumo: a home admin foi reorganizada para exibir a grade principal de KPIs logo no topo da tela, enquanto o `Mapa de Pedidos e Prestadores` passou a ficar imediatamente abaixo desses indicadores, priorizando leitura executiva antes da cobertura geografica.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminHome/Index.cshtml`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`
- Risco/Impacto: baixo

- [2026-02-26] [ST-053] KPIs de no-show da home admin separados em componentes incrementais
- Tipo: feat
- Resumo: o painel operacional de no-show da home admin passou a renderizar seus nove KPIs via componentes independentes com skeleton no boot, spinner em refresh seletivo e falha isolada por card, alinhando o comportamento ao restante do dashboard.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Admin/Models/AdminHomeKpiCardComponentModel.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminHome/_IncrementalMetricCard.cshtml`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminHome/Index.cshtml`, `Backend/src/ConsertaPraMim.Web.Admin/wwwroot/js/views/admin-home/index.js`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`
- Risco/Impacto: medio

- [2026-02-26] [ST-053] KPIs gerais da home admin separados em componentes incrementais
- Tipo: feat
- Resumo: a grade principal de KPIs da home admin foi migrada para componentes Razor reutilizaveis com skeleton no boot, spinner em refresh individual e consumo exclusivo dos endpoints dedicados por card.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Admin/Models/AdminHomeKpiCardComponentModel.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminHome/_IncrementalMetricCard.cshtml`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminHome/Index.cshtml`, `Backend/src/ConsertaPraMim.Web.Admin/wwwroot/js/views/admin-home/index.js`
- Risco/Impacto: medio

- [2026-02-26] [ST-053] Endpoints dedicados por KPI para home admin incremental
- Tipo: feat
- Resumo: adicionados contratos `AdminKpiCardDto`, cache curto em memoria e endpoints dedicados por KPI para dashboard geral e no-show, junto com proxies autenticados no portal admin para suportar carregamento individual por card.
- Arquivos principais: `Backend/src/ConsertaPraMim.Application/DTOs/AdminKpiCardDTOs.cs`, `Backend/src/ConsertaPraMim.Application/Services/AdminDashboardService.cs`, `Backend/src/ConsertaPraMim.Application/Services/AdminNoShowDashboardService.cs`, `Backend/src/ConsertaPraMim.API/Controllers/AdminDashboardController.cs`, `Backend/src/ConsertaPraMim.API/Controllers/AdminNoShowDashboardController.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Controllers/AdminHomeController.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Services/AdminDashboardApiClient.cs`
- Risco/Impacto: medio

- [2026-02-26] [ST-053] Backlog inicial da home admin incremental por KPI
- Tipo: docs
- Resumo: criado o `EPIC-022` e iniciada a `ST-053` para modularizar os KPIs da home admin em componentes independentes com carregamento individual, endpoints dedicados e feedback visual por card.
- Arquivos principais: `Documentacao/ADMIN_PORTAL/EPICS/EPIC-022-dashboard-admin-kpis-incrementais.md`, `Documentacao/ADMIN_PORTAL/STORIES/IN_PROGRESS/ST-053-home-admin-kpis-modulares.md`, `Documentacao/ADMIN_PORTAL/INDEX.md`
- Risco/Impacto: baixo

- [2026-02-26] [ADM-HOME] Filtros locais e ordenacao na grade de Eventos Recentes
- Tipo: feat
- Resumo: a home do portal admin passou a permitir filtros locais por tipo, titulo, descricao e periodo (`de/ate`) na grade `Eventos Recentes`, usando drawer `offcanvas` no mesmo padrao dos demais modulos; tambem foi adicionada ordenacao clicavel em todos os headers da tabela, preservada mesmo apos refresh do dashboard.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminHome/Index.cshtml`, `Backend/src/ConsertaPraMim.Web.Admin/wwwroot/js/views/admin-home/index.js`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`
- Risco/Impacto: baixo

- [2026-02-26] [ST-019] Monitoramento com exclusao de endpoint tecnico SignalR no ranking de Top Endpoints
- Tipo: fix
- Resumo: o modulo de monitoramento passou a ignorar `POST /notificationhub/negotiate` (SignalR de notificacoes) no pipeline de agregacao consultiva; com isso o Top Endpoints e o Top Endpoint do overview deixam de destacar ruido de infraestrutura e voltam a refletir trafego de negocio.
- Arquivos principais: `Backend/src/ConsertaPraMim.Infrastructure/Services/AdminMonitoringService.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/Integration/Controllers/AdminMonitoringControllerSqliteIntegrationTests.cs`
- Risco/Impacto: baixo

- [2026-02-25] [ST-043] Filtros em drawer offcanvas padronizados nos modulos operacionais restantes do admin
- Tipo: feat
- Resumo: os modulos `Usuarios > Fila de Confianca`, `Planos e Ofertas`, `Creditos`, `Propostas`, `Conversas` e `Disputas` migraram de filtros inline para drawer `offcanvas`, mantendo os mesmos parametros de consulta/exportacao e alinhando o padrao global de UX para telas com filtros.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminUsers/TrustQueue.cshtml`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminPlanGovernance/Index.cshtml`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminProviderCredits/Index.cshtml`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminProposals/Index.cshtml`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminChats/Index.cshtml`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminDisputes/Index.cshtml`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`
- Risco/Impacto: baixo

- [2026-02-25] [ST-039] Change Logs com filtros no padrao offcanvas/drawer
- Tipo: feat
- Resumo: a tela `Change Logs` migrou de formulario inline para drawer `offcanvas`, padronizando a UX de filtros (`q`, `de`, `ate`) com os demais modulos administrativos.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminChangeLogs/Index.cshtml`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`
- Risco/Impacto: baixo

- [2026-02-25] [ST-040] Roadmap com filtros no padrao offcanvas/drawer
- Tipo: feat
- Resumo: a tela `Roadmap` migrou de formulario inline para drawer `offcanvas`, mantendo consistencia de UX com os demais modulos admin e centralizando filtros por texto/epic/trilha/status no botao `Filtros`.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminRoadmap/Index.cshtml`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`
- Risco/Impacto: baixo

- [2026-02-25] [ST-042] Score Liquidez com filtros no padrao offcanvas/drawer
- Tipo: feat
- Resumo: a tela `Score Liquidez` migrou de formulario inline para drawer `offcanvas`, mantendo consistencia com os modulos `Monitoramento`, `Growth Funnel` e `Cockpit Growth`, com acesso por botao `Filtros` no cabecalho.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminLiquidityScore/Index.cshtml`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`
- Risco/Impacto: baixo

- [2026-02-25] [ST-051] Cockpit Growth com filtros no padrao offcanvas/drawer
- Tipo: feat
- Resumo: a tela `Cockpit Growth` migrou de formulario inline para drawer `offcanvas`, alinhando o mesmo padrao adotado em `Monitoramento` e `Growth Funnel`, com acao explicita de `Filtros` no cabecalho.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminGrowthCockpit/Index.cshtml`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`
- Risco/Impacto: baixo

- [2026-02-25] [ST-049] Growth Funnel com filtros no padrao offcanvas/drawer
- Tipo: feat
- Resumo: a tela `Growth Funnel` do portal admin migrou de formulario inline para drawer `offcanvas`, alinhando o mesmo padrao visual/operacional do modulo `Monitoramento`; diretriz global foi registrada para que toda tela com filtros siga esse padrao.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminGrowth/Index.cshtml`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`, `AGENTS.md`
- Risco/Impacto: baixo

- [2026-02-25] [ST-052] Comparativo IA entre duas analises no AI Copilot Growth
- Tipo: feat
- Resumo: implementado endpoint `POST /api/admin/growth/ai/compare` para comparar baseline x atual usando OpenAI no backend, com novo modal no portal admin para escolher duas analises do historico e exibicao visual do delta (melhorias, regressoes, sinais estaveis e acoes prioritarias).
- Arquivos principais: `Backend/src/ConsertaPraMim.Application/DTOs/AdminGrowthAiDTOs.cs`, `Backend/src/ConsertaPraMim.Application/Interfaces/IAdminGrowthAiService.cs`, `Backend/src/ConsertaPraMim.Application/Services/AdminGrowthAiService.cs`, `Backend/src/ConsertaPraMim.API/Controllers/AdminGrowthController.cs`, `Backend/src/ConsertaPraMim.API/Swagger/ApiEndpointDocumentationCatalog.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Services/IAdminOperationsApiClient.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Services/AdminOperationsApiClient.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Controllers/AdminGrowthAiController.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Models/AdminGrowthAiViewModel.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminGrowthAi/Index.cshtml`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/AdminGrowthAiServiceTests.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/AdminGrowthControllerAiTests.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`
- Risco/Impacto: medio

- [2026-02-25] [ST-052] Refinamento visual da analise do AI Copilot Growth
- Tipo: feat
- Resumo: secao `Ultima analise` recebeu formatacao executiva com destaque de resumo, chips de contexto (modelo/tokens/categoria/cidade), cards coloridos por dominio (`Funil`, `Liquidez`, `Riscos`, `Acoes`) e icones para leitura mais rapida no portal admin.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminGrowthAi/Index.cshtml`
- Risco/Impacto: baixo

- [2026-02-25] [ST-052] Correcao de binding no formulario do AI Copilot Growth
- Tipo: fix
- Resumo: corrigido binding dos POSTs `SaveSettings` e `RunAnalysis` no portal admin usando prefixos `SettingsForm` e `AnalyzeForm`; com isso a `OpenAI API key` e demais campos do formulario passam a ser recebidos corretamente no backend.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Admin/Controllers/AdminGrowthAiController.cs`
- Risco/Impacto: baixo

- [2026-02-24] [ST-050] Playbook de melhoria por baixa avaliacao publicado e story encerrada
- Tipo: docs
- Resumo: publicado runbook operacional de resposta a baixa avaliacao (`gatilhos`, severidade, SLA, owners e criterios de encerramento), manual QA recebeu o caso `QA-ADM-046` e a ST-050 foi movida para `DONE` com referenciamento no board.
- Arquivos principais: `Documentacao/ADMIN_PORTAL/RUNBOOKS/RUNBOOK_MELHORIA_BAIXA_AVALIACAO_ST-050.md`, `Documentacao/ADMIN_PORTAL/STORIES/DONE/ST-050-pos-servico-avaliacao-recompra.md`, `Documentacao/ADMIN_PORTAL/INDEX.md`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`
- Risco/Impacto: baixo
- [2026-02-24] [ST-050] Indicadores de recompra e NPS operacional no dashboard admin
- Tipo: feat
- Resumo: o dashboard admin passou a expor KPIs de retencao/qualidade pos-servico (`repurchaseRatePercent`, base e conversao de clientes, `operationalNpsScore`, `operationalQualityScore`) com atualizacao em tempo real no portal, apoiando leitura executiva de recompra e reputacao no mesmo recorte operacional.
- Arquivos principais: `Backend/src/ConsertaPraMim.Application/DTOs/AdminDashboardDTOs.cs`, `Backend/src/ConsertaPraMim.Application/Services/AdminDashboardService.cs`, `Backend/src/ConsertaPraMim.API/Swagger/ApiEndpointDocumentationCatalog.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminHome/Index.cshtml`, `Backend/src/ConsertaPraMim.Web.Admin/wwwroot/js/views/admin-home/index.js`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/AdminDashboardServiceTests.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`, `Documentacao/ADMIN_PORTAL/STORIES/IN_PROGRESS/ST-050-pos-servico-avaliacao-recompra.md`
- Risco/Impacto: medio
- [2026-02-24] [ST-046] Runbook operacional de no-show/cancelamento e encerramento da story
- Tipo: docs
- Resumo: publicado runbook de operacao/contestacao da ST-046 com fluxo de triagem, evidencia, SLA e decisao; story movida para `DONE` e index atualizado para refletir o encerramento.
- Arquivos principais: `Documentacao/ADMIN_PORTAL/RUNBOOKS/RUNBOOK_NO_SHOW_CANCELAMENTO_ST-046.md`, `Documentacao/ADMIN_PORTAL/STORIES/DONE/ST-046-politicas-no-show-cancelamento.md`, `Documentacao/ADMIN_PORTAL/INDEX.md`
- Risco/Impacto: baixo
- [2026-02-24] [ST-046] Painel de reincidencia no-show com top ofensores e tendencia diaria no admin
- Tipo: feat
- Resumo: dashboard administrativo passou a incluir bloco dedicado de reincidencia no-show (janela de 90 dias) com volume critico por perfil, taxa de reincidencia, top clientes/prestadores reincidentes e serie diaria de eventos criticos para suporte operacional.
- Arquivos principais: `Backend/src/ConsertaPraMim.Application/DTOs/AdminNoShowDashboardDTOs.cs`, `Backend/src/ConsertaPraMim.Application/Services/AdminNoShowDashboardService.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminHome/Index.cshtml`, `Backend/src/ConsertaPraMim.Web.Admin/wwwroot/js/views/admin-home/index.js`, `Documentacao/ADMIN_PORTAL/STORIES/IN_PROGRESS/ST-046-politicas-no-show-cancelamento.md`
- Risco/Impacto: medio
- [2026-02-24] [ST-046] Integracao de notificacoes e eventos operacionais para politicas de no-show
- Tipo: feat
- Resumo: aplicacao da politica financeira de no-show/cancelamento agora dispara evento operacional admin (`admin_event_no_show_policy_applied`) e os eventos passam a compor o feed de `Eventos Recentes` no dashboard admin com contexto de outcome e valor.
- Arquivos principais: `Backend/src/ConsertaPraMim.Application/Interfaces/IAdminOperationalEventNotifier.cs`, `Backend/src/ConsertaPraMim.Application/Services/AdminOperationalEventNotifier.cs`, `Backend/src/ConsertaPraMim.Application/Services/ServiceAppointmentService.cs`, `Backend/src/ConsertaPraMim.Application/Services/AdminDashboardService.cs`, `Documentacao/ADMIN_PORTAL/STORIES/IN_PROGRESS/ST-046-politicas-no-show-cancelamento.md`
- Risco/Impacto: medio
- [2026-02-24] [ST-046] Trilha auditavel de no-show/cancelamento exposta para operacao admin
- Tipo: feat
- Resumo: implementada consulta estruturada da trilha de decisao financeira de no-show/cancelamento via `GET /api/admin/no-show-audit`, consolidando eventos `ServiceFinancialPolicyEventGenerated` com tipo de evento, outcome, impacto financeiro e resultado de ledger para suporte/auditoria.
- Arquivos principais: `Backend/src/ConsertaPraMim.Application/DTOs/AdminNoShowAuditDTOs.cs`, `Backend/src/ConsertaPraMim.Application/Interfaces/IAdminNoShowAuditService.cs`, `Backend/src/ConsertaPraMim.Application/Services/AdminNoShowAuditService.cs`, `Backend/src/ConsertaPraMim.API/Controllers/AdminNoShowAuditController.cs`, `Backend/src/ConsertaPraMim.Application/DependencyInjection.cs`, `Documentacao/ADMIN_PORTAL/STORIES/IN_PROGRESS/ST-046-politicas-no-show-cancelamento.md`
- Risco/Impacto: baixo
- [2026-02-24] [ST-046] Matriz de politicas no-show/cancelamento consolidada por perfil
- Tipo: docs
- Resumo: story ST-046 foi iniciada com regras operacionais v1 por janela de antecedencia e reincidencia para cliente/prestador, incluindo evidencias obrigatorias para decisao e auditoria.
- Arquivos principais: `Documentacao/ADMIN_PORTAL/STORIES/IN_PROGRESS/ST-046-politicas-no-show-cancelamento.md`, `Documentacao/ADMIN_PORTAL/INDEX.md`
- Risco/Impacto: baixo
- [2026-02-24] [ST-045] Politica de confianca alinhada aos termos legais e runbooks operacionais
- Tipo: docs
- Resumo: atualizados os termos seed de cliente/prestador com clausulas explicitas de camadas de confianca (`Pending/Verified/Restricted`) e limites de garantia, alem de reforco de governanca nos runbooks de termos legais e confianca para obrigar revisao juridica quando a politica operacional mudar.
- Arquivos principais: `Backend/src/ConsertaPraMim.Infrastructure/Data/LegalTermsSeedContent.cs`, `Documentacao/ADMIN_PORTAL/RUNBOOKS/RUNBOOK_TERMOS_LEGAIS_ST-035.md`, `Documentacao/ADMIN_PORTAL/RUNBOOKS/RUNBOOK_CONFIANCA_PRESTADORES_ST-045.md`, `Documentacao/ADMIN_PORTAL/STORIES/DONE/ST-045-confianca-verificacao-prestadores.md`, `Documentacao/ADMIN_PORTAL/INDEX.md`
- Risco/Impacto: baixo
- [2026-02-24] [ST-045] Fila admin de confianca com decisao e historico operacional
- Tipo: feat
- Resumo: o portal admin ganhou a view `Confianca Prestadores` com filtros por status/risco, listagem documental, decisao de revisao (`Pending/Verified/Restricted`) e painel de historico de auditoria por prestador consumindo os novos endpoints da API.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Admin/Controllers/AdminUsersController.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Services/IAdminUsersApiClient.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Services/AdminUsersApiClient.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminUsers/TrustQueue.cshtml`, `Backend/src/ConsertaPraMim.Web.Admin/wwwroot/js/views/admin-users/trust-queue.js`, `Backend/src/ConsertaPraMim.Web.Admin/Views/Shared/_Layout.cshtml`
- Risco/Impacto: medio
- [2026-02-24] [ST-045] Selo de confianca exposto no perfil publico e em cards de propostas
- Tipo: feat
- Resumo: propostas do cliente e listagem admin passaram a exibir status de confianca/risco do prestador (`Pending/Verified/Restricted`), e o perfil publico do prestador agora mostra selo operacional de confianca baseado no status persistido.
- Arquivos principais: `Backend/src/ConsertaPraMim.Application/DTOs/ProposalDTOs.cs`, `Backend/src/ConsertaPraMim.Application/Services/ProposalService.cs`, `Backend/src/ConsertaPraMim.Web.Client/Views/ServiceRequests/Details.cshtml`, `Backend/src/ConsertaPraMim.Web.Client/wwwroot/js/views/service-requests/details.js`, `Backend/src/ConsertaPraMim.Web.Client/Views/PublicProfiles/Provider.cshtml`, `Backend/src/ConsertaPraMim.Application/DTOs/AdminRequestsProposalsDTOs.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminProposals/Index.cshtml`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminUsers/Details.cshtml`
- Risco/Impacto: baixo
- [2026-02-24] [ST-045] Trilha de auditoria de confianca de prestadores com fila e revisao admin na API
- Tipo: feat
- Resumo: criado o modelo persistido de confianca do prestador (`TrustStatus`, `RiskLevel`, motivo/data) e a trilha de auditoria `ProviderTrustReviews`; adicionados endpoints admin para fila de confianca, historico por prestador e decisao de revisao com log/auditoria.
- Arquivos principais: `Backend/src/ConsertaPraMim.Domain/Entities/ProviderProfile.cs`, `Backend/src/ConsertaPraMim.Domain/Entities/ProviderTrustReview.cs`, `Backend/src/ConsertaPraMim.Domain/Enums/Enums.cs`, `Backend/src/ConsertaPraMim.Infrastructure/Data/ConsertaPraMimDbContext.cs`, `Backend/src/ConsertaPraMim.Infrastructure/Migrations/20260224100823_AddProviderTrustReviewTrail.cs`, `Backend/src/ConsertaPraMim.Application/Services/AdminUserService.cs`, `Backend/src/ConsertaPraMim.API/Controllers/AdminUsersController.cs`
- Risco/Impacto: medio
- [2026-02-24] [ST-045] Politica de verificacao de prestadores por nivel de risco definida
- Tipo: docs
- Resumo: ST-045 movida para `In Progress` com politica operacional v1 de confianca (niveis baixo/medio/alto), estados `Pending/Verified/Restricted`, regras de transicao e SLA de analise/reanalise/escalonamento, incluindo runbook dedicado para operacao.
- Arquivos principais: `Documentacao/ADMIN_PORTAL/STORIES/IN_PROGRESS/ST-045-confianca-verificacao-prestadores.md`, `Documentacao/ADMIN_PORTAL/RUNBOOKS/RUNBOOK_CONFIANCA_PRESTADORES_ST-045.md`, `Documentacao/ADMIN_PORTAL/INDEX.md`
- Risco/Impacto: baixo
- [2026-02-24] [ST-044] Score persistido de qualidade de propostas com ranking e painel admin por categoria
- Tipo: feat
- Resumo: propostas agora recebem score de qualidade persistido no backend (completude, clareza, historico e comercial), sao ranqueadas por qualidade/historico na consulta do cliente, exibem score no detalhe do pedido e o admin ganhou consolidado de qualidade media por categoria na tela de propostas.
- Arquivos principais: `Backend/src/ConsertaPraMim.Domain/Entities/Proposal.cs`, `Backend/src/ConsertaPraMim.Application/Services/ProposalService.cs`, `Backend/src/ConsertaPraMim.Application/DTOs/ProposalDTOs.cs`, `Backend/src/ConsertaPraMim.Infrastructure/Data/ConsertaPraMimDbContext.cs`, `Backend/src/ConsertaPraMim.Infrastructure/Migrations/20260224095354_AddProposalQualityScoring.cs`, `Backend/src/ConsertaPraMim.Application/Services/AdminRequestProposalService.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminProposals/Index.cshtml`, `Backend/src/ConsertaPraMim.Web.Client/Views/ServiceRequests/Details.cshtml`, `Documentacao/ADMIN_PORTAL/STORIES/DONE/ST-044-qualidade-ranking-propostas.md`
- Risco/Impacto: medio
- [2026-02-24] [ST-044] Validacoes obrigatorias de proposta no backend (escopo, prazo, garantia)
- Tipo: feat
- Resumo: fluxo de envio de proposta no app prestador passou a exigir escopo minimo (>= 20 caracteres), prazo estimado e garantia; API mobile mapeia novos erros de validacao dedicados e validator de proposta foi reforcado para regras obrigatorias.
- Arquivos principais: `Backend/src/ConsertaPraMim.Application/Services/MobileProviderService.cs`, `Backend/src/ConsertaPraMim.API/Controllers/MobileProviderController.cs`, `Backend/src/ConsertaPraMim.Application/Validators/ProposalReviewValidators.cs`, `Documentacao/ADMIN_PORTAL/STORIES/IN_PROGRESS/ST-044-qualidade-ranking-propostas.md`
- Risco/Impacto: medio
- [2026-02-24] [ST-044] Rubric operacional de qualidade de propostas definida
- Tipo: docs
- Resumo: ST-044 movida para `In Progress` com rubric v1 de qualidade (completude, clareza, historico e confiabilidade comercial), formula de score 0-100 e faixas operacionais para ranking/admin.
- Arquivos principais: `Documentacao/ADMIN_PORTAL/STORIES/IN_PROGRESS/ST-044-qualidade-ranking-propostas.md`, `Documentacao/ADMIN_PORTAL/INDEX.md`
- Risco/Impacto: baixo
- [2026-02-24] [ST-038] Correcao de falso redirecionamento para login no modulo Manual QA/Operacao
- Tipo: fix
- Resumo: removida a heuristica global que varria o texto inteiro do documento para detectar "sessao expirada" no layout admin; o comportamento gerava falso positivo na tela `Manual QA/Operacao` e redirecionava indevidamente para `/Account/Login`.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Admin/wwwroot/js/layout/admin-layout.js`
- Risco/Impacto: baixo
- [2026-02-24] [ST-038] Diagramas Mermaid com pan/zoom e correcoes de sintaxe em labels sensiveis
- Tipo: fix
- Resumo: visualizador `Diagramas Mermaid` no portal admin passou a suportar pan/zoom (arrastar, zoom in/out e reset) com `svg-pan-zoom`; renderizacao ganhou fallback de sanitizacao para flowcharts com labels sensiveis e foram corrigidos arquivos `.mmd` com labels contendo parenteses para evitar erro de parse no browser.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminDiagrams/Index.cshtml`, `Documentacao/DIAGRAMAS/ADMIN_PORTAL/ST-017-aplicacao-creditos-mensalidade-visibilidade/fluxo-credito-mensalidade.mmd`, `Documentacao/DIAGRAMAS/ADMIN_PORTAL/ST-025-realtime-notificacoes-sla-suporte/fluxo-realtime-notificacoes-sla-suporte.mmd`, `Documentacao/DIAGRAMAS/PROVIDER_APP_WEB/ST-006-login-biometria-email-senha-hibrido-provider/fluxo-login-biometria-email-senha-hibrido-provider.mmd`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`
- Risco/Impacto: baixo

- [2026-02-26] [ST-052] AI Copilot Growth com periodo padrao de ultima semana no formulario
- Tipo: fix
- Resumo: tela `AdminGrowthAi` passou a abrir com `De/Ate` preenchidos automaticamente com janela de 7 dias quando o recorte nao e informado; o mesmo default agora e aplicado no submit da analise para evitar execucoes sem intervalo.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Admin/Controllers/AdminGrowthAiController.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/AdminGrowthAiWebControllerTests.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`
- Risco/Impacto: baixo

- [2026-02-26] [ST-052] Worker horario de relatorio executivo IA (hora cheia + envio por email com copia)
- Tipo: feat
- Resumo: API passou a executar `AdminGrowthAiHourlyDigestWorker` na virada de cada hora (`America/Sao_Paulo`), consolidando atividades recentes do dashboard, KPIs de monitoramento e analise diaria do `AdminGrowthAi`; o payload e enviado para OpenAI para gerar relatorio HTML e disparado via SMTP para `devcraftstudio@outlook.com` com copia para `leonardomendes201704@gmail.com`.
- Arquivos principais: `Backend/src/ConsertaPraMim.API/BackgroundJobs/AdminGrowthAiHourlyDigestWorker.cs`, `Backend/src/ConsertaPraMim.API/Program.cs`, `Backend/src/ConsertaPraMim.API/appsettings.json`, `Backend/src/ConsertaPraMim.API/appsettings.Development.json`, `Backend/tests/ConsertaPraMim.Tests.Unit/Integration/BackgroundJobs/AdminGrowthAiHourlyDigestWorkerIntegrationTests.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`
- Risco/Impacto: medio

- [2026-02-25] [ST-052] Configuracao OpenAI do AI Copilot Growth movida para modal no cabecalho
- Tipo: feat
- Resumo: a secao `Configuracao OpenAI` saiu do corpo principal da view `AdminGrowthAi` e passou para modal Bootstrap acionado por botao de configuracao no canto superior direito, liberando mais espaco para analise/historico e mantendo o mesmo fluxo de persistencia.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminGrowthAi/Index.cshtml`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`
- Risco/Impacto: baixo

- [2026-03-04] [ST-012] Delete de evento Google Calendar no cancelamento de agendamento
- Tipo: feat
- Resumo: `ServiceAppointmentService.CancelAsync` passou a sincronizar delete no Google Calendar apos cancelamento local bem-sucedido; quando ha `GoogleEventId`, executa `DeleteEventAsync` e marca sync como `Deleted` em sucesso ou `Failed` com trilha de erro em falha; quando nao ha sync previo, cria registro `ServiceAppointmentCalendarSync` com status `Deleted` para manter rastreabilidade.
- Arquivos principais: `Backend/src/ConsertaPraMim.Application/Services/ServiceAppointmentService.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/ServiceAppointmentServiceTests.cs`, `Documentacao/REALTIME_PRESENCA_CHAT/STORIES/IN_PROGRESS/ST-012-sync-automatica-agendamento-google-calendar.md`, `Documentacao/REALTIME_PRESENCA_CHAT/MANUAL_QA_OPERACAO_GOOGLE_CALENDAR_SYNC_ST-011.md`, `Documentacao/DIAGRAMAS/REALTIME_PRESENCA_CHAT/ST-012-sync-agendamento-google-calendar/README.md`, `Documentacao/DIAGRAMAS/REALTIME_PRESENCA_CHAT/ST-012-sync-agendamento-google-calendar/fluxo-sync-agendamento-google-calendar.mmd`, `Documentacao/DIAGRAMAS/REALTIME_PRESENCA_CHAT/ST-012-sync-agendamento-google-calendar/sequencia-sync-agendamento-google-calendar.mmd`
- Risco/Impacto: medio

- [2026-03-04] [ST-012] Update de evento Google Calendar ao aceitar reagendamento
- Tipo: feat
- Resumo: `ServiceAppointmentService.RespondRescheduleAsync` passou a sincronizar atualizacao do evento no Google Calendar quando o reagendamento e aceito, atualizando janela/metadata via `UpdateEventAsync`; quando o evento nao e encontrado (`google_calendar_event_not_found`), o fluxo faz fallback com `CreateEventAsync` idempotente por `appointmentId`, mantendo `ServiceAppointmentCalendarSync` em `Synced` ou `Failed` com trilha de erro.
- Arquivos principais: `Backend/src/ConsertaPraMim.Application/Services/ServiceAppointmentService.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/ServiceAppointmentServiceTests.cs`, `Documentacao/REALTIME_PRESENCA_CHAT/STORIES/IN_PROGRESS/ST-012-sync-automatica-agendamento-google-calendar.md`, `Documentacao/REALTIME_PRESENCA_CHAT/MANUAL_QA_OPERACAO_GOOGLE_CALENDAR_SYNC_ST-011.md`, `Documentacao/DIAGRAMAS/REALTIME_PRESENCA_CHAT/ST-012-sync-agendamento-google-calendar/fluxo-sync-agendamento-google-calendar.mmd`, `Documentacao/DIAGRAMAS/REALTIME_PRESENCA_CHAT/ST-012-sync-agendamento-google-calendar/sequencia-sync-agendamento-google-calendar.mmd`
- Risco/Impacto: medio

- [2026-03-04] [ST-012] Create de evento Google Calendar com idempotencia por appointmentId
- Tipo: feat
- Resumo: o fluxo de agendamento do chatbot passou a executar `CreateEventAsync` no Google Calendar apos persistencia local, usando chave idempotente `cpm-apt-{appointmentId}`; em sucesso o sync e marcado como `Synced` com `GoogleEventId`, e em falha e marcado como `Failed` com trilha de erro para reprocessamento sem bloquear o agendamento local.
- Arquivos principais: `Backend/src/ConsertaPraMim.Application/Services/TelegramChatbotSchedulingService.cs`, `Backend/src/ConsertaPraMim.Application/Interfaces/IGoogleCalendarService.cs`, `Backend/src/ConsertaPraMim.Infrastructure/Services/GoogleCalendarService.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/TelegramChatbotSchedulingServiceTests.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/GoogleCalendarServiceTests.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/Integration/Controllers/TelegramChatbotControllerSqliteIntegrationTests.cs`, `Documentacao/REALTIME_PRESENCA_CHAT/STORIES/IN_PROGRESS/ST-012-sync-automatica-agendamento-google-calendar.md`, `Documentacao/REALTIME_PRESENCA_CHAT/MANUAL_QA_OPERACAO_GOOGLE_CALENDAR_SYNC_ST-011.md`, `Documentacao/DIAGRAMAS/REALTIME_PRESENCA_CHAT/ST-012-sync-agendamento-google-calendar/sequencia-sync-agendamento-google-calendar.mmd`
- Risco/Impacto: medio

- [2026-03-04] [ST-012] Orquestracao de agendamento marcada para sync pendente no Google Calendar
- Tipo: feat
- Resumo: `TelegramChatbotSchedulingService` passou a persistir trilha de sincronizacao apos `CreateAsync` bem-sucedido: cria registro `ServiceAppointmentCalendarSync` com `Pending` quando inexistente ou atualiza registro existente para `Pending` limpando `Error`, garantindo fila consistente para os proximos passos de sync com Google.
- Arquivos principais: `Backend/src/ConsertaPraMim.Application/Services/TelegramChatbotSchedulingService.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/TelegramChatbotSchedulingServiceTests.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/Integration/Controllers/TelegramChatbotControllerSqliteIntegrationTests.cs`, `Documentacao/REALTIME_PRESENCA_CHAT/STORIES/IN_PROGRESS/ST-012-sync-automatica-agendamento-google-calendar.md`, `Documentacao/REALTIME_PRESENCA_CHAT/MANUAL_QA_OPERACAO_GOOGLE_CALENDAR_SYNC_ST-011.md`, `Documentacao/DIAGRAMAS/REALTIME_PRESENCA_CHAT/ST-012-sync-agendamento-google-calendar/fluxo-sync-agendamento-google-calendar.mmd`, `Documentacao/DIAGRAMAS/REALTIME_PRESENCA_CHAT/ST-012-sync-agendamento-google-calendar/sequencia-sync-agendamento-google-calendar.mmd`
- Risco/Impacto: medio

- [2026-03-04] [ST-012] Fundacao de persistencia para sincronizacao de agendamentos Google Calendar
- Tipo: feat
- Resumo: criada a entidade de mapeamento `ServiceAppointmentCalendarSync` com repositorio dedicado, relacao 1:1 com `ServiceAppointment`, indices de idempotencia (`AppointmentId` unico e `GoogleEventId` unico quando preenchido), status de sincronizacao e migration `AddServiceAppointmentCalendarSync` para persistencia da trilha.
- Arquivos principais: `Backend/src/ConsertaPraMim.Domain/Entities/ServiceAppointmentCalendarSync.cs`, `Backend/src/ConsertaPraMim.Domain/Entities/ServiceAppointment.cs`, `Backend/src/ConsertaPraMim.Domain/Repositories/IServiceAppointmentCalendarSyncRepository.cs`, `Backend/src/ConsertaPraMim.Infrastructure/Repositories/ServiceAppointmentCalendarSyncRepository.cs`, `Backend/src/ConsertaPraMim.Infrastructure/Data/ConsertaPraMimDbContext.cs`, `Backend/src/ConsertaPraMim.Infrastructure/Migrations/20260304152621_AddServiceAppointmentCalendarSync.cs`, `Backend/src/ConsertaPraMim.Infrastructure/Migrations/ConsertaPraMimDbContextModelSnapshot.cs`, `Documentacao/REALTIME_PRESENCA_CHAT/STORIES/IN_PROGRESS/ST-012-sync-automatica-agendamento-google-calendar.md`
- Risco/Impacto: medio

- [2026-03-04] [ST-011] Fundacao tecnica da integracao Google Calendar via Service Account
- Tipo: feat
- Resumo: adicionada base de integracao na API com `IGoogleCalendarService`, options `GoogleCalendarSync` e validacao de startup (`ValidateOnStart`) para bloquear configuracao invalida quando habilitada; servico implementa `create/update/delete` de eventos com autenticacao por Service Account, payload padrao (titulo/descricao/local/metadados) e conversao de janelas UTC para timezone de negocio (`America/Sao_Paulo`).
- Arquivos principais: `Backend/src/ConsertaPraMim.Application/Interfaces/IGoogleCalendarService.cs`, `Backend/src/ConsertaPraMim.Infrastructure/Configuration/GoogleCalendarSyncOptions.cs`, `Backend/src/ConsertaPraMim.Infrastructure/Configuration/GoogleCalendarSyncOptionsValidator.cs`, `Backend/src/ConsertaPraMim.Infrastructure/Services/GoogleCalendarService.cs`, `Backend/src/ConsertaPraMim.Infrastructure/DependencyInjection.cs`, `Backend/src/ConsertaPraMim.Infrastructure/ConsertaPraMim.Infrastructure.csproj`, `Backend/src/ConsertaPraMim.API/appsettings.json`, `Backend/src/ConsertaPraMim.API/appsettings.Development.json`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/GoogleCalendarSyncOptionsValidatorTests.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/GoogleCalendarServiceTests.cs`
- Risco/Impacto: medio

- [2026-03-04] [ST-011] Manual operacional e diagramas da fundacao Google Calendar
- Tipo: docs
- Resumo: story ST-011 movida para `DONE` com tasks concluidas, publicacao de manual QA/operacao da integracao e diagramas Mermaid de fluxo/sequencia para bootstrap, validacao e operacao basica do cliente Google Calendar.
- Arquivos principais: `Documentacao/REALTIME_PRESENCA_CHAT/STORIES/DONE/ST-011-fundacao-google-calendar-service-account-calendario-unico.md`, `Documentacao/REALTIME_PRESENCA_CHAT/MANUAL_QA_OPERACAO_GOOGLE_CALENDAR_SYNC_ST-011.md`, `Documentacao/REALTIME_PRESENCA_CHAT/INDEX.md`, `Documentacao/REALTIME_PRESENCA_CHAT/README.md`, `Documentacao/DIAGRAMAS/REALTIME_PRESENCA_CHAT/ST-011-fundacao-google-calendar-sync/README.md`, `Documentacao/DIAGRAMAS/REALTIME_PRESENCA_CHAT/ST-011-fundacao-google-calendar-sync/fluxo-fundacao-google-calendar-sync.mmd`, `Documentacao/DIAGRAMAS/REALTIME_PRESENCA_CHAT/ST-011-fundacao-google-calendar-sync/sequencia-fundacao-google-calendar-sync.mmd`
- Risco/Impacto: baixo

- [2026-03-04] [ST-005] UX do composer no Telegram Bridge com envio por Enter
- Tipo: fix
- Resumo: ajustado o composer do chat web para enviar mensagem ao pressionar `Enter`, preservando quebra de linha com `Shift+Enter`, com atualizacao da story ST-005 e manual QA/operacao da bridge.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.TelegramBridge/wwwroot/js/chat.js`, `Documentacao/REALTIME_PRESENCA_CHAT/STORIES/DONE/ST-005-login-cliente-telegram-bridge-e-vinculo-conversa.md`, `Documentacao/REALTIME_PRESENCA_CHAT/MANUAL_QA_OPERACAO_TELEGRAM_BRIDGE_ST-003.md`
- Risco/Impacto: baixo

- [2026-03-04] [EPIC-003] Planejamento da integracao Google Calendar para agendamentos
- Tipo: docs
- Resumo: criada a trilha documental inicial para sincronizacao de agendamentos com Google Calendar (epic, stories e tasks), incluindo orientacao operacional de Service Account e calendario unico.
- Arquivos principais: `Documentacao/REALTIME_PRESENCA_CHAT/EPICS/EPIC-003-sincronizacao-agendamento-google-calendar.md`, `Documentacao/REALTIME_PRESENCA_CHAT/STORIES/BACKLOG/ST-011-fundacao-google-calendar-service-account-calendario-unico.md`, `Documentacao/REALTIME_PRESENCA_CHAT/STORIES/BACKLOG/ST-012-sync-automatica-agendamento-google-calendar.md`, `Documentacao/REALTIME_PRESENCA_CHAT/STORIES/BACKLOG/ST-013-observabilidade-reprocessamento-qa-rollout-google-calendar.md`, `Documentacao/REALTIME_PRESENCA_CHAT/INDEX.md`
- Risco/Impacto: baixo

- [2026-03-04] [OPS-CHATBOT] Sanitizacao de credenciais de teste no Telegram Bridge
- Tipo: fix
- Resumo: removidos valores sensiveis em texto puro dos `appsettings` do Telegram Bridge e substituidos por placeholder para carregamento via user-secrets/variaveis de ambiente.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.TelegramBridge/appsettings.json`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/appsettings.Development.json`
- Risco/Impacto: baixo

- [2026-03-04] [ST-010] Guardrails, rollout gradual e observabilidade operacional do chatbot Telegram
- Tipo: feat
- Resumo: implementados guardrails conversacionais com handoff humano (`emergencia`, `fora de escopo`, `dados sensiveis`), catalogo padronizado de erros/fallback por `errorCode`, feature flag de rollout gradual por ambiente/chat (`allow/block list` + percentual deterministico), instrumentacao de observabilidade (trafego, IA, negocio, dependencias, incidentes) e endpoint de dashboard operacional `GET /api/chatbot-observability/dashboard` com controle de token fora de desenvolvimento.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.TelegramBridge/Services/TelegramChatbotOrchestrator.cs`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/Services/TelegramChatbotErrorCatalog.cs`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/Services/TelegramChatbotGuardrailPolicy.cs`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/Services/TelegramChatbotFeatureFlagService.cs`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/Services/TelegramChatbotObservabilityService.cs`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/Controllers/ChatbotObservabilityController.cs`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/Options/TelegramChatbotRolloutOptions.cs`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/Options/TelegramChatbotObservabilityOptions.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/TelegramChatbotGuardrailPolicyTests.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/TelegramChatbotFeatureFlagServiceTests.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/TelegramChatbotObservabilityServiceTests.cs`, `Documentacao/REALTIME_PRESENCA_CHAT/STORIES/DONE/ST-010-guardrails-observabilidade-qa-e-rollout-chatbot.md`, `Documentacao/REALTIME_PRESENCA_CHAT/MANUAL_QA_OPERACAO_CHATBOT_TELEGRAM.md`
- Risco/Impacto: medio

- [2026-03-04] [ST-010] Plano QA, runbook e diagramas de rollout/rollback do chatbot Telegram
- Tipo: docs
- Resumo: concluido o fechamento operacional da ST-010 com plano QA completo (smoke, regressao, carga basica e falha), runbook de incidentes/rollback, atualizacao de status da story para `DONE` e publicacao dos diagramas Mermaid de fluxo e sequencia para guardrails/observabilidade/rollout.
- Arquivos principais: `Documentacao/REALTIME_PRESENCA_CHAT/MANUAL_QA_OPERACAO_CHATBOT_TELEGRAM.md`, `Documentacao/REALTIME_PRESENCA_CHAT/RUNBOOKS/RUNBOOK_INCIDENTES_ROLLBACK_CHATBOT_TELEGRAM_ST-010.md`, `Documentacao/REALTIME_PRESENCA_CHAT/STORIES/DONE/ST-010-guardrails-observabilidade-qa-e-rollout-chatbot.md`, `Documentacao/REALTIME_PRESENCA_CHAT/INDEX.md`, `Documentacao/DIAGRAMAS/REALTIME_PRESENCA_CHAT/ST-010-guardrails-observabilidade-rollout-chatbot/README.md`, `Documentacao/DIAGRAMAS/REALTIME_PRESENCA_CHAT/ST-010-guardrails-observabilidade-rollout-chatbot/fluxo-guardrails-observabilidade-rollout-chatbot.mmd`, `Documentacao/DIAGRAMAS/REALTIME_PRESENCA_CHAT/ST-010-guardrails-observabilidade-rollout-chatbot/sequencia-guardrails-observabilidade-rollout-chatbot.mmd`
- Risco/Impacto: baixo

- [2026-03-04] [ST-009] Orquestrador de consulta natural com contexto, auditoria e paginacao no chatbot Telegram
- Tipo: feat
- Resumo: concluida a ST-009 com fluxo conversacional de consulta para pedidos/status/detalhes/agenda no `TelegramChatbotOrchestrator`, incluindo deteccao contextual por protocolo/pedido atual, respostas amigaveis para casos sem dados, paginacao por continuidade ("mostrar mais"), persistencia de trilha auditavel (`query_intent_result`, `query_reference_state`, `query_*`) e cobertura automatizada unitaria/integracao das intents de consulta e autorizacao.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.TelegramBridge/Services/TelegramChatbotOrchestrator.cs`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/Services/ITelegramChatbotApiClient.cs`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/Services/TelegramChatbotApiClient.cs`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/Models/TelegramServiceRequestModels.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/TelegramChatbotOrchestratorTests.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/Integration/Controllers/TelegramChatbotControllerSqliteIntegrationTests.cs`, `Documentacao/DIAGRAMAS/REALTIME_PRESENCA_CHAT/ST-009-consulta-natural-status-pedidos-agenda/fluxo-consulta-natural-status-pedidos-agenda.mmd`, `Documentacao/DIAGRAMAS/REALTIME_PRESENCA_CHAT/ST-009-consulta-natural-status-pedidos-agenda/sequencia-consulta-natural-status-pedidos-agenda.mmd`, `Documentacao/REALTIME_PRESENCA_CHAT/STORIES/DONE/ST-009-consulta-natural-de-status-pedidos-e-agenda.md`, `Documentacao/REALTIME_PRESENCA_CHAT/MANUAL_QA_OPERACAO_CHATBOT_TELEGRAM.md`
- Risco/Impacto: medio

- [2026-03-04] [ST-009] API de consulta natural de pedidos, status e agenda para chatbot Telegram
- Tipo: feat
- Resumo: adicionados contratos e endpoints de consulta no dominio `TelegramChatbot` para listar pedidos do cliente, consultar status/detalhes de pedido especifico e listar agendamentos com paginacao e escopo por `ClientId`, preparando a base da ST-009 para respostas conversacionais no orquestrador.
- Arquivos principais: `Backend/src/ConsertaPraMim.Application/DTOs/TelegramChatbotSchedulingDTOs.cs`, `Backend/src/ConsertaPraMim.Application/Interfaces/ITelegramChatbotSchedulingService.cs`, `Backend/src/ConsertaPraMim.Application/Services/TelegramChatbotSchedulingService.cs`, `Backend/src/ConsertaPraMim.API/Controllers/TelegramChatbotController.cs`, `Backend/src/ConsertaPraMim.API/Swagger/ApiEndpointDocumentationCatalog.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/TelegramChatbotSchedulingServiceTests.cs`, `Documentacao/REALTIME_PRESENCA_CHAT/STORIES/IN_PROGRESS/ST-009-consulta-natural-de-status-pedidos-e-agenda.md`, `Documentacao/REALTIME_PRESENCA_CHAT/INDEX.md`
- Risco/Impacto: medio

- [2026-03-04] [ST-008] Guardrail de confirmacao de agenda sem persistencia no chatbot Telegram
- Tipo: fix
- Resumo: adicionado guardrail no orquestrador da bridge para bloquear respostas de "agendamento confirmado" quando nao existe lote persistido com sucesso no historico da conversa; nesses casos o bot responde com `awaiting_provider_confirmation`, informando que ainda depende de acao do prestador e que retornara com detalhes apos confirmacao.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.TelegramBridge/Services/TelegramChatbotOrchestrator.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/TelegramChatbotOrchestratorTests.cs`, `Documentacao/REALTIME_PRESENCA_CHAT/MANUAL_QA_OPERACAO_CHATBOT_TELEGRAM.md`, `Documentacao/REALTIME_PRESENCA_CHAT/STORIES/DONE/ST-008-matching-prestadores-e-agendamento-multi-visitas.md`
- Risco/Impacto: medio

- [2026-03-04] [ST-008] Hotfix de disponibilidade real antes de sugerir prestadores no chatbot Telegram
- Tipo: fix
- Resumo: fluxo de agendamento foi ajustado para pedir primeiro os dias/periodos desejados e somente depois avaliar prestadores; a bridge agora consulta slots reais (`/api/service-appointments/slots`) por prestador/janela antes de montar o lote, evitando sugestao prematura e reduzindo respostas de indisponibilidade apos confirmacao textual. Tambem foi corrigida a interpretacao de "semana q vem" para manter todos os dias na semana seguinte.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.TelegramBridge/Services/TelegramChatbotOrchestrator.cs`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/Services/TelegramChatbotApiClient.cs`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/Services/ITelegramChatbotApiClient.cs`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/Services/TelegramSchedulingNaturalLanguageParser.cs`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/Models/TelegramServiceRequestModels.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/TelegramChatbotOrchestratorTests.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/TelegramSchedulingNaturalLanguageParserTests.cs`, `Documentacao/REALTIME_PRESENCA_CHAT/MANUAL_QA_OPERACAO_CHATBOT_TELEGRAM.md`
- Risco/Impacto: medio

- [2026-03-04] [ST-008] Hotfix de inferencia de agendamento por sinais de dia/periodo no chatbot Telegram
- Tipo: fix
- Resumo: corrigido o parser de agenda para considerar intencao de agendamento quando o cliente informa apenas sinais naturais de data/periodo (ex.: "quarta, quinta e sexta de manha"), mesmo sem palavra-chave explicita como "agendar"; com isso o orquestrador deixa de responder confirmacao sem persistencia e volta a executar o lote real de agendamentos (`schedule-visits-batch`) quando ha dados suficientes.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.TelegramBridge/Services/TelegramSchedulingNaturalLanguageParser.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/TelegramSchedulingNaturalLanguageParserTests.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/TelegramChatbotOrchestratorTests.cs`, `Documentacao/REALTIME_PRESENCA_CHAT/MANUAL_QA_OPERACAO_CHATBOT_TELEGRAM.md`
- Risco/Impacto: medio

- [2026-03-03] [ST-008] Hotfix do loop de resposta e consulta de status/agendamento no chatbot Telegram
- Tipo: fix
- Resumo: corrigido o comportamento em que o bot repetia "pedido ja registrado" para qualquer mensagem apos criacao do pedido; a triagem agora so continua automaticamente enquanto o pedido nao foi criado, e o orquestrador passou a responder consultas de status/agendamento/prestadores usando o contexto historico (`serviceRequestId`) com fallback para listagem de prestadores quando ainda nao ha visitas confirmadas.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.TelegramBridge/Services/TelegramServiceRequestTriageEngine.cs`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/Services/TelegramChatbotOrchestrator.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/TelegramServiceRequestTriageEngineTests.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/TelegramChatbotOrchestratorTests.cs`
- Risco/Impacto: medio

- [2026-03-03] [ST-008] Story encerrada e movida para DONE no board realtime
- Tipo: docs
- Resumo: concluida a ST-008 com todas as tasks finalizadas, incluindo parser natural, matching e agendamento multi-visitas; story movida para `STORIES/DONE` e indices/manuais da trilha atualizados.
- Arquivos principais: `Documentacao/REALTIME_PRESENCA_CHAT/STORIES/DONE/ST-008-matching-prestadores-e-agendamento-multi-visitas.md`, `Documentacao/REALTIME_PRESENCA_CHAT/INDEX.md`, `Documentacao/REALTIME_PRESENCA_CHAT/MANUAL_QA_OPERACAO_CHATBOT_TELEGRAM.md`
- Risco/Impacto: baixo

- [2026-03-03] [ST-008] Orquestracao de matching + agendamento natural no Telegram Bridge
- Tipo: feat
- Resumo: integrado o fluxo ST-008 no `TelegramChatbotOrchestrator` para, apos criacao de pedido ou solicitacao do cliente, consultar prestadores elegiveis, interpretar janelas em linguagem natural, executar `schedule-visits-batch`, persistir sugestoes/decisoes em `context-snapshots/actions` e responder em linguagem humana com cenarios de sucesso total, parcial ou replanejamento.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.TelegramBridge/Services/TelegramChatbotOrchestrator.cs`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/Services/ITelegramChatbotApiClient.cs`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/Services/TelegramChatbotApiClient.cs`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/Models/TelegramServiceRequestModels.cs`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/Program.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/TelegramChatbotOrchestratorTests.cs`, `Documentacao/DIAGRAMAS/REALTIME_PRESENCA_CHAT/ST-008-matching-agendamento-multi-visitas/fluxo-matching-agendamento-multi-visitas.mmd`, `Documentacao/DIAGRAMAS/REALTIME_PRESENCA_CHAT/ST-008-matching-agendamento-multi-visitas/sequencia-matching-agendamento-multi-visitas.mmd`, `Documentacao/REALTIME_PRESENCA_CHAT/STORIES/IN_PROGRESS/ST-008-matching-prestadores-e-agendamento-multi-visitas.md`, `Documentacao/REALTIME_PRESENCA_CHAT/MANUAL_QA_OPERACAO_CHATBOT_TELEGRAM.md`
- Risco/Impacto: medio

- [2026-03-03] [ST-008] Parser de linguagem natural para janela de agendamento no chatbot Telegram
- Tipo: feat
- Resumo: implementado parser dedicado na bridge para interpretar pedido de agenda em linguagem natural (dias da semana, periodo manha/tarde/noite e horario explicito), convertendo para janelas UTC e retornando erros orientados quando faltar dia/periodo ou houver dias insuficientes para a quantidade solicitada.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.TelegramBridge/Services/TelegramSchedulingNaturalLanguageParser.cs`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/Models/TelegramSchedulingModels.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/TelegramSchedulingNaturalLanguageParserTests.cs`, `Documentacao/REALTIME_PRESENCA_CHAT/STORIES/IN_PROGRESS/ST-008-matching-prestadores-e-agendamento-multi-visitas.md`, `Documentacao/REALTIME_PRESENCA_CHAT/MANUAL_QA_OPERACAO_CHATBOT_TELEGRAM.md`
- Risco/Impacto: medio

- [2026-03-03] [ST-008] Agendamento em lote de visitas (ate 3) para o chatbot Telegram
- Tipo: feat
- Resumo: evoluido o fluxo ST-008 com endpoint `POST /api/telegram-chatbot/service-requests/{serviceRequestId}/schedule-visits-batch`, incluindo validacoes de ownership do cliente, limite de ate 3 visitas, bloqueio de dias duplicados e retorno consolidado por visita para suporte a replanejamento conversacional.
- Arquivos principais: `Backend/src/ConsertaPraMim.Application/DTOs/TelegramChatbotSchedulingDTOs.cs`, `Backend/src/ConsertaPraMim.Application/Interfaces/ITelegramChatbotSchedulingService.cs`, `Backend/src/ConsertaPraMim.Application/Services/TelegramChatbotSchedulingService.cs`, `Backend/src/ConsertaPraMim.API/Contracts/TelegramChatbotContracts.cs`, `Backend/src/ConsertaPraMim.API/Controllers/TelegramChatbotController.cs`, `Backend/src/ConsertaPraMim.API/Swagger/ApiEndpointDocumentationCatalog.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/TelegramChatbotSchedulingServiceTests.cs`, `Documentacao/REALTIME_PRESENCA_CHAT/STORIES/IN_PROGRESS/ST-008-matching-prestadores-e-agendamento-multi-visitas.md`, `Documentacao/REALTIME_PRESENCA_CHAT/MANUAL_QA_OPERACAO_CHATBOT_TELEGRAM.md`, `Documentacao/REALTIME_PRESENCA_CHAT/INDEX.md`, `Documentacao/DIAGRAMAS/REALTIME_PRESENCA_CHAT/ST-008-matching-agendamento-multi-visitas/README.md`, `Documentacao/DIAGRAMAS/REALTIME_PRESENCA_CHAT/ST-008-matching-agendamento-multi-visitas/fluxo-matching-agendamento-multi-visitas.mmd`, `Documentacao/DIAGRAMAS/REALTIME_PRESENCA_CHAT/ST-008-matching-agendamento-multi-visitas/sequencia-matching-agendamento-multi-visitas.mmd`, `Documentacao/DIAGRAMAS/INDEX.md`
- Risco/Impacto: medio

- [2026-03-03] [ST-008] Endpoint de matching de prestadores elegiveis para o chatbot Telegram
- Tipo: feat
- Resumo: iniciado o fluxo ST-008 com servico de aplicacao dedicado ao chatbot para listar prestadores elegiveis por pedido e cobertura, incluindo endpoint `GET /api/telegram-chatbot/service-requests/{serviceRequestId}/eligible-providers` com validacao de ownership por `ClientId`.
- Arquivos principais: `Backend/src/ConsertaPraMim.Application/DTOs/TelegramChatbotSchedulingDTOs.cs`, `Backend/src/ConsertaPraMim.Application/Interfaces/ITelegramChatbotSchedulingService.cs`, `Backend/src/ConsertaPraMim.Application/Services/TelegramChatbotSchedulingService.cs`, `Backend/src/ConsertaPraMim.Application/DependencyInjection.cs`, `Backend/src/ConsertaPraMim.API/Controllers/TelegramChatbotController.cs`, `Backend/src/ConsertaPraMim.API/Swagger/ApiEndpointDocumentationCatalog.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/TelegramChatbotSchedulingServiceTests.cs`, `Documentacao/REALTIME_PRESENCA_CHAT/STORIES/IN_PROGRESS/ST-008-matching-prestadores-e-agendamento-multi-visitas.md`, `Documentacao/REALTIME_PRESENCA_CHAT/INDEX.md`, `Documentacao/REALTIME_PRESENCA_CHAT/MANUAL_QA_OPERACAO_CHATBOT_TELEGRAM.md`
- Risco/Impacto: medio

- [2026-03-03] [ST-007] Correcao da abertura automatica de pedido com CEP valido no chatbot Telegram
- Tipo: fix
- Resumo: corrigida incompatibilidade de contrato no payload da triagem para `POST /api/service-requests` (categoria agora enviada como enum numerico compativel com o backend), com pre-resolucao de CEP via `GET /api/service-requests/zip-resolution` para enriquecer endereco/coordenadas e reduzir falhas falsas de "instabilidade" na abertura automatica.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.TelegramBridge/Models/TelegramServiceRequestModels.cs`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/Services/TelegramServiceRequestTriageEngine.cs`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/Services/TelegramChatbotApiClient.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/TelegramServiceRequestTriageEngineTests.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/TelegramChatbotOrchestratorTests.cs`, `Documentacao/REALTIME_PRESENCA_CHAT/MANUAL_QA_OPERACAO_CHATBOT_TELEGRAM.md`, `Documentacao/REALTIME_PRESENCA_CHAT/STORIES/DONE/ST-007-triagem-natural-e-abertura-automatica-de-pedido.md`
- Risco/Impacto: medio

- [2026-03-03] [ST-007] Diagrama Mermaid de sequencia e encerramento da story no board realtime
- Tipo: docs
- Resumo: publicada sequencia da ST-007 com chamadas entre chat bridge, orquestrador de triagem, endpoint de criacao de pedido e persistencia conversacional, com story movida para `STORIES/DONE` e indices atualizados.
- Arquivos principais: `Documentacao/DIAGRAMAS/REALTIME_PRESENCA_CHAT/ST-007-triagem-abertura-automatica-pedido/sequencia-triagem-abertura-automatica-pedido.mmd`, `Documentacao/DIAGRAMAS/REALTIME_PRESENCA_CHAT/ST-007-triagem-abertura-automatica-pedido/README.md`, `Documentacao/DIAGRAMAS/INDEX.md`, `Documentacao/REALTIME_PRESENCA_CHAT/STORIES/DONE/ST-007-triagem-natural-e-abertura-automatica-de-pedido.md`, `Documentacao/REALTIME_PRESENCA_CHAT/INDEX.md`, `Documentacao/REALTIME_PRESENCA_CHAT/MANUAL_QA_OPERACAO_CHATBOT_TELEGRAM.md`
- Risco/Impacto: baixo

- [2026-03-03] [ST-007] Diagrama Mermaid de fluxo da triagem e abertura automatica de pedido
- Tipo: docs
- Resumo: publicado fluxo funcional da ST-007 detalhando analise de intent/entidades, state machine de triagem, validacao de dados minimos, chamada ao endpoint de criacao de pedido e persistencia de snapshots/acoes no historico conversacional.
- Arquivos principais: `Documentacao/DIAGRAMAS/REALTIME_PRESENCA_CHAT/ST-007-triagem-abertura-automatica-pedido/fluxo-triagem-abertura-automatica-pedido.mmd`, `Documentacao/DIAGRAMAS/REALTIME_PRESENCA_CHAT/ST-007-triagem-abertura-automatica-pedido/README.md`, `Documentacao/REALTIME_PRESENCA_CHAT/STORIES/DONE/ST-007-triagem-natural-e-abertura-automatica-de-pedido.md`, `Documentacao/REALTIME_PRESENCA_CHAT/MANUAL_QA_OPERACAO_CHATBOT_TELEGRAM.md`
- Risco/Impacto: baixo

- [2026-03-03] [ST-007] Testes unitarios da triagem e criacao automatica de pedido
- Tipo: test
- Resumo: adicionados testes para validar regras de completude/merge de contexto da triagem (`TelegramServiceRequestTriageEngine`) e cenario de sucesso da abertura automatica de pedido no `TelegramChatbotOrchestrator`, mantendo cobertura dos fluxos de fallback/cache.
- Arquivos principais: `Backend/tests/ConsertaPraMim.Tests.Unit/Services/TelegramServiceRequestTriageEngineTests.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/TelegramChatbotOrchestratorTests.cs`, `Documentacao/REALTIME_PRESENCA_CHAT/STORIES/IN_PROGRESS/ST-007-triagem-natural-e-abertura-automatica-de-pedido.md`, `Documentacao/REALTIME_PRESENCA_CHAT/MANUAL_QA_OPERACAO_CHATBOT_TELEGRAM.md`
- Risco/Impacto: baixo

- [2026-03-03] [ST-007] Triagem natural e abertura automatica de pedido no Telegram Bridge
- Tipo: feat
- Resumo: iniciada a ST-007 com contrato de intent `open_service_request`, state machine de triagem por contexto historico e abertura automatica de pedido via `POST /api/service-requests` quando os dados minimos (categoria, descricao e CEP) estao completos; fluxo tambem persiste payload final e estado de triagem em snapshots/acoes da conversa.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.TelegramBridge/Services/TelegramChatbotOrchestrator.cs`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/Services/TelegramServiceRequestTriageEngine.cs`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/Services/ITelegramChatbotApiClient.cs`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/Services/TelegramChatbotApiClient.cs`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/Models/TelegramServiceRequestModels.cs`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/Program.cs`, `Documentacao/REALTIME_PRESENCA_CHAT/STORIES/IN_PROGRESS/ST-007-triagem-natural-e-abertura-automatica-de-pedido.md`, `Documentacao/REALTIME_PRESENCA_CHAT/INDEX.md`, `Documentacao/REALTIME_PRESENCA_CHAT/MANUAL_QA_OPERACAO_CHATBOT_TELEGRAM.md`
- Risco/Impacto: medio

- [2026-03-03] [ST-006] Diagrama Mermaid de sequencia e encerramento da story no board realtime
- Tipo: docs
- Resumo: publicada sequencia da ST-006 para a orquestracao OpenAI no Telegram Bridge e story movida para `STORIES/DONE`, com atualizacao do board realtime e manual operacional.
- Arquivos principais: `Documentacao/DIAGRAMAS/REALTIME_PRESENCA_CHAT/ST-006-orquestrador-openai-contexto-historico/sequencia-orquestrador-openai-contexto-historico.mmd`, `Documentacao/DIAGRAMAS/REALTIME_PRESENCA_CHAT/ST-006-orquestrador-openai-contexto-historico/README.md`, `Documentacao/REALTIME_PRESENCA_CHAT/STORIES/DONE/ST-006-orquestrador-openai-contexto-historico-linguagem-humana.md`, `Documentacao/REALTIME_PRESENCA_CHAT/INDEX.md`, `Documentacao/REALTIME_PRESENCA_CHAT/MANUAL_QA_OPERACAO_CHATBOT_TELEGRAM.md`, `Documentacao/DIAGRAMAS/INDEX.md`
- Risco/Impacto: baixo

- [2026-03-03] [ST-006] Diagrama Mermaid de fluxo da orquestracao OpenAI no chatbot Telegram
- Tipo: docs
- Resumo: publicado fluxo funcional da ST-006 cobrindo envio da mensagem do cliente, montagem de contexto historico, chamada OpenAI com retries, fallback/cache, persistencia de trilha conversacional e broadcast realtime no Telegram Bridge.
- Arquivos principais: `Documentacao/DIAGRAMAS/REALTIME_PRESENCA_CHAT/ST-006-orquestrador-openai-contexto-historico/fluxo-orquestrador-openai-contexto-historico.mmd`, `Documentacao/DIAGRAMAS/REALTIME_PRESENCA_CHAT/ST-006-orquestrador-openai-contexto-historico/README.md`, `Documentacao/DIAGRAMAS/INDEX.md`, `Documentacao/REALTIME_PRESENCA_CHAT/INDEX.md`, `Documentacao/REALTIME_PRESENCA_CHAT/STORIES/DONE/ST-006-orquestrador-openai-contexto-historico-linguagem-humana.md`, `Documentacao/REALTIME_PRESENCA_CHAT/MANUAL_QA_OPERACAO_CHATBOT_TELEGRAM.md`
- Risco/Impacto: baixo

- [2026-03-03] [ST-006] Orquestrador OpenAI com contexto, fallback e observabilidade no Telegram Bridge
- Tipo: feat
- Resumo: implementado `TelegramChatbotOrchestrator` com prompt de atendimento humano e saida estruturada, montagem de contexto por historico da conversa (`messages/snapshots/actions`), fallback seguro, cache por conversa/mensagem, metricas de custo/latencia e integracao no envio de mensagens para responder automaticamente no chat do cliente.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.TelegramBridge/Services/TelegramChatbotOrchestrator.cs`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/Services/ITelegramChatbotApiClient.cs`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/Services/TelegramChatbotApiClient.cs`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/Controllers/ChatApiController.cs`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/Services/ITelegramChatService.cs`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/Services/TelegramChatService.cs`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/Program.cs`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/appsettings.json`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/appsettings.Development.json`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/TelegramAiResponseParserTests.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/TelegramChatbotOrchestratorTests.cs`, `Documentacao/REALTIME_PRESENCA_CHAT/STORIES/IN_PROGRESS/ST-006-orquestrador-openai-contexto-historico-linguagem-humana.md`, `Documentacao/REALTIME_PRESENCA_CHAT/MANUAL_QA_OPERACAO_CHATBOT_TELEGRAM.md`
- Risco/Impacto: medio

- [2026-03-03] [ST-006] Gateway OpenAI resiliente para orquestracao do chatbot Telegram
- Tipo: feat
- Resumo: criada a fundacao tecnica da ST-006 no `Telegram Bridge` com gateway dedicado para a OpenAI (`Responses API`), incluindo timeout por chamada, retries para erros transientes, parse de tokens/erros e modelos/opcoes de configuracao da trilha de IA (`TelegramBridgeAi`).
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.TelegramBridge/Services/OpenAiTelegramGateway.cs`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/Services/ITelegramAiGateway.cs`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/Options/TelegramBridgeAiOptions.cs`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/Models/TelegramAiModels.cs`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/Services/TelegramAiResponseParser.cs`, `Documentacao/REALTIME_PRESENCA_CHAT/STORIES/IN_PROGRESS/ST-006-orquestrador-openai-contexto-historico-linguagem-humana.md`, `Documentacao/REALTIME_PRESENCA_CHAT/MANUAL_QA_OPERACAO_CHATBOT_TELEGRAM.md`
- Risco/Impacto: medio

- [2026-03-03] [ST-005] Story concluida e movida para DONE no board realtime
- Tipo: docs
- Resumo: ST-005 foi encerrada com todas as tasks concluidas, movida de `STORIES/IN_PROGRESS` para `STORIES/DONE` e board da trilha realtime atualizado.
- Arquivos principais: `Documentacao/REALTIME_PRESENCA_CHAT/STORIES/DONE/ST-005-login-cliente-telegram-bridge-e-vinculo-conversa.md`, `Documentacao/REALTIME_PRESENCA_CHAT/INDEX.md`
- Risco/Impacto: baixo

- [2026-03-03] [ST-005] Diagrama Mermaid de sequencia e indices da trilha atualizados
- Tipo: docs
- Resumo: publicada sequencia detalhada da ST-005 (login, sessao, conversa unica, SignalR e envio de mensagem), com atualizacao dos indices de diagramas e do board de realtime para incluir a story.
- Arquivos principais: `Documentacao/DIAGRAMAS/REALTIME_PRESENCA_CHAT/ST-005-login-cliente-telegram-bridge-vinculo-conversa/sequencia-login-cliente-vinculo-conversa.mmd`, `Documentacao/DIAGRAMAS/REALTIME_PRESENCA_CHAT/ST-005-login-cliente-telegram-bridge-vinculo-conversa/README.md`, `Documentacao/DIAGRAMAS/INDEX.md`, `Documentacao/REALTIME_PRESENCA_CHAT/INDEX.md`, `Documentacao/REALTIME_PRESENCA_CHAT/STORIES/IN_PROGRESS/ST-005-login-cliente-telegram-bridge-e-vinculo-conversa.md`, `Documentacao/REALTIME_PRESENCA_CHAT/MANUAL_QA_OPERACAO_CHATBOT_TELEGRAM.md`
- Risco/Impacto: baixo

- [2026-03-03] [ST-005] Diagrama Mermaid de fluxo do login e vinculo de conversa no Telegram Bridge
- Tipo: docs
- Resumo: publicado fluxo funcional da ST-005 cobrindo autenticacao por email/senha, criacao automatica de conversa unica por cliente, regras de autorizacao em API/Hub e envio de mensagens com persistencia.
- Arquivos principais: `Documentacao/DIAGRAMAS/REALTIME_PRESENCA_CHAT/ST-005-login-cliente-telegram-bridge-vinculo-conversa/fluxo-login-cliente-vinculo-conversa.mmd`, `Documentacao/DIAGRAMAS/REALTIME_PRESENCA_CHAT/ST-005-login-cliente-telegram-bridge-vinculo-conversa/README.md`, `Documentacao/REALTIME_PRESENCA_CHAT/STORIES/IN_PROGRESS/ST-005-login-cliente-telegram-bridge-e-vinculo-conversa.md`, `Documentacao/REALTIME_PRESENCA_CHAT/MANUAL_QA_OPERACAO_CHATBOT_TELEGRAM.md`
- Risco/Impacto: baixo

- [2026-03-03] [ST-005] Testes de login e autorizacao basica no Telegram Bridge
- Tipo: test
- Resumo: adicionados testes unitarios cobrindo login valido, erro de credencial e regras de autorizacao basica da bridge (controladores/hub protegidos e login anonimo), com referencia direta ao projeto web do Telegram Bridge no projeto de testes.
- Arquivos principais: `Backend/tests/ConsertaPraMim.Tests.Unit/ConsertaPraMim.Tests.Unit.csproj`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/TelegramBridgeAccountControllerTests.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/TelegramBridgeAuthorizationTests.cs`, `Documentacao/REALTIME_PRESENCA_CHAT/STORIES/IN_PROGRESS/ST-005-login-cliente-telegram-bridge-e-vinculo-conversa.md`, `Documentacao/REALTIME_PRESENCA_CHAT/MANUAL_QA_OPERACAO_CHATBOT_TELEGRAM.md`
- Risco/Impacto: baixo

- [2026-03-03] [ST-005] Conversa unica automatica por cliente no Telegram Bridge
- Tipo: feat
- Resumo: login no bridge passa a abrir automaticamente uma unica conversa por `ClientId`, sem campo manual de `chatId`; endpoints e SignalR agora bloqueiam acesso a conversas de outros clientes, `chatId` passa a ser serializado como string para evitar perda de precisao no frontend e chamadas API/Hub retornam `401/403` sem redirecionamento em loop.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.TelegramBridge/Controllers/ChatApiController.cs`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/Hubs/TelegramChatHub.cs`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/Security/TelegramBridgeClientConversation.cs`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/Models/ChatConversationSummaryDto.cs`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/Models/ChatMessageDto.cs`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/Program.cs`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/Properties/launchSettings.json`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/Services/ITelegramChatService.cs`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/Services/TelegramChatService.cs`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/Services/ITelegramChatbotApiClient.cs`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/Services/TelegramChatbotApiClient.cs`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/Views/Home/Index.cshtml`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/wwwroot/js/chat.js`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/README.md`, `Documentacao/REALTIME_PRESENCA_CHAT/STORIES/IN_PROGRESS/ST-005-login-cliente-telegram-bridge-e-vinculo-conversa.md`, `Documentacao/REALTIME_PRESENCA_CHAT/MANUAL_QA_OPERACAO_CHATBOT_TELEGRAM.md`
- Risco/Impacto: medio

- [2026-03-03] [ST-005] Fluxo de logout do Telegram Bridge com limpeza de sessao
- Tipo: feat
- Resumo: implementado `POST /Account/Logout` com antiforgery e `SignOutAsync`, adicionando botao `Sair` na interface do chat para invalidar cookie local e remover acesso imediato as rotas protegidas.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.TelegramBridge/Controllers/AccountController.cs`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/Views/Home/Index.cshtml`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/wwwroot/css/site.css`, `Documentacao/REALTIME_PRESENCA_CHAT/STORIES/IN_PROGRESS/ST-005-login-cliente-telegram-bridge-e-vinculo-conversa.md`
- Risco/Impacto: baixo

- [2026-03-03] [ST-005] Vinculo do `ClientId` da sessao da bridge com a API do chatbot
- Tipo: feat
- Resumo: `ChatApiController` passou a sincronizar abertura de sessao e mensagens de saida com `/api/telegram-chatbot/session` e `/api/telegram-chatbot/messages` usando `Bearer` token da sessao autenticada, garantindo derivacao de `ClientId` no backend.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.TelegramBridge/Controllers/ChatApiController.cs`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/Services/ITelegramChatbotApiClient.cs`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/Services/TelegramChatbotApiClient.cs`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/Program.cs`, `Documentacao/REALTIME_PRESENCA_CHAT/STORIES/IN_PROGRESS/ST-005-login-cliente-telegram-bridge-e-vinculo-conversa.md`
- Risco/Impacto: medio

- [2026-03-03] [ST-005] Rotas de chat do Telegram Bridge protegidas por autenticacao
- Tipo: feat
- Resumo: aplicados atributos `[Authorize]` no `HomeController`, `ChatApiController` e `TelegramChatHub`, garantindo redirecionamento de anonimos para login nas telas web e bloqueio de chamadas de chat sem sessao autenticada.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.TelegramBridge/Controllers/HomeController.cs`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/Controllers/ChatApiController.cs`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/Hubs/TelegramChatHub.cs`, `Documentacao/REALTIME_PRESENCA_CHAT/STORIES/IN_PROGRESS/ST-005-login-cliente-telegram-bridge-e-vinculo-conversa.md`
- Risco/Impacto: medio

- [2026-03-03] [ST-005] Sessao de autenticacao do Telegram Bridge persistida com cookie seguro
- Tipo: feat
- Resumo: bridge passou a usar cookie auth para manter sessao do cliente apos login valido, com `SignInAsync` armazenando token da API em claim e configuracao de cookie com `HttpOnly`, `SameSite=Strict`, expiracao e `SlidingExpiration`.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.TelegramBridge/Program.cs`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/Controllers/AccountController.cs`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/Security/TelegramBridgeClaimTypes.cs`, `Documentacao/REALTIME_PRESENCA_CHAT/STORIES/IN_PROGRESS/ST-005-login-cliente-telegram-bridge-e-vinculo-conversa.md`
- Risco/Impacto: medio

- [2026-03-03] [ST-005] Login do Telegram Bridge integrado ao endpoint oficial de autenticacao da API
- Tipo: feat
- Resumo: implementado `TelegramBridgeAuthApiClient` consumindo `POST /api/auth/login` com `ApiBaseUrl` configuravel; `AccountController` passou a validar credenciais reais da plataforma e role `Client`, sem duplicar regra de senha no frontend.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.TelegramBridge/Services/ITelegramBridgeAuthApiClient.cs`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/Services/TelegramBridgeAuthApiClient.cs`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/Controllers/AccountController.cs`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/Models/TelegramBridgeLoginResponse.cs`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/Program.cs`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/appsettings.json`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/appsettings.Development.json`, `Documentacao/REALTIME_PRESENCA_CHAT/STORIES/IN_PROGRESS/ST-005-login-cliente-telegram-bridge-e-vinculo-conversa.md`
- Risco/Impacto: medio

- [2026-03-03] [ST-005] Tela e controller de login criados no Telegram Bridge
- Tipo: feat
- Resumo: adicionados `AccountController` e view de login com email/senha no `ConsertaPraMim.Web.TelegramBridge`, incluindo ajuste de layout para carregar `chat.js` apenas na tela do chat e evitar falhas em paginas de autenticacao.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.TelegramBridge/Controllers/AccountController.cs`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/Models/LoginViewModel.cs`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/Views/Account/Login.cshtml`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/Views/Shared/_Layout.cshtml`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/Views/Home/Index.cshtml`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/wwwroot/css/site.css`, `Documentacao/REALTIME_PRESENCA_CHAT/STORIES/IN_PROGRESS/ST-005-login-cliente-telegram-bridge-e-vinculo-conversa.md`
- Risco/Impacto: medio

- [2026-03-03] [ST-004] Story de fundacao do chatbot Telegram concluida e movida para DONE
- Tipo: docs
- Resumo: ST-004 foi encerrada com todas as tasks marcadas, arquivo movido para `STORIES/DONE` e board `INDEX` atualizado para refletir conclusao da base de API/persistencia conversacional.
- Arquivos principais: `Documentacao/REALTIME_PRESENCA_CHAT/STORIES/DONE/ST-004-fundacao-api-chatbot-telegram-persistencia.md`, `Documentacao/REALTIME_PRESENCA_CHAT/INDEX.md`
- Risco/Impacto: baixo

- [2026-03-03] [ST-004] Diagrama de sequencia Mermaid da API conversacional do chatbot Telegram
- Tipo: docs
- Resumo: publicado diagrama de sequencia da ST-004 detalhando chamadas entre bridge, controller, servico, repositorio e banco para sessao, mensagens, contexto, acoes e historico com validacao por `ClientId`.
- Arquivos principais: `Documentacao/DIAGRAMAS/REALTIME_PRESENCA_CHAT/ST-004-fundacao-api-chatbot-telegram-persistencia/sequencia-api-chatbot-telegram-persistencia.mmd`, `Documentacao/DIAGRAMAS/REALTIME_PRESENCA_CHAT/ST-004-fundacao-api-chatbot-telegram-persistencia/README.md`, `Documentacao/REALTIME_PRESENCA_CHAT/STORIES/IN_PROGRESS/ST-004-fundacao-api-chatbot-telegram-persistencia.md`
- Risco/Impacto: baixo

- [2026-03-03] [ST-004] Diagrama de fluxo Mermaid da API conversacional do chatbot Telegram
- Tipo: docs
- Resumo: publicado fluxo funcional da ST-004 cobrindo sessao, registro de mensagens/contexto/acoes, consulta de historico e bloqueio de acesso cruzado por `ClientId`.
- Arquivos principais: `Documentacao/DIAGRAMAS/REALTIME_PRESENCA_CHAT/ST-004-fundacao-api-chatbot-telegram-persistencia/fluxo-api-chatbot-telegram-persistencia.mmd`, `Documentacao/DIAGRAMAS/REALTIME_PRESENCA_CHAT/ST-004-fundacao-api-chatbot-telegram-persistencia/README.md`, `Documentacao/REALTIME_PRESENCA_CHAT/INDEX.md`, `Documentacao/REALTIME_PRESENCA_CHAT/STORIES/IN_PROGRESS/ST-004-fundacao-api-chatbot-telegram-persistencia.md`
- Risco/Impacto: baixo

- [2026-03-03] [ST-004] Testes unitarios e integracao para persistencia/autorizacao do chatbot Telegram
- Tipo: test
- Resumo: adicionados testes para `TelegramChatbotConversationService` e `TelegramChatbotController` cobrindo criacao de sessao, normalizacao UTC, validacao de tokens e bloqueio de acesso cruzado entre clientes usando base SQLite em memoria.
- Arquivos principais: `Backend/tests/ConsertaPraMim.Tests.Unit/Services/TelegramChatbotConversationServiceTests.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/Integration/Controllers/TelegramChatbotControllerSqliteIntegrationTests.cs`, `Documentacao/REALTIME_PRESENCA_CHAT/STORIES/IN_PROGRESS/ST-004-fundacao-api-chatbot-telegram-persistencia.md`, `Documentacao/REALTIME_PRESENCA_CHAT/MANUAL_QA_OPERACAO_CHATBOT_TELEGRAM.md`
- Risco/Impacto: baixo

- [2026-03-03] [ST-004] Politica UTC, isolamento por cliente e paridade OpenAPI do chatbot Telegram consolidados
- Tipo: feat
- Resumo: consolidada a regra de timestamps em UTC no fluxo conversacional (persistencia e retorno), com isolamento por `ClientId` no servico/controlador e documentacao Swagger alinhada nos tres arquivos obrigatorios (`ApiEndpointDocumentationCatalog`, `ComprehensiveSwaggerOperationFilter`, `ApiTagDescriptionsDocumentFilter`).
- Arquivos principais: `Backend/src/ConsertaPraMim.Application/Services/TelegramChatbotConversationService.cs`, `Backend/src/ConsertaPraMim.API/Controllers/TelegramChatbotController.cs`, `Backend/src/ConsertaPraMim.API/Swagger/ApiEndpointDocumentationCatalog.cs`, `Backend/src/ConsertaPraMim.API/Swagger/ComprehensiveSwaggerOperationFilter.cs`, `Backend/src/ConsertaPraMim.API/Swagger/ApiTagDescriptionsDocumentFilter.cs`, `Documentacao/REALTIME_PRESENCA_CHAT/STORIES/IN_PROGRESS/ST-004-fundacao-api-chatbot-telegram-persistencia.md`
- Risco/Impacto: medio

- [2026-03-03] [ST-004] Endpoints `/api/telegram-chatbot/*` com historico, acoes e documentacao Swagger dedicada
- Tipo: feat
- Resumo: criado `TelegramChatbotController` com endpoints para abrir sessao, registrar mensagem, registrar contexto, registrar acao, atualizar estado e consultar historico conversacional; atualizada documentacao Swagger/OpenAPI para dominio de chatbot Telegram com narrativa de negocio e tecnica.
- Arquivos principais: `Backend/src/ConsertaPraMim.API/Controllers/TelegramChatbotController.cs`, `Backend/src/ConsertaPraMim.API/Contracts/TelegramChatbotContracts.cs`, `Backend/src/ConsertaPraMim.API/Swagger/ApiEndpointDocumentationCatalog.cs`, `Backend/src/ConsertaPraMim.API/Swagger/ComprehensiveSwaggerOperationFilter.cs`, `Backend/src/ConsertaPraMim.API/Swagger/ApiTagDescriptionsDocumentFilter.cs`, `Documentacao/REALTIME_PRESENCA_CHAT/STORIES/IN_PROGRESS/ST-004-fundacao-api-chatbot-telegram-persistencia.md`
- Risco/Impacto: medio

- [2026-03-03] [ST-004] Servico de aplicacao e repositorio para trilha conversacional do chatbot Telegram
- Tipo: feat
- Resumo: implementado repositorio dedicado do chatbot e servico de aplicacao para abrir/retomar conversa, registrar mensagens de entrada/saida, snapshots de contexto, action logs e atualizar estado conversacional com normalizacao UTC e validacoes de payload/token.
- Arquivos principais: `Backend/src/ConsertaPraMim.Domain/Repositories/IChatbotConversationRepository.cs`, `Backend/src/ConsertaPraMim.Infrastructure/Repositories/ChatbotConversationRepository.cs`, `Backend/src/ConsertaPraMim.Application/DTOs/TelegramChatbotDTOs.cs`, `Backend/src/ConsertaPraMim.Application/Interfaces/ITelegramChatbotConversationService.cs`, `Backend/src/ConsertaPraMim.Application/Services/TelegramChatbotConversationService.cs`, `Backend/src/ConsertaPraMim.Application/DependencyInjection.cs`, `Backend/src/ConsertaPraMim.Infrastructure/DependencyInjection.cs`, `Documentacao/REALTIME_PRESENCA_CHAT/STORIES/IN_PROGRESS/ST-004-fundacao-api-chatbot-telegram-persistencia.md`
- Risco/Impacto: medio

- [2026-03-03] [ST-004] Mapeamento EF Core e migration inicial do armazenamento conversacional do chatbot
- Tipo: feat
- Resumo: adicionados `DbSet` e configuracoes EF Core para as quatro entidades do chatbot (indices, constraints, relacoes e campos de auditoria), com migration `AddTelegramChatbotConversationFoundation` e snapshot atualizado do contexto.
- Arquivos principais: `Backend/src/ConsertaPraMim.Infrastructure/Data/ConsertaPraMimDbContext.cs`, `Backend/src/ConsertaPraMim.Infrastructure/Migrations/20260303181725_AddTelegramChatbotConversationFoundation.cs`, `Backend/src/ConsertaPraMim.Infrastructure/Migrations/20260303181725_AddTelegramChatbotConversationFoundation.Designer.cs`, `Backend/src/ConsertaPraMim.Infrastructure/Migrations/ConsertaPraMimDbContextModelSnapshot.cs`, `Documentacao/REALTIME_PRESENCA_CHAT/STORIES/IN_PROGRESS/ST-004-fundacao-api-chatbot-telegram-persistencia.md`
- Risco/Impacto: medio

- [2026-03-03] [ST-004] Entidades de dominio base para persistencia conversacional do chatbot Telegram
- Tipo: feat
- Resumo: adicionadas entidades de dominio `ChatbotConversation`, `ChatbotMessage`, `ChatbotContextSnapshot` e `ChatbotActionLog`, com enums de estado/direcao para estruturar trilha conversacional e auditavel do chatbot.
- Arquivos principais: `Backend/src/ConsertaPraMim.Domain/Entities/ChatbotConversation.cs`, `Backend/src/ConsertaPraMim.Domain/Entities/ChatbotMessage.cs`, `Backend/src/ConsertaPraMim.Domain/Entities/ChatbotContextSnapshot.cs`, `Backend/src/ConsertaPraMim.Domain/Entities/ChatbotActionLog.cs`, `Backend/src/ConsertaPraMim.Domain/Enums/ChatbotEnums.cs`, `Documentacao/REALTIME_PRESENCA_CHAT/STORIES/IN_PROGRESS/ST-004-fundacao-api-chatbot-telegram-persistencia.md`, `Documentacao/REALTIME_PRESENCA_CHAT/INDEX.md`
- Risco/Impacto: medio

- [2026-03-03] [ST-003] Sanitizacao de segredo e higiene de artefatos locais no Telegram Bridge
- Tipo: fix
- Resumo: removido token real do bot Telegram dos `appsettings` versionados da bridge e adicionadas regras no `.gitignore` do projeto para nao versionar uploads locais e arquivo `*.csproj.user`.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.TelegramBridge/appsettings.json`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/appsettings.Development.json`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/.gitignore`
- Risco/Impacto: baixo

- [2026-03-03] [ST-003] Novo projeto .NET 8 para atendimento Telegram com UI estilo WhatsApp
- Tipo: feat
- Resumo: criado o projeto `ConsertaPraMim.Web.TelegramBridge` (ASP.NET Core MVC net8.0) com chat em tempo real via SignalR, integracao com Telegram Bot API por polling (`getUpdates`), envio/recebimento de anexos (imagem/video/documento), persistencia local de arquivos em `wwwroot/uploads/telegram-bridge` e painel visual inspirado no WhatsApp para operacao.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.TelegramBridge/ConsertaPraMim.Web.TelegramBridge.csproj`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/Program.cs`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/Controllers/ChatApiController.cs`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/Hubs/TelegramChatHub.cs`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/Services/TelegramBotApiClient.cs`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/Services/TelegramAttachmentStorage.cs`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/Services/TelegramChatService.cs`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/Services/TelegramLongPollingBackgroundService.cs`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/Views/Home/Index.cshtml`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/wwwroot/css/site.css`, `Backend/src/ConsertaPraMim.Web.TelegramBridge/wwwroot/js/chat.js`, `Documentacao/REALTIME_PRESENCA_CHAT/STORIES/DONE/ST-003-telegram-bridge-web-whatsapp-realtime-anexos.md`, `Documentacao/REALTIME_PRESENCA_CHAT/MANUAL_QA_OPERACAO_TELEGRAM_BRIDGE_ST-003.md`
- Risco/Impacto: medio

- [2026-02-25] [ST-008] Criacao de usuario Admin no portal com modal Bootstrap e overlay de status
- Tipo: feat
- Resumo: modulo `Usuarios` agora permite criar contas com role `Admin` via modal dedicado (`Novo Admin`), exibindo feedback em overlay para estados de requisicao (`Salvando`, `Salvo com sucesso`, `Erro`) e cobrindo o fluxo E2E no backend com auditoria de criacao.
- Arquivos principais: `Backend/src/ConsertaPraMim.Application/DTOs/AdminUsersDTOs.cs`, `Backend/src/ConsertaPraMim.Application/Interfaces/IAdminUserService.cs`, `Backend/src/ConsertaPraMim.Application/Services/AdminUserService.cs`, `Backend/src/ConsertaPraMim.API/Controllers/AdminUsersController.cs`, `Backend/src/ConsertaPraMim.API/Swagger/ApiEndpointDocumentationCatalog.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Controllers/AdminUsersController.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Services/IAdminUsersApiClient.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Services/AdminUsersApiClient.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminUsers/Index.cshtml`, `Backend/src/ConsertaPraMim.Web.Admin/wwwroot/js/views/admin-users/index.js`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/AdminUserServiceTests.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/AdminUsersControllerTests.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/Integration/E2E/AdminUsersAdminProvisioningE2EInMemoryIntegrationTests.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`
- Risco/Impacto: medio

- [2026-02-25] [ST-052] Fechamento E2E do AI Copilot com manual QA e story movida para DONE
- Tipo: docs
- Resumo: manual QA/Operacao recebeu cobertura dedicada do modulo `AI Copilot Growth` (caso `QA-ADM-051`, smoke/regressao/troubleshooting), foi publicado guia operacional `AI_COPILOT_ST-052.md` e a story ST-052 foi concluida/movida para `DONE` no board.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`, `Documentacao/ADMIN_PORTAL/GROWTH/AI_COPILOT_ST-052.md`, `Documentacao/ADMIN_PORTAL/STORIES/DONE/ST-052-ai-copilot-growth-liquidez.md`, `Documentacao/ADMIN_PORTAL/INDEX.md`
- Risco/Impacto: baixo

- [2026-02-25] [ST-052] Prompt IA enriquecido com contexto do cockpit growth e governanca semanal/mensal
- Tipo: feat
- Resumo: a analise do `AI Copilot Growth` passou a consumir, alem de funnel e score de liquidez, os KPIs/North Star do `Cockpit Growth`, tendencia semanal e o contexto mais recente dos rituais semanal/mensal para gerar recomendacoes mais conectadas ao negocio e ao plano de execucao.
- Arquivos principais: `Backend/src/ConsertaPraMim.Application/Services/AdminGrowthAiService.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/AdminGrowthAiServiceTests.cs`, `Documentacao/ADMIN_PORTAL/STORIES/IN_PROGRESS/ST-052-ai-copilot-growth-liquidez.md`
- Risco/Impacto: medio

- [2026-02-25] [ST-052] Portal admin com modulo AI Copilot Growth (configuracao + analise)
- Tipo: feat
- Resumo: portal admin ganhou novo item de menu `AI Copilot Growth`, com tela dedicada para configurar OpenAI (habilitar, modelo, prompt, API key mascarada) e executar analises assistidas por IA sobre growth funnel/liquidez, incluindo visualizacao da ultima rodada e historico recente.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Admin/Services/IAdminOperationsApiClient.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Services/AdminOperationsApiClient.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Controllers/AdminGrowthAiController.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Models/AdminGrowthAiViewModel.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminGrowthAi/Index.cshtml`, `Backend/src/ConsertaPraMim.Web.Admin/Views/Shared/_Layout.cshtml`, `Documentacao/ADMIN_PORTAL/STORIES/IN_PROGRESS/ST-052-ai-copilot-growth-liquidez.md`
- Risco/Impacto: medio

- [2026-02-25] [ST-052] Backend do AI Copilot com configuracao persistida e endpoints de analise
- Tipo: feat
- Resumo: implementado backend E2E do copiloto de growth com snapshot em `SystemSettings` (configuracao + historico), servico de orquestracao `AdminGrowthAiService`, integracao com OpenAI Responses API (`OpenAiGrowthAiGateway`) e novos endpoints admin em `/api/admin/growth/ai/*` para snapshot, configuracao e analise assistida com dados reais de funnel/liquidez.
- Arquivos principais: `Backend/src/ConsertaPraMim.Application/DTOs/AdminGrowthAiDTOs.cs`, `Backend/src/ConsertaPraMim.Application/Interfaces/IAdminGrowthAiService.cs`, `Backend/src/ConsertaPraMim.Application/Interfaces/IAdminGrowthAiStore.cs`, `Backend/src/ConsertaPraMim.Application/Interfaces/IAdminGrowthAiGateway.cs`, `Backend/src/ConsertaPraMim.Application/Services/AdminGrowthAiService.cs`, `Backend/src/ConsertaPraMim.Infrastructure/Services/AdminGrowthAiSystemSettingsStore.cs`, `Backend/src/ConsertaPraMim.Infrastructure/Services/OpenAiGrowthAiGateway.cs`, `Backend/src/ConsertaPraMim.API/Controllers/AdminGrowthController.cs`, `Backend/src/ConsertaPraMim.API/Swagger/ApiEndpointDocumentationCatalog.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/AdminGrowthAiServiceTests.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/AdminGrowthControllerAiTests.cs`, `Documentacao/ADMIN_PORTAL/STORIES/IN_PROGRESS/ST-052-ai-copilot-growth-liquidez.md`
- Risco/Impacto: medio

- [2026-02-25] [ST-052] Backlog inicial do AI Copilot para growth funnel e liquidez
- Tipo: docs
- Resumo: criado o `EPIC-021` e iniciada a `ST-052` com criterios de aceite e plano incremental para integrar OpenAI no portal admin (configuracao segura da API key, analise assistida de funil/liquidez e historico de execucao), com registro no board oficial.
- Arquivos principais: `Documentacao/ADMIN_PORTAL/EPICS/EPIC-021-ai-growth-liquidez-copilot.md`, `Documentacao/ADMIN_PORTAL/STORIES/IN_PROGRESS/ST-052-ai-copilot-growth-liquidez.md`, `Documentacao/ADMIN_PORTAL/INDEX.md`
- Risco/Impacto: baixo

- [2026-02-24] [ST-051] Processo de revisao mensal estrategica no Cockpit Growth
- Tipo: feat
- Resumo: criada trilha mensal de governanca no cockpit com endpoints `GET /api/admin/growth/monthly-review` e `POST /api/admin/growth/monthly-review/record`, agenda executiva padronizada, formulario de fechamento mensal e historico de atas para orientar bets, riscos e alocacao de capacidade/budget.
- Arquivos principais: `Backend/src/ConsertaPraMim.Application/DTOs/AdminGrowthDTOs.cs`, `Backend/src/ConsertaPraMim.Application/Interfaces/IAdminGrowthService.cs`, `Backend/src/ConsertaPraMim.Application/Services/AdminGrowthService.cs`, `Backend/src/ConsertaPraMim.API/Controllers/AdminGrowthController.cs`, `Backend/src/ConsertaPraMim.API/Swagger/ApiEndpointDocumentationCatalog.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Services/IAdminOperationsApiClient.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Services/AdminOperationsApiClient.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Controllers/AdminGrowthCockpitController.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminGrowthCockpit/Index.cshtml`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/AdminGrowthControllerReactivationTests.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/AdminGrowthServiceReactivationTests.cs`, `Documentacao/ADMIN_PORTAL/GROWTH/REVISAO_MENSAL_ESTRATEGIA_ST-051.md`, `Documentacao/ADMIN_PORTAL/STORIES/DONE/ST-051-cockpit-growth-northstar.md`, `Documentacao/ADMIN_PORTAL/INDEX.md`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`
- Risco/Impacto: medio
- [2026-02-24] [ST-051] Cockpit Growth integrado ao roadmap/backlog com status de entrega
- Tipo: feat
- Resumo: o `Cockpit Growth` passou a consumir snapshot do roadmap (`Backlog`, `In Progress`, `Done`) com taxa de entrega, taxa de execucao ativa e lista priorizada de stories criticas, conectando KPI executivo com capacidade real de entrega e links para `Roadmap`/`Wiki`.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Admin/Controllers/AdminGrowthCockpitController.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Models/AdminGrowthCockpitViewModel.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminGrowthCockpit/Index.cshtml`, `Documentacao/ADMIN_PORTAL/GROWTH/ROADMAP_ENTREGA_ST-051.md`, `Documentacao/ADMIN_PORTAL/STORIES/DONE/ST-051-cockpit-growth-northstar.md`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`
- Risco/Impacto: baixo
- [2026-02-24] [ST-051] Ritual semanal de growth com ata e owners no cockpit admin
- Tipo: feat
- Resumo: adicionado fluxo semanal de governanca no `Cockpit Growth` com pauta fixa, formulario de ata e historico recente; API recebeu os endpoints `GET /api/admin/growth/weekly-ritual` e `POST /api/admin/growth/weekly-ritual/record` para registrar decisoes, owners, riscos e proximos passos.
- Arquivos principais: `Backend/src/ConsertaPraMim.Application/DTOs/AdminGrowthDTOs.cs`, `Backend/src/ConsertaPraMim.Application/Interfaces/IAdminGrowthService.cs`, `Backend/src/ConsertaPraMim.Application/Services/AdminGrowthService.cs`, `Backend/src/ConsertaPraMim.API/Controllers/AdminGrowthController.cs`, `Backend/src/ConsertaPraMim.API/Swagger/ApiEndpointDocumentationCatalog.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Services/IAdminOperationsApiClient.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Services/AdminOperationsApiClient.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Controllers/AdminGrowthCockpitController.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminGrowthCockpit/Index.cshtml`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/AdminGrowthControllerReactivationTests.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/AdminGrowthServiceReactivationTests.cs`, `Documentacao/ADMIN_PORTAL/GROWTH/RITUAL_SEMANAL_ST-051.md`, `Documentacao/ADMIN_PORTAL/STORIES/DONE/ST-051-cockpit-growth-northstar.md`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`
- Risco/Impacto: medio
- [2026-02-24] [ST-051] Cockpit executivo de growth no portal admin com endpoint dedicado
- Tipo: feat
- Resumo: implementado o endpoint `GET /api/admin/growth/executive-cockpit` com North Star `RQ72`, metas trimestrais, KPIs de guardrail e tendencia semanal; portal admin ganhou o menu `Cockpit Growth` com painel executivo para leitura de performance e tomada de decisao.
- Arquivos principais: `Backend/src/ConsertaPraMim.Application/DTOs/AdminGrowthDTOs.cs`, `Backend/src/ConsertaPraMim.Application/Interfaces/IAdminGrowthService.cs`, `Backend/src/ConsertaPraMim.Application/Services/AdminGrowthService.cs`, `Backend/src/ConsertaPraMim.API/Controllers/AdminGrowthController.cs`, `Backend/src/ConsertaPraMim.API/Swagger/ApiEndpointDocumentationCatalog.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Controllers/AdminGrowthCockpitController.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminGrowthCockpit/Index.cshtml`, `Backend/src/ConsertaPraMim.Web.Admin/Services/IAdminOperationsApiClient.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Services/AdminOperationsApiClient.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Views/Shared/_Layout.cshtml`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/AdminGrowthControllerReactivationTests.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/AdminGrowthServiceReactivationTests.cs`, `Documentacao/ADMIN_PORTAL/STORIES/DONE/ST-051-cockpit-growth-northstar.md`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`
- Risco/Impacto: medio
- [2026-02-24] [ST-051] North Star metric formalizada com metas trimestrais de growth
- Tipo: docs
- Resumo: ST-051 foi iniciada em `In Progress` com definicao oficial da North Star `RQ72` (resolucao qualificada em ate 72h), guardrails operacionais e metas por trimestre com ownership para a governanca executiva de growth.
- Arquivos principais: `Documentacao/ADMIN_PORTAL/GROWTH/NORTH_STAR_ST-051.md`, `Documentacao/ADMIN_PORTAL/STORIES/DONE/ST-051-cockpit-growth-northstar.md`, `Documentacao/ADMIN_PORTAL/INDEX.md`
- Risco/Impacto: baixo
- [2026-02-24] [ST-050] Regra operacional de acionamento de recompra com auditoria e deduplicacao
- Tipo: feat
- Resumo: adicionado disparo admin de recompra (`POST /api/reviews/admin/repurchase/run`) com janela temporal configuravel, supressoes de elegibilidade (ja recomprou, sem review positiva, ja acionado), notificacao ao cliente e trilha auditavel `ClientRepurchaseTrigger` para evitar reenvio indevido.
- Arquivos principais: `Backend/src/ConsertaPraMim.Application/Interfaces/IReviewRetentionService.cs`, `Backend/src/ConsertaPraMim.Application/DTOs/ReviewDTOs.cs`, `Backend/src/ConsertaPraMim.Application/Services/ReviewService.cs`, `Backend/src/ConsertaPraMim.Application/DependencyInjection.cs`, `Backend/src/ConsertaPraMim.API/Controllers/ReviewsController.cs`, `Backend/src/ConsertaPraMim.API/Swagger/ApiEndpointDocumentationCatalog.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/ReviewServiceTests.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`, `Documentacao/ADMIN_PORTAL/STORIES/IN_PROGRESS/ST-050-pos-servico-avaliacao-recompra.md`
- Risco/Impacto: medio
- [2026-02-24] [ST-050] Coleta pos-conclusao de reviews pendentes para cliente e prestador
- Tipo: feat
- Resumo: adicionados endpoints de pendencias de avaliacao pos-servico (`GET /api/reviews/client/pending` e `GET /api/reviews/provider/pending`) com janela operacional, exclusao de itens ja avaliados e payload de prazo restante; adapters web de review foram atualizados para o novo contrato e o manual QA ganhou cobertura dedicada (`QA-ADM-043`).
- Arquivos principais: `Backend/src/ConsertaPraMim.Application/Interfaces/IReviewService.cs`, `Backend/src/ConsertaPraMim.Application/DTOs/ReviewDTOs.cs`, `Backend/src/ConsertaPraMim.Application/Services/ReviewService.cs`, `Backend/src/ConsertaPraMim.Infrastructure/Repositories/ServiceRequestRepository.cs`, `Backend/src/ConsertaPraMim.API/Controllers/ReviewsController.cs`, `Backend/src/ConsertaPraMim.API/Swagger/ApiEndpointDocumentationCatalog.cs`, `Backend/src/ConsertaPraMim.Web.Client/Services/ClientApiReviewService.cs`, `Backend/src/ConsertaPraMim.Web.Provider/Services/ProviderApiReviewService.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/ReviewServiceTests.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`, `Documentacao/ADMIN_PORTAL/STORIES/IN_PROGRESS/ST-050-pos-servico-avaliacao-recompra.md`
- Risco/Impacto: medio
- [2026-02-24] [ST-050] Questionario estruturado de avaliacao pos-servico com score composto
- Tipo: feat
- Resumo: fluxo de `reviews` passou a aceitar questionario estruturado (qualidade, pontualidade, comunicacao, custo-beneficio, NPS e intencao de recompra), com persistencia em banco, score composto (0-100), validacoes de faixa e constraints de integridade para reputacao operacional.
- Arquivos principais: `Backend/src/ConsertaPraMim.Domain/Entities/Review.cs`, `Backend/src/ConsertaPraMim.Application/DTOs/ReviewDTOs.cs`, `Backend/src/ConsertaPraMim.Application/Services/ReviewService.cs`, `Backend/src/ConsertaPraMim.Application/Validators/ProposalReviewValidators.cs`, `Backend/src/ConsertaPraMim.Infrastructure/Data/ConsertaPraMimDbContext.cs`, `Backend/src/ConsertaPraMim.Infrastructure/Migrations/20260224220648_AddStructuredPostServiceReviewQuestionnaire.cs`, `Backend/src/ConsertaPraMim.API/Controllers/ReviewsController.cs`, `Backend/src/ConsertaPraMim.API/Swagger/ApiEndpointDocumentationCatalog.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/ReviewServiceTests.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`, `Documentacao/ADMIN_PORTAL/STORIES/IN_PROGRESS/ST-050-pos-servico-avaliacao-recompra.md`, `Documentacao/ADMIN_PORTAL/INDEX.md`
- Risco/Impacto: medio
- [2026-02-24] [ST-049] Governanca de opt-out/frequencia nas campanhas de reativacao
- Tipo: feat
- Resumo: campanhas de reativacao passaram a aplicar politicas de opt-out e limite de toques por janela (`frequencyWindowDays`, `defaultMaxTouchesPerWeek`), com endpoint admin para preferencia individual (`POST /api/admin/growth/provider-reactivation/preferences`) e feedback de supressao por politica no `Growth Funnel`.
- Arquivos principais: `Backend/src/ConsertaPraMim.Application/DTOs/AdminGrowthDTOs.cs`, `Backend/src/ConsertaPraMim.Application/Interfaces/IAdminGrowthService.cs`, `Backend/src/ConsertaPraMim.Application/Services/AdminGrowthService.cs`, `Backend/src/ConsertaPraMim.API/Controllers/AdminGrowthController.cs`, `Backend/src/ConsertaPraMim.API/Swagger/ApiEndpointDocumentationCatalog.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Controllers/AdminGrowthController.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Services/IAdminOperationsApiClient.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Services/AdminOperationsApiClient.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminGrowth/Index.cshtml`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/AdminGrowthServiceReactivationTests.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/AdminGrowthControllerReactivationTests.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`, `Documentacao/ADMIN_PORTAL/STORIES/DONE/ST-049-reativacao-automatica-prestadores.md`, `Documentacao/ADMIN_PORTAL/INDEX.md`
- Risco/Impacto: medio
- [2026-02-24] [ST-049] Painel de performance de reativacao com taxa por campanha
- Tipo: feat
- Resumo: adicionado endpoint `GET /api/admin/growth/provider-reactivation/campaigns/performance` com consolidado de campanhas, volume selecionado, entrega por canal e taxa de reativacao (login apos disparo), e o `Growth Funnel` ganhou secao de performance com cards executivos e tabela por campanha.
- Arquivos principais: `Backend/src/ConsertaPraMim.Application/DTOs/AdminGrowthDTOs.cs`, `Backend/src/ConsertaPraMim.Application/Interfaces/IAdminGrowthService.cs`, `Backend/src/ConsertaPraMim.Application/Services/AdminGrowthService.cs`, `Backend/src/ConsertaPraMim.API/Controllers/AdminGrowthController.cs`, `Backend/src/ConsertaPraMim.API/Swagger/ApiEndpointDocumentationCatalog.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Controllers/AdminGrowthController.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Models/AdminGrowthViewModel.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Services/IAdminOperationsApiClient.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Services/AdminOperationsApiClient.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminGrowth/Index.cshtml`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/AdminGrowthServiceReactivationTests.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/AdminGrowthControllerReactivationTests.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`, `Documentacao/ADMIN_PORTAL/STORIES/IN_PROGRESS/ST-049-reativacao-automatica-prestadores.md`
- Risco/Impacto: medio
- [2026-02-24] [ST-049] Campanha de reativacao com canais sistema/push/email e trilha de entrega
- Tipo: feat
- Resumo: evoluido o endpoint de rodada de reativacao para suportar disparo por canais configuraveis (`sistema`, `push`, `email`) com mensagem customizavel, consolidado de entrega por canal e erros por destinatario; `Growth Funnel` passou a exibir configuracao de canais e feedback detalhado da ultima campanha.
- Arquivos principais: `Backend/src/ConsertaPraMim.Application/DTOs/AdminGrowthDTOs.cs`, `Backend/src/ConsertaPraMim.Application/Services/AdminGrowthService.cs`, `Backend/src/ConsertaPraMim.API/Swagger/ApiEndpointDocumentationCatalog.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Controllers/AdminGrowthController.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminGrowth/Index.cshtml`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/AdminGrowthServiceReactivationTests.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`, `Documentacao/ADMIN_PORTAL/STORIES/IN_PROGRESS/ST-049-reativacao-automatica-prestadores.md`
- Risco/Impacto: medio
- [2026-02-24] [ST-049] Motor de campanha automatizada de reativacao com cadencia no Growth Funnel
- Tipo: feat
- Resumo: implementada rodada operacional de campanha no modulo `Growth Funnel` com endpoint `POST /api/admin/growth/provider-reactivation/campaigns/run`, controle de cadencia por janela minima, opcao de `force run`, selecao segmentada de destinatarios e registro auditavel da execucao com feedback imediato no portal admin.
- Arquivos principais: `Backend/src/ConsertaPraMim.Application/DTOs/AdminGrowthDTOs.cs`, `Backend/src/ConsertaPraMim.Application/Interfaces/IAdminGrowthService.cs`, `Backend/src/ConsertaPraMim.Application/Services/AdminGrowthService.cs`, `Backend/src/ConsertaPraMim.API/Controllers/AdminGrowthController.cs`, `Backend/src/ConsertaPraMim.API/Swagger/ApiEndpointDocumentationCatalog.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Controllers/AdminGrowthController.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Services/IAdminOperationsApiClient.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Services/AdminOperationsApiClient.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Models/AdminGrowthViewModel.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminGrowth/Index.cshtml`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/AdminGrowthServiceReactivationTests.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/AdminGrowthControllerReactivationTests.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`, `Documentacao/ADMIN_PORTAL/STORIES/IN_PROGRESS/ST-049-reativacao-automatica-prestadores.md`
- Risco/Impacto: medio
- [2026-02-24] [ST-049] Segmentacao de prestadores inativos por periodo/categoria/regiao no Growth Funnel
- Tipo: feat
- Resumo: implementado endpoint `GET /api/admin/growth/provider-reactivation/segments` com criterios operacionais de inatividade (atencao/frio/dormente/hibernado), consolidando ultima atividade por login/proposta, breakdown por categoria/regiao e preview de prestadores para acao de reativacao; portal admin ganhou secao dedicada no modulo `Growth Funnel`.
- Arquivos principais: `Backend/src/ConsertaPraMim.Application/DTOs/AdminGrowthDTOs.cs`, `Backend/src/ConsertaPraMim.Application/Interfaces/IAdminGrowthService.cs`, `Backend/src/ConsertaPraMim.Application/Services/AdminGrowthService.cs`, `Backend/src/ConsertaPraMim.API/Controllers/AdminGrowthController.cs`, `Backend/src/ConsertaPraMim.API/Swagger/ApiEndpointDocumentationCatalog.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Controllers/AdminGrowthController.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Models/AdminGrowthViewModel.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Services/IAdminOperationsApiClient.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Services/AdminOperationsApiClient.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminGrowth/Index.cshtml`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/AdminGrowthServiceReactivationTests.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/AdminGrowthControllerReactivationTests.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`, `Documentacao/ADMIN_PORTAL/STORIES/IN_PROGRESS/ST-049-reativacao-automatica-prestadores.md`, `Documentacao/ADMIN_PORTAL/INDEX.md`
- Risco/Impacto: medio
- [2026-02-24] [ST-048] KPI de receita recorrente PJ com endpoint e painel admin por janela
- Tipo: feat
- Resumo: concluida a instrumentacao do KPI de receita recorrente PJ com endpoint `GET /api/admin/pj-recurring-contracts/kpis/revenue`, consolidado de MRR/renovacoes previstas e serie diaria; portal admin `Planos e Ofertas` ganhou nova secao com filtros por periodo, cards executivos e tabela temporal para monitorar previsao de renovacao.
- Arquivos principais: `Backend/src/ConsertaPraMim.Application/DTOs/PjRecurringContractsDTOs.cs`, `Backend/src/ConsertaPraMim.Application/Interfaces/IPjRecurringContractService.cs`, `Backend/src/ConsertaPraMim.Application/Services/PjRecurringContractService.cs`, `Backend/src/ConsertaPraMim.API/Controllers/AdminPjRecurringContractsController.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Controllers/AdminPlanGovernanceController.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Services/IAdminOperationsApiClient.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Services/AdminOperationsApiClient.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Models/AdminOperationsViewModels.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminPlanGovernance/Index.cshtml`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/PjRecurringContractServiceTests.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/AdminPjRecurringContractsControllerTests.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`, `Documentacao/ADMIN_PORTAL/STORIES/DONE/ST-048-pacotes-pj-recorrentes.md`, `Documentacao/ADMIN_PORTAL/INDEX.md`
- Risco/Impacto: medio
- [2026-02-24] [ST-048] Visao admin da carteira PJ recorrente no modulo Planos e Ofertas
- Tipo: feat
- Resumo: criado endpoint `GET /api/admin/pj-recurring-contracts/portfolio` e secao `Carteira PJ recorrente` no portal admin com filtros por periodo/status, KPI de carteira (ativos, inadimplencia, MRR, ticket), breakdown por status/categoria e tabela top 200 com renovacao/SLA/elegibilidade.
- Arquivos principais: `Backend/src/ConsertaPraMim.API/Controllers/AdminPjRecurringContractsController.cs`, `Backend/src/ConsertaPraMim.Application/Interfaces/IPjRecurringContractService.cs`, `Backend/src/ConsertaPraMim.Application/Services/PjRecurringContractService.cs`, `Backend/src/ConsertaPraMim.Domain/Repositories/IPjRecurringContractRepository.cs`, `Backend/src/ConsertaPraMim.Infrastructure/Repositories/PjRecurringContractRepository.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Controllers/AdminPlanGovernanceController.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Services/IAdminOperationsApiClient.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Services/AdminOperationsApiClient.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminPlanGovernance/Index.cshtml`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/PjRecurringContractServiceTests.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/AdminPjRecurringContractsControllerTests.cs`, `Documentacao/ADMIN_PORTAL/STORIES/IN_PROGRESS/ST-048-pacotes-pj-recorrentes.md`
- Risco/Impacto: medio
- [2026-02-24] [ST-048] Elegibilidade de prestadores integrada no contrato PJ recorrente (PF/PJ/ambos)
- Tipo: feat
- Resumo: o fluxo PJ recorrente passou a calcular oferta elegivel por categoria e preferencia (`Both`/`PjOnly`), bloqueando contratacao sem prestadores aptos e retornando `eligibleProvidersCount` no payload para transparencia operacional; cobertura de testes ampliada para cenarios positivos e negativos.
- Arquivos principais: `Backend/src/ConsertaPraMim.Application/Services/PjRecurringContractService.cs`, `Backend/src/ConsertaPraMim.Application/DTOs/PjRecurringContractsDTOs.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/PjRecurringContractServiceTests.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`, `Documentacao/ADMIN_PORTAL/STORIES/IN_PROGRESS/ST-048-pacotes-pj-recorrentes.md`
- Risco/Impacto: medio
- [2026-02-24] [ST-048] Fluxo mobile de contratacao e renovacao de pacotes PJ recorrentes
- Tipo: feat
- Resumo: implementados servico, repositorio e endpoints mobile do cliente para listar, contratar e renovar contratos PJ recorrentes com validacao de perfil PJ, regras de SLA/janela e transicao automatica para `Completed` ao exceder vigencia; Swagger e cobertura de testes unitarios foram atualizados.
- Arquivos principais: `Backend/src/ConsertaPraMim.Application/Services/PjRecurringContractService.cs`, `Backend/src/ConsertaPraMim.Application/DTOs/PjRecurringContractsDTOs.cs`, `Backend/src/ConsertaPraMim.Domain/Repositories/IPjRecurringContractRepository.cs`, `Backend/src/ConsertaPraMim.Infrastructure/Repositories/PjRecurringContractRepository.cs`, `Backend/src/ConsertaPraMim.API/Controllers/MobileClientPjRecurringContractsController.cs`, `Backend/src/ConsertaPraMim.API/Swagger/ApiEndpointDocumentationCatalog.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/PjRecurringContractServiceTests.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/MobileClientPjRecurringContractsControllerTests.cs`, `Documentacao/ADMIN_PORTAL/STORIES/IN_PROGRESS/ST-048-pacotes-pj-recorrentes.md`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`
- Risco/Impacto: medio
- [2026-02-24] [ST-048] Modelagem de contratos PJ recorrentes com SLA e janela operacional
- Tipo: feat
- Resumo: criada a entidade `PjRecurringContract` com enums de cadencia/status, regras de integridade para SLA/janela/dias operacionais e relacionamento com cliente PJ; migration `AddPjRecurringContractsModel` adiciona a tabela e indices de renovacao/carteira para habilitar o fluxo recorrente nas proximas tasks.
- Arquivos principais: `Backend/src/ConsertaPraMim.Domain/Entities/PjRecurringContract.cs`, `Backend/src/ConsertaPraMim.Domain/Enums/Enums.cs`, `Backend/src/ConsertaPraMim.Infrastructure/Data/ConsertaPraMimDbContext.cs`, `Backend/src/ConsertaPraMim.Infrastructure/Migrations/20260224121459_AddPjRecurringContractsModel.cs`, `Documentacao/ADMIN_PORTAL/STORIES/IN_PROGRESS/ST-048-pacotes-pj-recorrentes.md`, `Documentacao/ADMIN_PORTAL/INDEX.md`
- Risco/Impacto: medio
- [2026-02-24] [ST-047] Estrategia de rollout por cohort para monetizacao hibrida
- Tipo: feat
- Resumo: implementado painel/endpoint de rollout por cohort (`GET /api/admin/plan-governance/hybrid-rollout`) com elegibilidade por trust/compliance/plano, cohorts priorizados, fases de execucao (D+0..D+90) e guardrails de governanca para escalar assinatura + creditos sem regressao operacional.
- Arquivos principais: `Backend/src/ConsertaPraMim.Application/DTOs/PlanGovernanceDTOs.cs`, `Backend/src/ConsertaPraMim.Application/Interfaces/IPlanGovernanceService.cs`, `Backend/src/ConsertaPraMim.Application/Services/PlanGovernanceService.cs`, `Backend/src/ConsertaPraMim.API/Controllers/AdminPlanGovernanceController.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Controllers/AdminPlanGovernanceController.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminPlanGovernance/Index.cshtml`, `Backend/src/ConsertaPraMim.Web.Admin/Services/IAdminOperationsApiClient.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Services/AdminOperationsApiClient.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/PlanGovernanceServiceTests.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/AdminPlanGovernanceControllerTests.cs`, `Documentacao/ADMIN_PORTAL/STORIES/DONE/ST-047-modelo-hibrido-monetizacao.md`, `Documentacao/ADMIN_PORTAL/INDEX.md`
- Risco/Impacto: medio
- [2026-02-24] [ST-047] Dashboard de receita por componente no modulo Planos e Ofertas
- Tipo: feat
- Resumo: criado painel de receita hibrida no admin com consolidado de assinatura fixa vs creditos variaveis, filtros por periodo, participacao percentual por componente, tabela de MRR por plano e serie diaria operacional; backend ganhou endpoint dedicado `GET /api/admin/plan-governance/revenue-components`.
- Arquivos principais: `Backend/src/ConsertaPraMim.Application/DTOs/PlanGovernanceDTOs.cs`, `Backend/src/ConsertaPraMim.Application/Interfaces/IPlanGovernanceService.cs`, `Backend/src/ConsertaPraMim.Application/Services/PlanGovernanceService.cs`, `Backend/src/ConsertaPraMim.API/Controllers/AdminPlanGovernanceController.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Services/IAdminOperationsApiClient.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Services/AdminOperationsApiClient.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Controllers/AdminPlanGovernanceController.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Models/AdminOperationsViewModels.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminPlanGovernance/Index.cshtml`, `Backend/tests/ConsertaPraMim.Tests.Unit/Services/PlanGovernanceServiceTests.cs`, `Documentacao/ADMIN_PORTAL/STORIES/DONE/ST-047-modelo-hibrido-monetizacao.md`
- Risco/Impacto: medio
- [2026-02-24] [ST-047] Ledger de creditos segregado por componente de receita (assinatura x creditos)
- Tipo: feat
- Resumo: extrato e mutacoes de creditos passaram a carregar `RevenueComponent` para separar receita fixa e variavel no ledger, com filtros no endpoint admin/prestador, persistencia no banco (migration `AddProviderCreditRevenueComponent`) e cobertura de testes de integracao ajustada para o novo contrato.
- Arquivos principais: `Backend/src/ConsertaPraMim.Domain/Entities/ProviderCreditLedgerEntry.cs`, `Backend/src/ConsertaPraMim.Domain/Enums/ProviderCreditEnums.cs`, `Backend/src/ConsertaPraMim.Application/DTOs/ProviderCreditsDTOs.cs`, `Backend/src/ConsertaPraMim.Application/Services/ProviderCreditService.cs`, `Backend/src/ConsertaPraMim.Infrastructure/Data/ConsertaPraMimDbContext.cs`, `Backend/src/ConsertaPraMim.Infrastructure/Migrations/20260224113304_AddProviderCreditRevenueComponent.cs`, `Backend/src/ConsertaPraMim.API/Controllers/AdminProviderCreditsController.cs`, `Backend/src/ConsertaPraMim.API/Controllers/ProviderCreditsController.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/Integration/Controllers/AdminProviderCreditsControllerSqliteIntegrationTests.cs`, `Backend/tests/ConsertaPraMim.Tests.Unit/Integration/Repositories/ProviderCreditRepositorySqliteIntegrationTests.cs`, `Documentacao/ADMIN_PORTAL/STORIES/DONE/ST-047-modelo-hibrido-monetizacao.md`
- Risco/Impacto: medio
- [2026-02-24] [ST-047] Simulador financeiro hibrido no admin (assinatura + creditos por resultado)
- Tipo: feat
- Resumo: evoluido o simulador de `Planos e Ofertas` para projetar receita variavel por eventos de resultado (propostas aceitas, agendamentos e conclusoes), consumo previsto de creditos e receita total combinada com assinatura.
- Arquivos principais: `Backend/src/ConsertaPraMim.Application/DTOs/PlanGovernanceDTOs.cs`, `Backend/src/ConsertaPraMim.Application/Services/PlanGovernanceService.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Models/AdminOperationsViewModels.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Controllers/AdminPlanGovernanceController.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminPlanGovernance/Index.cshtml`, `Backend/src/ConsertaPraMim.Web.Admin/wwwroot/js/views/admin-plan-governance/index.js`, `Documentacao/ADMIN_PORTAL/STORIES/DONE/ST-047-modelo-hibrido-monetizacao.md`
- Risco/Impacto: medio
- [2026-02-24] [ST-047] Modelagem comercial v1 para monetizacao hibrida (assinatura + creditos)
- Tipo: docs
- Resumo: ST-047 foi iniciada com modelo comercial v1 detalhando componentes de receita fixa/variavel, regras de combinacao, entradas/saidas do simulador financeiro e principios de migracao de plano sem perda de historico.
- Arquivos principais: `Documentacao/ADMIN_PORTAL/STORIES/DONE/ST-047-modelo-hibrido-monetizacao.md`, `Documentacao/ADMIN_PORTAL/INDEX.md`
- Risco/Impacto: baixo
- [2026-02-24] [ST-043] Telemetria de interacao do comparador e consolidado A/B de conversao
- Tipo: feat
- Resumo: criada persistencia de eventos do comparador (`ProposalComparisonInteraction`), endpoint mobile para tracking (`POST /api/mobile/client/orders/{orderId}/proposals/comparison/interactions`), registro automatico de `comparison_viewed` e `proposal_accepted_after_comparison`, alem de endpoint admin para analise A/B (`GET /api/admin/proposal-comparison/ab-summary`) com taxa de conversao por bucket.
- Arquivos principais: `Backend/src/ConsertaPraMim.Domain/Entities/ProposalComparisonInteraction.cs`, `Backend/src/ConsertaPraMim.Application/Services/MobileClientOrderService.cs`, `Backend/src/ConsertaPraMim.API/Controllers/MobileClientOrdersController.cs`, `Backend/src/ConsertaPraMim.API/Controllers/AdminProposalComparisonController.cs`, `Backend/src/ConsertaPraMim.Infrastructure/Migrations/20260224005615_AddProposalComparisonInteractionTelemetry.cs`, `conserta-pra-mim app/App.tsx`, `conserta-pra-mim app/services/mobileOrders.ts`
- Risco/Impacto: medio
- [2026-02-24] [ST-043] Comparador de propostas entregue no app e portal cliente
- Tipo: feat
- Resumo: implementado endpoint de comparacao (`/api/mobile/client/orders/{orderId}/proposals/comparison`) com score consolidado e ordenacao por criterio, integrado ao app cliente (bloco comparador com ordenacao/abertura de proposta) e ao portal cliente (tabela lado a lado com ranking dinamico por score/preco/prazo/avaliacao/garantia).
- Arquivos principais: `Backend/src/ConsertaPraMim.API/Controllers/MobileClientOrdersController.cs`, `Backend/src/ConsertaPraMim.Application/Services/MobileClientOrderService.cs`, `conserta-pra-mim app/components/RequestDetails.tsx`, `conserta-pra-mim app/App.tsx`, `Backend/src/ConsertaPraMim.Web.Client/Views/ServiceRequests/Details.cshtml`
- Risco/Impacto: medio
- [2026-02-24] [ST-043] Payload de propostas evoluido com prazo e garantia
- Tipo: feat
- Resumo: adicionados os campos `estimatedLeadTimeHours` e `warrantyDays` no fluxo de propostas (backend + web/app prestador + app cliente), com validacao de faixas, constraints de banco e migracao EF (`AddProposalLeadTimeAndWarranty`) para habilitar comparacao objetiva por prazo/garantia.
- Arquivos principais: `Backend/src/ConsertaPraMim.Domain/Entities/Proposal.cs`, `Backend/src/ConsertaPraMim.Application/DTOs/ProposalDTOs.cs`, `Backend/src/ConsertaPraMim.Infrastructure/Migrations/20260224002825_AddProposalLeadTimeAndWarranty.cs`, `Backend/src/ConsertaPraMim.Web.Provider/Views/ServiceRequests/Details.cshtml`, `conserta-pra-mim-provider app/components/RequestDetails.tsx`, `conserta-pra-mim app/services/mobileOrders.ts`
- Risco/Impacto: medio
- [2026-02-24] [ST-043] Modelo comparativo de propostas definido para cliente (app/portal)
- Tipo: feat
- Resumo: padronizada a estrutura de comparacao de propostas com cinco estrategias de ordenacao (`best_score`, `lowest_price`, `fastest_lead_time`, `best_rating`, `highest_warranty`) e novos DTOs para suportar score, prazo, garantia e historico do prestador.
- Arquivos principais: `Backend/src/ConsertaPraMim.Application/DTOs/MobileClientOrderDTOs.cs`, `Documentacao/ADMIN_PORTAL/STORIES/IN_PROGRESS/ST-043-comparador-propostas-cliente.md`, `Documentacao/ADMIN_PORTAL/INDEX.md`
- Risco/Impacto: baixo
- [2026-02-24] [ST-038] Correcao de renderizacao Mermaid no portal admin
- Tipo: fix
- Resumo: corrigida a injecao do codigo Mermaid na view `AdminDiagrams` para evitar entity encoding (`&#xA;`) que quebrava o parser com erro `AMP`; leitura dos arquivos `.mmd` passou a detectar BOM e remover `\uFEFF` antes da renderizacao.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminDiagrams/Index.cshtml`, `Backend/src/ConsertaPraMim.Web.Admin/Services/AdminDiagramsService.cs`
- Risco/Impacto: baixo
- [2026-02-23] [ST-042] Portal admin com visualizacao de liquidez e playbook operacional
- Tipo: feat
- Resumo: criada area `Score Liquidez` no menu admin com filtros operacionais, lista priorizada de deficit por regiao/categoria, historico diario, alertas e consolidacao do playbook de acao por faixa.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Admin/Controllers/AdminLiquidityScoreController.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminLiquidityScore/Index.cshtml`, `Backend/src/ConsertaPraMim.Web.Admin/Models/AdminLiquidityScoreViewModel.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Services/IAdminOperationsApiClient.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Services/AdminOperationsApiClient.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Views/Shared/_Layout.cshtml`, `Documentacao/ADMIN_PORTAL/RUNBOOKS/PLAYBOOK_LIQUIDEZ_ST-042.md`, `Documentacao/ADMIN_PORTAL/STORIES/DONE/ST-042-score-liquidez-regiao-categoria.md`, `Documentacao/ADMIN_PORTAL/INDEX.md`
- Risco/Impacto: medio
- [2026-02-23] [ST-042] API de score de liquidez por regiao/categoria com historico e alertas
- Tipo: feat
- Resumo: implementado `GET /api/admin/growth/liquidity-score` com formula ponderada de liquidez (cobertura de propostas, profundidade de oferta e velocidade da primeira proposta), ranking por regiao/categoria, serie diaria e alertas de deficit.
- Arquivos principais: `Backend/src/ConsertaPraMim.API/Controllers/AdminGrowthController.cs`, `Backend/src/ConsertaPraMim.Application/Services/AdminLiquidityScoreService.cs`, `Backend/src/ConsertaPraMim.Application/Interfaces/IAdminLiquidityScoreService.cs`, `Backend/src/ConsertaPraMim.Application/DTOs/AdminGrowthDTOs.cs`, `Backend/src/ConsertaPraMim.Application/DependencyInjection.cs`, `Backend/src/ConsertaPraMim.API/Swagger/ApiEndpointDocumentationCatalog.cs`, `Documentacao/ADMIN_PORTAL/STORIES/IN_PROGRESS/ST-042-score-liquidez-regiao-categoria.md`, `Documentacao/ADMIN_PORTAL/INDEX.md`
- Risco/Impacto: medio
- [2026-02-23] [ST-041] Visualizacao do funil de growth no portal admin
- Tipo: feat
- Resumo: criada a area `Growth Funnel` no menu do portal admin com filtros (periodo/categoria/cidade/SLA), cards de conversao, etapas com barras de SLA e lista de alertas operacionais.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Admin/Controllers/AdminGrowthController.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminGrowth/Index.cshtml`, `Backend/src/ConsertaPraMim.Web.Admin/Models/AdminGrowthViewModel.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Services/IAdminOperationsApiClient.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Services/AdminOperationsApiClient.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Views/Shared/_Layout.cshtml`, `Documentacao/ADMIN_PORTAL/STORIES/DONE/ST-041-funil-e2e-sla-operacional.md`, `Documentacao/ADMIN_PORTAL/INDEX.md`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`
- Risco/Impacto: medio
- [2026-02-23] [ST-041] Endpoint admin de funil de growth com SLA por etapa e alertas operacionais
- Tipo: feat
- Resumo: implementado `GET /api/admin/growth/funnel` com recorte temporal/categoria/cidade, calculo de etapas (`pedido -> primeira proposta`, `primeira proposta -> aceite`), SLA configuravel, P50/media de duracao e alertas acionaveis para gargalos de liquidez/conversao.
- Arquivos principais: `Backend/src/ConsertaPraMim.API/Controllers/AdminGrowthController.cs`, `Backend/src/ConsertaPraMim.Application/Services/AdminGrowthService.cs`, `Backend/src/ConsertaPraMim.Application/DTOs/AdminGrowthDTOs.cs`, `Backend/src/ConsertaPraMim.Application/Interfaces/IAdminGrowthService.cs`, `Backend/src/ConsertaPraMim.Application/DependencyInjection.cs`, `Backend/src/ConsertaPraMim.API/Swagger/ApiEndpointDocumentationCatalog.cs`, `Documentacao/ADMIN_PORTAL/STORIES/IN_PROGRESS/ST-041-funil-e2e-sla-operacional.md`, `Documentacao/ADMIN_PORTAL/INDEX.md`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`
- Risco/Impacto: medio
- [2026-02-23] [ST-040] Roadmap de produto no portal admin com board de backlog por status
- Tipo: feat
- Resumo: novo modulo `Roadmap` no menu do portal admin, com leitura automatica de epics/stories markdown, filtros (`q`, `epic`, `trilha`, `status`), cards de progresso por epic e colunas de stories (`Backlog`, `In Progress`, `Done`) com link direto para `Wiki Docs`.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Admin/Controllers/AdminRoadmapController.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Services/AdminRoadmapService.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminRoadmap/Index.cshtml`, `Backend/src/ConsertaPraMim.Web.Admin/Views/Shared/_Layout.cshtml`, `Backend/src/ConsertaPraMim.Web.Admin/Program.cs`
- Risco/Impacto: medio
- [2026-02-23] [ST-040] Backlog estrategico de crescimento estruturado em epics e stories incrementais
- Tipo: docs
- Resumo: criados os epics `EPIC-018/019/020` e stories `ST-041..ST-051` para evolucao de liquidez, conversao, monetizacao, retencao e governanca de growth; board `INDEX.md` atualizado para refletir a trilha e roadmap.
- Arquivos principais: `Documentacao/ADMIN_PORTAL/EPICS/EPIC-018-liquidez-conversao-marketplace.md`, `Documentacao/ADMIN_PORTAL/EPICS/EPIC-019-monetizacao-retencao-prestadores.md`, `Documentacao/ADMIN_PORTAL/EPICS/EPIC-020-roadmap-governanca-growth.md`, `Documentacao/ADMIN_PORTAL/STORIES/BACKLOG/ST-041-funil-e2e-sla-operacional.md`, `Documentacao/ADMIN_PORTAL/STORIES/BACKLOG/ST-051-cockpit-growth-northstar.md`, `Documentacao/ADMIN_PORTAL/INDEX.md`
- Risco/Impacto: baixo
- [2026-02-23] [GOV-002] Diretriz obrigatoria para manutencao continua da documentacao Swagger/OpenAPI
- Tipo: docs
- Resumo: atualizado `AGENTS.md` com regras de governanca para manter o Swagger sempre sincronizado com endpoints novos/alterados, incluindo padrao de contexto de negocio, paridade entre catalogo/filtros e obrigacao de atualizar manual/changelog junto com mudancas de contrato.
- Arquivos principais: `AGENTS.md`
- Risco/Impacto: baixo
- [2026-02-23] [ST-039] Refino contextual da documentacao Swagger por rota/acao (sem texto generico)
- Tipo: feat
- Resumo: o motor de documentacao Swagger passou a gerar narrativas de negocio especificas por rota/acao (login, cadastro, abertura de pedido, proposta, aceite, agenda, disputa, suporte, webmail, push devices, monitoring, load tests e financeiro), com fallback contextual somente para casos nao mapeados.
- Arquivos principais: `Backend/src/ConsertaPraMim.API/Swagger/ApiEndpointDocumentationCatalog.cs`, `Backend/src/ConsertaPraMim.API/Swagger/ComprehensiveSwaggerOperationFilter.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`, `Documentacao/ADMIN_PORTAL/STORIES/IN_PROGRESS/ST-039-documentacao-extrema-endpoints-swagger-api.md`
- Risco/Impacto: medio
- [2026-02-23] [ST-039] Manual QA/Operacao atualizado para validacao da documentacao extrema da API
- Tipo: docs
- Resumo: manual do portal admin atualizado com cobertura operacional do Swagger enriquecido, incluindo caso `QA-ADM-026`, ajuste de checklist, troubleshooting especifico e entrada de revisao para ST-039.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`, `Documentacao/ADMIN_PORTAL/STORIES/IN_PROGRESS/ST-039-documentacao-extrema-endpoints-swagger-api.md`
- Risco/Impacto: baixo
- [2026-02-23] [ST-039] Catalogo de dominios e exemplos cURL por endpoint no Swagger
- Tipo: feat
- Resumo: adicionada camada de contexto por dominio (`ApiEndpointDocumentationCatalog`) com narrativa de negocio/tecnica por tag/controlador, descricao automatica de tags no Swagger e secao de exemplo cURL por endpoint (incluindo auth, path/query e body JSON de referencia gerado por schema).
- Arquivos principais: `Backend/src/ConsertaPraMim.API/Swagger/ApiEndpointDocumentationCatalog.cs`, `Backend/src/ConsertaPraMim.API/Swagger/ApiTagDescriptionsDocumentFilter.cs`, `Backend/src/ConsertaPraMim.API/Swagger/ComprehensiveSwaggerOperationFilter.cs`, `Backend/src/ConsertaPraMim.API/Program.cs`
- Risco/Impacto: medio
- [2026-02-23] [ST-039] Motor global de documentacao Swagger para cobertura de todas as operacoes
- Tipo: feat
- Resumo: adicionada camada automatica de documentacao (`ComprehensiveSwaggerOperationFilter`) que preenche `summary` e `description` com secoes padronizadas de negocio/tecnicas para todas as operacoes da API, alem da inclusao automatica de XML comments de assemblies `ConsertaPraMim*`.
- Arquivos principais: `Backend/src/ConsertaPraMim.API/Swagger/ComprehensiveSwaggerOperationFilter.cs`, `Backend/src/ConsertaPraMim.API/Program.cs`, `Documentacao/ADMIN_PORTAL/STORIES/IN_PROGRESS/ST-039-documentacao-extrema-endpoints-swagger-api.md`
- Risco/Impacto: medio
- [2026-02-23] [ST-039] Planejamento de documentacao extrema dos endpoints da API no Swagger
- Tipo: docs
- Resumo: criada a trilha de entrega (`EPIC-017` + `ST-039`) para evolucao completa da documentacao de endpoints da API no Swagger, com escopo, criterios de aceite, tasks e governanca de execucao por etapas curtas.
- Arquivos principais: `Documentacao/ADMIN_PORTAL/EPICS/EPIC-017-documentacao-extrema-endpoints-api-swagger.md`, `Documentacao/ADMIN_PORTAL/STORIES/IN_PROGRESS/ST-039-documentacao-extrema-endpoints-swagger-api.md`, `Documentacao/ADMIN_PORTAL/INDEX.md`
- Risco/Impacto: baixo
- [2026-02-23] [ST-038] Atalho de Swagger API no menu lateral do portal admin
- Tipo: feat
- Resumo: adicionado item `Swagger API` no menu lateral do portal admin, abrindo a documentacao OpenAPI da API em nova aba usando a `ApiBaseUrl` resolvida no browser; manual QA/Operacao atualizado com cobertura funcional (`QA-ADM-025`) e revisao de checklist.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Admin/Views/Shared/_Layout.cshtml`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`
- Risco/Impacto: baixo
- [2026-02-23] [ST-038] Visualizador de Diagramas Mermaid no portal admin com layout estilo Wiki
- Tipo: feat
- Resumo: criada nova area `Diagramas Mermaid` no menu do portal admin, com leitura de arquivos `.mmd` em `Documentacao/DIAGRAMAS`, sidebar por secao/arquivo, busca textual, preview renderizado via Mermaid e fallback para codigo-fonte; manual QA/Operacao atualizado com cobertura funcional, caso de teste, checklist e troubleshooting do novo modulo.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Admin/Controllers/AdminDiagramsController.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Services/AdminDiagramsService.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminDiagrams/Index.cshtml`, `Backend/src/ConsertaPraMim.Web.Admin/Views/Shared/_Layout.cshtml`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`
- Risco/Impacto: baixo
- [2026-02-23] [ST-037] Nova area Change Logs no portal admin com leitura formatada e filtros
- Tipo: feat
- Resumo: adicionada pagina dedicada `Change Logs` no menu lateral do portal admin para leitura formatada do `CHANGELOG.md`, com filtros por palavra-chave e periodo (`de`/`ate`), contadores de resultado e cards estruturados por entrada.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Admin/Controllers/AdminChangeLogsController.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Services/AdminChangeLogsService.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminChangeLogs/Index.cshtml`, `Backend/src/ConsertaPraMim.Web.Admin/Views/Shared/_Layout.cshtml`
- Risco/Impacto: baixo
- [2026-02-23] [ST-036] Ajuste de fuso horario na view Aplicativos do portal admin
- Tipo: fix
- Resumo: substituida conversao `ToLocalTime()` por conversao explicita para fuso de exibicao (`America/Sao_Paulo`, com fallback Windows/UTC), corrigindo horario de "Ultima publicacao" com +3h.
- Arquivos principais: `Backend/src/ConsertaPraMim.Web.Admin/Controllers/AdminApplicationsController.cs`, `Backend/src/ConsertaPraMim.Web.Admin/Views/AdminApplications/Index.cshtml`, `Backend/src/ConsertaPraMim.Web.Admin/Models/AdminApplicationsViewModels.cs`
- Risco/Impacto: baixo
- [2026-02-23] [ST-036] Deploy Plan ampliado com todos os nos do workflow e status previsto de execucao
- Tipo: feat
- Resumo: o resumo de `Resolve Deploy Targets` passou a listar todos os nos do grafo (deploys, healthchecks, builds/uploads de APK, `Skip Deploy` e `Deploy Summary`) com marcacao visual de execucao prevista.
- Arquivos principais: `.github/workflows/deploy-vps.yml`
- Risco/Impacto: baixo
- [2026-02-23] [OPS-APK] Marcador operacional para forcar rebuild/publicacao dos APKs no deploy VPS
- Tipo: docs
- Resumo: adicionada nota operacional no script `build_apks.py` para disparar `apk_shared` e forcar nova execucao de build/upload dos tres APKs via pipeline.
- Arquivos principais: `scripts/build_apks.py`
- Risco/Impacto: baixo
- [2026-02-23] [ST-036] Grafo de deploy: uploads APK encadeados em nos explicitos para melhor visualizacao
- Tipo: refactor
- Resumo: adicionadas dependencias diretas entre jobs de upload de APK (`client -> provider -> admin`) para o grafo do GitHub Actions exibir os uploads em cadeia com leitura operacional mais clara.
- Arquivos principais: `.github/workflows/deploy-vps.yml`
- Risco/Impacto: baixo
- [2026-02-23] [ST-036] Corrigido encadeamento de build/upload dos APKs no deploy VPS para evitar skip indevido
- Tipo: fix
- Resumo: ajustadas dependencias e condicoes dos jobs `build/upload-apk-mobile-*` para que cada app atualize seus APKs sempre que houver mudanca naquele app, sem depender de execucao de build/upload dos outros apps.
- Arquivos principais: `.github/workflows/deploy-vps.yml`
- Risco/Impacto: medio
- [2026-02-23] [ST-036] Atualizacao de icone Android do app cliente com novo asset oficial
- Tipo: feat
- Resumo: atualizado o conjunto completo de icones Android (`ic_launcher`, `ic_launcher_round`, `ic_launcher_foreground`) do app cliente a partir do arquivo `icon_cpm_cliente.png`, cobrindo todas as densidades (`mdpi` a `xxxhdpi`).
- Arquivos principais: `conserta-pra-mim app/android/app/src/main/res/mipmap-*/ic_launcher*.png`
- Risco/Impacto: baixo
- [2026-02-23] [ST-036] Botao fisico Android agora respeita historico de telas (cliente/prestador)
- Tipo: fix
- Resumo: substituida navegacao hardcoded do botao `back` por historico interno de views nos apps cliente e prestador, garantindo retorno para a tela anterior; em telas-raiz (`Dashboard/Auth/Onboarding`) o comportamento permanece de saida do app.
- Arquivos principais: `conserta-pra-mim app/App.tsx`, `conserta-pra-mim-provider app/App.tsx`
- Risco/Impacto: medio
- [2026-02-23] [ST-036] Pedidos mobile com destaque de propostas e robustez na resolucao de CEP
- Tipo: fix
- Resumo: tela `Meus Pedidos` do app cliente passou a exibir contagem de propostas no mesmo padrao visual da Home; backend de geocodificacao ganhou fallback via BrasilAPI + AwesomeAPI para resolver CEP com coordenadas, reduzindo falhas de "Nao foi possivel localizar esse CEP" nos fluxos de `Novo pedido` e `Perfil`.
- Arquivos principais: `conserta-pra-mim app/components/OrdersList.tsx`, `Backend/src/ConsertaPraMim.Infrastructure/Services/ZipGeocodingService.cs`, `conserta-pra-mim app/components/Profile.tsx`
- Risco/Impacto: medio
- [2026-02-23] [ST-036] Ajuste de fuso horario na exibicao de aceite dos termos (mobile cliente/prestador)
- Tipo: fix
- Resumo: corrigida a formatacao das datas de termos no app cliente e prestador para tratar timestamps sem sufixo de fuso como UTC, evitando exibicao com +3h no campo "Status do aceite".
- Arquivos principais: `conserta-pra-mim app/components/Profile.tsx`, `conserta-pra-mim-provider app/components/Profile.tsx`
- Risco/Impacto: baixo
- [2026-02-23] [ST-036] Perfil mobile com localizacao persistida e termo de aceite com visualizacao/PDF
- Tipo: feat
- Resumo: perfil do cliente passou a salvar localizacao base (CEP/endereco/lat-lng) com suporte a CEP e geolocalizacao atual; perfil de cliente e prestador agora exibe termo ativo, permite visualizacao em modal, download em PDF e registro de aceite no proprio perfil, com novos endpoints autenticados em `/api/profile/legal-terms`.
- Arquivos principais: `ConsertaPraMim.API/Controllers/ProfileController.cs`, `ConsertaPraMim.Application/Services/ProfileService.cs`, `ConsertaPraMim.Application/DTOs/ProfileDTOs.cs`, `ConsertaPraMim.Domain/Repositories/ILegalTermsRepository.cs`, `ConsertaPraMim.Infrastructure/Repositories/LegalTermsRepository.cs`, `conserta-pra-mim app/components/Profile.tsx`, `conserta-pra-mim app/services/profile.ts`, `conserta-pra-mim-provider app/components/Profile.tsx`, `conserta-pra-mim-provider app/services/mobileProvider.ts`
- Risco/Impacto: medio
- [2026-02-22] [ST-035] Termos legais versionados com aceite obrigatorio no cadastro (web/mobile) e gestao no portal admin
- Tipo: feat
- Resumo: implementado fluxo completo de termos legais por publico (`client`/`provider`) com versionamento em banco, APIs publica/admin, validacao de aceite no backend durante cadastro, consumo nos cadastros web/mobile e nova area `Termos Legais` no portal admin para editar/publicar versoes com historico.
- Arquivos principais: `ConsertaPraMim.API/Controllers/LegalTermsController.cs`, `ConsertaPraMim.API/Controllers/AdminLegalTermsController.cs`, `ConsertaPraMim.Application/Services/LegalTermsService.cs`, `ConsertaPraMim.Application/Services/AuthService.cs`, `ConsertaPraMim.Web.Admin/Controllers/AdminLegalTermsController.cs`, `ConsertaPraMim.Web.Admin/Views/AdminLegalTerms/Index.cshtml`, `conserta-pra-mim app/components/Auth.tsx`, `conserta-pra-mim-provider app/components/Auth.tsx`, `Documentacao/ADMIN_PORTAL/RUNBOOKS/RUNBOOK_TERMOS_LEGAIS_ST-035.md`
- Risco/Impacto: alto
- [2026-02-22] [ST-034] Webmail admin E2E com Gmail SMTP/POP3 no portal e app mobile
- Tipo: feat
- Resumo: entregue fluxo de webmail administrativo ponta a ponta com backend mailbox (`/api/admin/mailbox/*`), worker de sincronizacao POP3, envio SMTP, notificacao de inbound para admins, nova area `Webmail` no portal admin (inbox/compose/settings), nova aba `Webmail` no app admin mobile (inbox/detalhe/compose/sync) e runbook operacional de setup Gmail/troubleshooting.
- Arquivos principais: `ConsertaPraMim.API/Controllers/AdminMailboxController.cs`, `ConsertaPraMim.Application/Services/AdminMailboxService.cs`, `ConsertaPraMim.API/BackgroundJobs/AdminMailboxSyncWorker.cs`, `ConsertaPraMim.Web.Admin/Controllers/AdminMailboxController.cs`, `ConsertaPraMim.Web.Admin/Views/AdminMailbox/Index.cshtml`, `conserta-pra-mim-admin app/components/AdminMailbox.tsx`, `Documentacao/ADMIN_PORTAL/RUNBOOKS/RUNBOOK_WEBMAIL_ST-034.md`
- Risco/Impacto: medio
- [2026-02-20] [ST-026] Auditoria final, regressao E2E e runbook de rollout do modulo de suporte
- Tipo: test
- Resumo: concluida trilha de governanca do modulo de suporte com reabertura auditavel (`support_ticket_reopened`), suite E2E in-memory do fluxo prestador <-> admin, reforco de regressao para isolamento/permissao e publicacao de runbook + checklist operacional de monitoramento pos-deploy.
- Arquivos principais: `ConsertaPraMim.Application/Services/AdminSupportTicketService.cs`, `tests/ConsertaPraMim.Tests.Unit/Integration/E2E/SupportTicketsProviderAdminE2EInMemoryIntegrationTests.cs`, `tests/ConsertaPraMim.Tests.Unit/Integration/Services/AdminSupportTicketServiceInMemoryIntegrationTests.cs`, `Documentacao/ADMIN_PORTAL/RUNBOOKS/DEPLOY_ROLLBACK_ST-026_SUPORTE.md`, `Documentacao/ADMIN_PORTAL/RUNBOOKS/CHECKLIST_MONITORAMENTO_ST-026_SUPORTE.md`
- Risco/Impacto: medio
- [2026-02-20] [ST-025] Realtime de suporte com fallback de polling e SLA basico na fila admin
- Tipo: feat
- Resumo: implementadas notificacoes realtime de chamados (ticket criado, resposta admin e mudanca de status), consumo de eventos nos portais admin/prestador, fallback de polling nos detalhes para ambientes sem websocket, indicador de tempo sem resposta na fila admin e resiliencia para falha de notificacao sem interromper o fluxo principal.
- Arquivos principais: `ConsertaPraMim.Application/Services/MobileProviderService.cs`, `ConsertaPraMim.Application/Services/AdminSupportTicketService.cs`, `ConsertaPraMim.Web.Admin/wwwroot/js/layout/admin-layout.js`, `ConsertaPraMim.Web.Admin/Controllers/AdminSupportTicketsController.cs`, `ConsertaPraMim.Web.Provider/Controllers/SupportTicketsController.cs`, `tests/ConsertaPraMim.Tests.Unit/Integration/Services/MobileProviderSupportTicketServiceInMemoryIntegrationTests.cs`
- Risco/Impacto: medio
- [2026-02-18] [ST-019] Monitoramento E2E da API com dashboard operacional no portal admin
- Tipo: feat
- Resumo: implementado monitoramento completo de requests da API com middleware global (correlationId, severidade, warnings, sanitizacao), buffer assincrono + workers de flush/agregacao/retencao, endpoints admin dedicados (`/api/admin/monitoring/*`), dashboard de monitoramento no Web.Admin, seeds para validacao local, testes unitarios/integracao e diagramas Mermaid (fluxo e sequencia).
- Arquivos principais: `ConsertaPraMim.API/Middleware/RequestTelemetryMiddleware.cs`, `ConsertaPraMim.API/Controllers/AdminMonitoringController.cs`, `ConsertaPraMim.Infrastructure/Services/AdminMonitoringService.cs`, `ConsertaPraMim.Web.Admin/Views/AdminMonitoring/Index.cshtml`, `ConsertaPraMim.Infrastructure/Migrations/20260218192717_AddApiMonitoringTelemetry.cs`, `tests/ConsertaPraMim.Tests.Unit/Middleware/RequestTelemetryMiddlewareTests.cs`, `tests/ConsertaPraMim.Tests.Unit/Integration/Controllers/AdminMonitoringControllerSqliteIntegrationTests.cs`
- Risco/Impacto: medio
- [2026-02-16] [ST-017] Regressao E2E de creditos: concessao admin ate abatimento da mensalidade
- Tipo: test
- Resumo: adicionado teste de integracao SQLite cobrindo o fluxo completo de negocio `admin concede credito -> prestador recebe notificacao -> simulacao de mensalidade consome credito -> saldo/extrato refletem debit`, incluindo ajuste de compatibilidade dos testes com a assinatura atual do `AdminProviderCreditService`.
- Arquivos principais: `tests/ConsertaPraMim.Tests.Unit/Integration/Controllers/AdminProviderCreditsControllerSqliteIntegrationTests.cs`, `tests/ConsertaPraMim.Tests.Unit/Services/AdminProviderCreditServiceTests.cs`, `Documentacao/ADMIN_PORTAL/STORIES/DONE/ST-017-aplicacao-creditos-mensalidade-visibilidade.md`, `Documentacao/ADMIN_PORTAL/INDEX.md`
- Risco/Impacto: baixo
- [2026-02-16] [ST-017] Manual admin atualizado para relatorio consolidado de creditos
- Tipo: docs
- Resumo: manual HTML do portal admin passou a cobrir o relatorio consolidado de uso de creditos (filtros, paginacao, totais), com novo caso QA-ADM-023, ajustes de checklist e troubleshooting especifico.
- Arquivos principais: `ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`, `Documentacao/ADMIN_PORTAL/STORIES/IN_PROGRESS/ST-017-aplicacao-creditos-mensalidade-visibilidade.md`
- Risco/Impacto: baixo
- [2026-02-16] [ST-017] Relatorio administrativo de uso de creditos com filtros e paginacao
- Tipo: feat
- Resumo: adicionados endpoint/API client e tela administrativa consolidada para uso de creditos por prestador, com filtros de periodo/tipo/status/busca textual, cards de totais, tabela paginada e coexistencia com o extrato individual por email.
- Arquivos principais: `ConsertaPraMim.API/Controllers/AdminProviderCreditsController.cs`, `ConsertaPraMim.Application/Services/AdminProviderCreditService.cs`, `ConsertaPraMim.Web.Admin/Controllers/AdminProviderCreditsController.cs`, `ConsertaPraMim.Web.Admin/Views/AdminProviderCredits/Index.cshtml`, `Documentacao/ADMIN_PORTAL/STORIES/IN_PROGRESS/ST-017-aplicacao-creditos-mensalidade-visibilidade.md`
- Risco/Impacto: medio
- [2026-02-16] [ST-017] KPIs de creditos no dashboard administrativo
- Tipo: feat
- Resumo: dashboard admin passou a exibir KPIs financeiros de creditos (concedidos no periodo, consumidos no periodo, saldo total em aberto e creditos com vencimento nos proximos 30 dias), com calculo consolidado no backend e atualizacao via snapshot/polling.
- Arquivos principais: `ConsertaPraMim.Application/DTOs/AdminDashboardDTOs.cs`, `ConsertaPraMim.Application/Services/AdminDashboardService.cs`, `ConsertaPraMim.Web.Admin/Views/AdminHome/Index.cshtml`, `Documentacao/ADMIN_PORTAL/STORIES/IN_PROGRESS/ST-017-aplicacao-creditos-mensalidade-visibilidade.md`
- Risco/Impacto: medio
- [2026-02-16] [ST-017] UI do prestador para carteira de creditos e extrato operacional
- Tipo: feat
- Resumo: criado modulo `Creditos` no portal do prestador com saldo atual, previsao de abatimento da proxima mensalidade, simulacao de impacto no valor final e extrato paginado/filtravel de movimentacoes (`Concessao`, `Consumo`, `Expiracao`, `Estorno`).
- Arquivos principais: `ConsertaPraMim.Web.Provider/Controllers/ProviderCreditsController.cs`, `ConsertaPraMim.Web.Provider/Models/ProviderCreditsViewModel.cs`, `ConsertaPraMim.Web.Provider/Views/ProviderCredits/Index.cshtml`, `ConsertaPraMim.Web.Provider/Views/Shared/_Layout.cshtml`, `Documentacao/ADMIN_PORTAL/STORIES/IN_PROGRESS/ST-017-aplicacao-creditos-mensalidade-visibilidade.md`
- Risco/Impacto: medio
- [2026-02-16] [ST-017] Expiracao automatica de creditos vencidos na simulacao
- Tipo: feat
- Resumo: antes de calcular/aplicar creditos na simulacao de mensalidade, o motor agora reconcilia o ledger e gera lancamento automatico `Expire` para saldo vencido, evitando abatimento indevido com creditos fora da vigencia.
- Arquivos principais: `ConsertaPraMim.Application/Services/PlanGovernanceService.cs`, `ConsertaPraMim.Domain/Repositories/IProviderCreditRepository.cs`, `ConsertaPraMim.Infrastructure/Repositories/ProviderCreditRepository.cs`, `tests/ConsertaPraMim.Tests.Unit/Services/PlanGovernanceServiceTests.cs`
- Risco/Impacto: medio
- [2026-02-16] [ST-017] Consumo opcional de creditos no simulador de mensalidade
- Tipo: feat
- Resumo: adicionada flag `consumeCredits` na simulacao de mensalidade para efetivar debito no ledger de creditos do prestador, com validacao de `ProviderUserId`, consumo atomico (respeitando saldo corrente) e retorno de telemetria de consumo (`creditsConsumed`, `creditsConsumptionEntryId`).
- Arquivos principais: `ConsertaPraMim.Application/Services/PlanGovernanceService.cs`, `ConsertaPraMim.Application/DTOs/PlanGovernanceDTOs.cs`, `ConsertaPraMim.Web.Admin/Controllers/AdminPlanGovernanceController.cs`, `ConsertaPraMim.Web.Admin/Models/AdminOperationsViewModels.cs`, `ConsertaPraMim.Web.Admin/Views/AdminPlanGovernance/Index.cshtml`, `tests/ConsertaPraMim.Tests.Unit/Services/PlanGovernanceServiceTests.cs`
- Risco/Impacto: medio
- [2026-02-16] [ST-017] Simulacao de mensalidade com aplicacao de creditos do prestador
- Tipo: feat
- Resumo: motor de simulacao de governanca comercial passou a aplicar saldo de creditos (quando `ProviderUserId` informado), mantendo ordem de calculo base -> promocao -> cupom -> credito, com novos campos de transparencia (`priceBeforeCredits`, `availableCredits`, `creditsApplied`, `creditsRemaining`) e cobertura de testes unitarios.
- Arquivos principais: `ConsertaPraMim.Application/Services/PlanGovernanceService.cs`, `ConsertaPraMim.Application/DTOs/PlanGovernanceDTOs.cs`, `ConsertaPraMim.Web.Admin/Views/AdminPlanGovernance/Index.cshtml`, `tests/ConsertaPraMim.Tests.Unit/Services/PlanGovernanceServiceTests.cs`, `Documentacao/ADMIN_PORTAL/STORIES/IN_PROGRESS/ST-017-aplicacao-creditos-mensalidade-visibilidade.md`
- Risco/Impacto: medio
- [2026-02-16] [ST-016] Backend inicial para concessao/estorno administrativo de creditos
- Tipo: feat
- Resumo: adicionados endpoints admin para concessao (`grants`) e estorno (`reversals`) com validacoes de negocio, template de notificacao por tipo de concessao, trilha de auditoria `before/after` e testes unitarios iniciais do fluxo.
- Arquivos principais: `ConsertaPraMim.API/Controllers/AdminProviderCreditsController.cs`, `ConsertaPraMim.Application/Services/AdminProviderCreditService.cs`, `ConsertaPraMim.Application/DTOs/AdminProviderCreditsDTOs.cs`, `ConsertaPraMim.Domain/Enums/ProviderCreditGrantType.cs`, `tests/ConsertaPraMim.Tests.Unit/Services/AdminProviderCreditServiceTests.cs`, `Documentacao/ADMIN_PORTAL/STORIES/DONE/ST-016-concessao-admin-creditos-notificacao-premio.md`
- Risco/Impacto: medio
- [2026-02-16] [ST-016] UI admin para concessao/estorno e consulta de creditos por prestador
- Tipo: feat
- Resumo: criado modulo web `Creditos` no portal admin com busca por prestador, saldo, extrato paginado, filtros operacionais (periodo/tipo/status) e modais para concessao/estorno com validacao de payload; manual operacional/QA atualizado com os novos fluxos.
- Arquivos principais: `ConsertaPraMim.Web.Admin/Controllers/AdminProviderCreditsController.cs`, `ConsertaPraMim.Web.Admin/Views/AdminProviderCredits/Index.cshtml`, `ConsertaPraMim.Web.Admin/Views/Shared/_Layout.cshtml`, `ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`, `Documentacao/ADMIN_PORTAL/STORIES/DONE/ST-016-concessao-admin-creditos-notificacao-premio.md`
- Risco/Impacto: medio
- [2026-02-16] [ST-016] Testes de integracao da API de creditos e notificacao de premio
- Tipo: test
- Resumo: adicionados testes SQLite de integracao para `AdminProviderCreditsController` cobrindo concessao via endpoint admin, atualizacao de saldo/extrato, auditoria e envio de notificacao realtime (`HubNotificationService`), alem de cenario de estorno com saldo insuficiente sem notificacao.
- Arquivos principais: `tests/ConsertaPraMim.Tests.Unit/Integration/Controllers/AdminProviderCreditsControllerSqliteIntegrationTests.cs`, `Documentacao/ADMIN_PORTAL/STORIES/DONE/ST-016-concessao-admin-creditos-notificacao-premio.md`, `Documentacao/ADMIN_PORTAL/INDEX.md`
- Risco/Impacto: baixo
- [2026-02-16] [ST-014] Revisao periodica do manual operacional/QA
- Tipo: docs
- Resumo: manual HTML revisado para refletir entregas ST-011/ST-013/ST-015, incluindo creditos via API admin, novos casos QA/checklist, troubleshooting e historico formal de revisoes.
- Arquivos principais: `ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`, `Documentacao/ADMIN_PORTAL/STORIES/DONE/ST-014-manual-html-operacao-qa-portal-admin.md`, `Documentacao/ADMIN_PORTAL/INDEX.md`
- Risco/Impacto: baixo
- [2026-02-14] [ST-014] Manual HTML completo de operacao e QA no portal admin
- Tipo: docs
- Resumo: criado manual operacional/QA em HTML dentro do `ConsertaPraMim.Web.Admin`, com cobertura de todos os modulos do admin, casos de uso, casos de teste funcionais, checklist de operacao, troubleshooting e regra obrigatoria de atualizacao do manual em toda mudanca funcional.
- Arquivos principais: `ConsertaPraMim.Web.Admin/Controllers/AdminManualController.cs`, `ConsertaPraMim.Web.Admin/Views/AdminManual/Index.cshtml`, `ConsertaPraMim.Web.Admin/Views/Shared/_Layout.cshtml`, `Documentacao/ADMIN_PORTAL/EPICS/EPIC-005-manual-operacional-qa-portal-admin.md`, `Documentacao/ADMIN_PORTAL/STORIES/DONE/ST-014-manual-html-operacao-qa-portal-admin.md`
- Risco/Impacto: baixo
- [2026-02-14] [ST-012] KPI de renda mensal de assinaturas no dashboard admin
- Tipo: feat
- Resumo: adicionado calculo de receita mensal estimada por planos (`Bronze`, `Silver`, `Gold`) no dashboard admin, com breakdown por plano e total de prestadores assinantes; seed ajustado para incluir prestador em plano `Gold` e seeder auxiliar atualizado para plano pagante.
- Arquivos principais: `ConsertaPraMim.Application/Services/AdminDashboardService.cs`, `ConsertaPraMim.Application/DTOs/AdminDashboardDTOs.cs`, `ConsertaPraMim.Web.Admin/Views/AdminHome/Index.cshtml`, `ConsertaPraMim.Infrastructure/Data/DbInitializer.cs`, `ConsertaPraMim.DatabaseSeeder/Program.cs`
- Risco/Impacto: medio
- [2026-02-13] [ST-001] Hardening inicial de seguranca para Admin
- Tipo: feat
- Resumo: bloqueio de auto-cadastro com role Admin, policy `AdminOnly` criada e seed de admin controlado por ambiente/config.
- Arquivos principais: `ConsertaPraMim.Application/Services/AuthService.cs`, `ConsertaPraMim.API/Controllers/AuthController.cs`, `ConsertaPraMim.Infrastructure/Data/DbInitializer.cs`, `ConsertaPraMim.Web.Provider/Controllers/AdminController.cs`
- Risco/Impacto: medio
- [2026-02-13] [ST-001] Cobertura de autorizacao para usuario nao-admin em rotas admin
- Tipo: test
- Resumo: adicionado teste de autorizacao para validar que usuarios nao-admin falham na policy `AdminOnly` e usuarios admin sao autorizados.
- Arquivos principais: `ConsertaPraMim.Tests.Unit/Services/AdminAuthorizationPolicyTests.cs`
- Risco/Impacto: baixo
- [2026-02-13] [ST-002] Bootstrap do novo portal web admin
- Tipo: feat
- Resumo: criado projeto `ConsertaPraMim.Web.Admin` com cookie auth, policy `AdminOnly`, login admin e dashboard inicial.
- Arquivos principais: `ConsertaPraMim.Web.Admin/Program.cs`, `ConsertaPraMim.Web.Admin/Controllers/AccountController.cs`, `ConsertaPraMim.Web.Admin/Controllers/AdminHomeController.cs`, `ConsertaPraMim.Web.Admin/Views/Shared/_Layout.cshtml`
- Risco/Impacto: medio
- [2026-02-13] [ST-003] API de dashboard administrativo com filtros e eventos paginados
- Tipo: feat
- Resumo: criado endpoint `GET /api/admin/dashboard` protegido por `AdminOnly`, com agregados de usuarios/pedidos/propostas/chat e eventos recentes paginados com filtros.
- Arquivos principais: `ConsertaPraMim.API/Controllers/AdminDashboardController.cs`, `ConsertaPraMim.Application/Services/AdminDashboardService.cs`, `ConsertaPraMim.Application/DTOs/AdminDashboardDTOs.cs`, `ConsertaPraMim.Infrastructure/Repositories/ProposalRepository.cs`
- Risco/Impacto: medio
- [2026-02-13] [ST-004] API Admin para gestao de usuarios com auditoria
- Tipo: feat
- Resumo: adicionados endpoints admin para listar/filtrar usuarios, detalhe por id e alteracao de status com regras de seguranca (ultimo admin e auto-bloqueio) e registro de auditoria.
- Arquivos principais: `ConsertaPraMim.API/Controllers/AdminUsersController.cs`, `ConsertaPraMim.Application/Services/AdminUserService.cs`, `ConsertaPraMim.Domain/Entities/AdminAuditLog.cs`, `ConsertaPraMim.Infrastructure/Migrations/20260213021345_AddAdminAuditLogs.cs`
- Risco/Impacto: medio
- [2026-02-13] [ST-005] API Admin para gestao de pedidos e propostas
- Tipo: feat
- Resumo: criados endpoints admin para listagem/detalhe de pedidos, alteracao administrativa de status, listagem de propostas e invalidacao com auditoria e regras de seguranca.
- Arquivos principais: `ConsertaPraMim.API/Controllers/AdminServiceRequestsController.cs`, `ConsertaPraMim.API/Controllers/AdminProposalsController.cs`, `ConsertaPraMim.Application/Services/AdminRequestProposalService.cs`, `ConsertaPraMim.Infrastructure/Migrations/20260213110423_AddProposalInvalidation.cs`
- Risco/Impacto: medio
- [2026-02-13] [ST-006] API Admin para monitoramento de chats e notificacao manual
- Tipo: feat
- Resumo: adicionados endpoints admin para listagem/detalhe de conversas, consulta de anexos com filtros, envio manual de notificacao para usuario e auditoria com mascaramento de dados sensiveis.
- Arquivos principais: `ConsertaPraMim.API/Controllers/AdminChatsController.cs`, `ConsertaPraMim.API/Controllers/AdminChatAttachmentsController.cs`, `ConsertaPraMim.API/Controllers/AdminNotificationsController.cs`, `ConsertaPraMim.Application/Services/AdminChatNotificationService.cs`
- Risco/Impacto: medio
- [2026-02-13] [ST-007] Dashboard web admin integrado com API e polling
- Tipo: feat
- Resumo: dashboard do `ConsertaPraMim.Web.Admin` passou a consumir `GET /api/admin/dashboard` com filtros, cards KPI, tabela de eventos e estados de loading/erro/vazio com atualizacao automatica via polling controlado.
- Arquivos principais: `ConsertaPraMim.Web.Admin/Controllers/AdminHomeController.cs`, `ConsertaPraMim.Web.Admin/Views/AdminHome/Index.cshtml`, `ConsertaPraMim.Web.Admin/Services/AdminDashboardApiClient.cs`, `ConsertaPraMim.Web.Admin/Controllers/AccountController.cs`
- Risco/Impacto: medio
- [2026-02-13] [ST-008] UI Admin para operacao de usuarios
- Tipo: feat
- Resumo: implementada tela de usuarios no portal admin com filtros e paginacao, detalhe de usuario, acao de ativar/desativar com confirmacao e atualizacao da linha sem refresh completo.
- Arquivos principais: `ConsertaPraMim.Web.Admin/Controllers/AdminUsersController.cs`, `ConsertaPraMim.Web.Admin/Views/AdminUsers/Index.cshtml`, `ConsertaPraMim.Web.Admin/Views/AdminUsers/Details.cshtml`, `ConsertaPraMim.Web.Admin/Services/AdminUsersApiClient.cs`
- Risco/Impacto: medio
- [2026-02-13] [ST-009] UI Admin para operacao de pedidos, propostas e conversas
- Tipo: feat
- Resumo: criados modulos web admin para pedidos, propostas e conversas com filtros, detalhes, acoes administrativas com confirmacao, envio de notificacao manual e navegacao cruzada entre usuario, pedido, proposta e chat.
- Arquivos principais: `ConsertaPraMim.Web.Admin/Controllers/AdminServiceRequestsController.cs`, `ConsertaPraMim.Web.Admin/Controllers/AdminProposalsController.cs`, `ConsertaPraMim.Web.Admin/Controllers/AdminChatsController.cs`, `ConsertaPraMim.Web.Admin/Services/AdminOperationsApiClient.cs`
- Risco/Impacto: medio
- [2026-02-13] [ST-010] Auditoria final, QA de seguranca e rollout com desativacao do admin legado
- Tipo: feat
- Resumo: padronizado metadata de auditoria com `before/after` nas acoes sensiveis, adicionados logs estruturados para operacao/incidentes, criada feature flag `LegacyAdmin:Enabled` no portal do prestador, adicionados testes automatizados de autorizacao/feature flag e publicado runbook de deploy/rollback.
- Arquivos principais: `ConsertaPraMim.Application/Services/AdminUserService.cs`, `ConsertaPraMim.Application/Services/AdminRequestProposalService.cs`, `ConsertaPraMim.Application/Services/AdminChatNotificationService.cs`, `ConsertaPraMim.Web.Provider/Controllers/AdminController.cs`, `ConsertaPraMim.Tests.Unit/Services/ProviderLegacyAdminFeatureFlagTests.cs`, `Documentacao/ADMIN_PORTAL/RUNBOOKS/DEPLOY_ROLLBACK_ST-010.md`
- Risco/Impacto: medio

- [2026-02-23] [GOV-001] Governanca global: manual QA/Operacao obrigatorio para qualquer feature
- Tipo: docs
- Resumo: atualizado `AGENTS.md` com regra global de criacao/atualizacao de manual QA/Operacao para qualquer feature/alteracao funcional em qualquer projeto, exigindo versionamento no mesmo ciclo de entrega e incluindo a regra na DoD.
- Arquivos principais: `AGENTS.md`
- Risco/Impacto: baixo
- [2026-02-23] [GOV-002] Governanca operacional padronizada para entregas e changelog em Released
- Tipo: docs
- Resumo: adicionadas diretrizes de trabalho no `AGENTS.md` (Epic/Story/Tasks, commit por task, build/testes minimos, atualizacao de manual/changelog/diagramas, estrategia de branch/PR, UTC->America/Sao_Paulo, seguranca de secrets, versionamento mobile e regressao), alem da regra obrigatoria de promover entradas do changelog para `Released` antes de qualquer commit/push, inclusive em `dev-local`.
- Arquivos principais: `AGENTS.md`
- Risco/Impacto: baixo

## Template de entrada

- `[YYYY-MM-DD] [ST-XXX] Titulo curto`
- `Tipo: feat|fix|refactor|docs|test`
- `Resumo: o que foi entregue`
- `Arquivos principais: caminho1, caminho2`
- `Risco/Impacto: baixo|medio|alto`
