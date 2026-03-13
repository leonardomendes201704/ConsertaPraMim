# Manual de QA e Operacao - ConsertaPraMim.Web.CpmFull

## Objetivo

Orientar validacao funcional e operacao basica do projeto `ConsertaPraMim.Web.CpmFull`.

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
- Quando o lead ainda nao possuir `ChatwootConversationId`, o sistema deve criar a conversa e registrar uma primeira mensagem privada com o resumo operacional do lead.
- Em falha externa, o lead local continua salvo e os campos `ChatwootSyncStatus`/`ChatwootLastError` devem refletir o erro sem quebrar o Kanban.
- O modal de detalhe do lead deve oferecer o botao `Sincronizar Chatwoot` para reprocessar leads antigos ou falhas anteriores.

### Checklist de QA

1. Criar um lead novo no funil de clientes com telefone e e-mail validos.
2. Confirmar retorno do fluxo sem erro funcional na tela.
3. Abrir o detalhe do lead e validar `Sync Chatwoot = Sincronizado`.
4. Confirmar que `Contato Chatwoot`, `Conversa Chatwoot` e `Inbox Chatwoot` estao preenchidos.
5. Entrar em `https://chatwoot.consertapramim.com` e validar o contato/conversa no inbox `CPM Clientes`.
6. Validar que a primeira mensagem da conversa foi criada como anotacao privada com resumo do lead.
7. Editar o mesmo lead e confirmar que o fluxo reaproveita os IDs ja gravados, sem criar nova conversa.
8. Escolher um lead antigo ainda sem sync e acionar `Sincronizar Chatwoot` no modal.
9. Confirmar atualizacao imediata do status e dos IDs no detalhe do lead.
10. Repetir o fluxo com um lead do funil de prestadores e validar uso do inbox `CPM Prestadores`.
11. Criar um lead sem telefone e sem e-mail.
12. Confirmar que o lead local continua salvo, mas com `Sync Chatwoot = Falha` e `Ultimo erro Chatwoot` explicando a ausencia de dados minimos.

### Troubleshooting

- `Lead sem telefone ou e-mail valido`: corrigir o cadastro e usar o botao `Sincronizar Chatwoot`.
- `Chatwoot retornou erro HTTP 401`: validar token admin, proxy reverso e se o header `api_access_token` continua sendo encaminhado pelo Nginx.
- `Phone number has already been taken`: o contato pode ter sido criado manualmente sem `identifier`; validar busca por telefone/e-mail no Chatwoot e reprocessar o lead.
- O modal nao atualiza apos clicar em `Sincronizar Chatwoot`: validar o endpoint `POST /admin/funil/lead/{id}/chatwoot/sincronizar` e o anti-forgery token da pagina.
- Nova conversa nao aparece: validar `ChatwootConversationId`, `Inbox Chatwoot` e se a chamada de criacao da conversa nao falhou antes da primeira mensagem privada.

## Integracao Chatwoot - sincronizacao de etapa do Kanban

### Objetivo desta etapa

- Refletir no Chatwoot a mudanca de etapa do card no Kanban, atualizando status da conversa, labels gerenciadas pelo CPM e custom attributes operacionais.

### Comportamento esperado

- Ao mover um card entre etapas no Kanban, o CPM Full deve manter a mudanca local como fonte de verdade e tentar sincronizar a conversa correspondente no Chatwoot.
- A sincronizacao de etapa deve atualizar:
- status da conversa (`open`, `pending` ou `resolved`);
- labels gerenciadas pelo prefixo `cpm_`, preservando labels manuais nao pertencentes ao CPM;
- `custom_attributes` da conversa com `cpm_lead_id`, `cpm_board_type`, `cpm_stage_name` e `cpm_stage_slug`.
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
9. labels `cpm_` compatíveis com a etapa atual;
10. preservacao de labels manuais nao pertencentes ao prefixo `cpm_`.
11. Repetir o teste com card ainda sem conversa no Chatwoot e confirmar bootstrap automatico antes da sync de etapa.

### Troubleshooting

- O card moveu, mas o Chatwoot nao refletiu a etapa: abrir o detalhe do lead e validar `Ultimo erro Chatwoot`.
- Labels manuais sumiram: revisar se houve label manual usando prefixo `cpm_`; esse prefixo esta reservado para labels gerenciadas pelo CPM.
- Status de conversa inesperado: revisar o mapa fixo em `Integrations/Chatwoot/ChatwootStageMapping.cs`.
- Falha recorrente de sync de etapa: usar `Sincronizar Chatwoot` no modal para reprocessar o lead e confirmar bootstrap de contato/conversa antes de novo drag-and-drop.

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
