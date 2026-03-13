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

## Integracao Chatwoot - deploy da VPS

### Estado atual do ambiente

- URL publica: `https://chatwoot.consertapramim.com`
- Stack Docker: `/opt/chatwoot`
- Servicos: `chatwoot-rails`, `chatwoot-sidekiq`, `chatwoot-postgres`, `chatwoot-redis`
- Proxy reverso Nginx: `/etc/nginx/sites-available/chatwoot.consertapramim.com.conf`
- Certificado TLS: `/etc/letsencrypt/live/chatwoot.consertapramim.com/fullchain.pem`
- Renovacao TLS: job global em `/etc/cron.d/profinder-certbot-renew`

### Comportamento esperado

- A URL publica deve responder em HTTPS e redirecionar para `/installation/onboarding` enquanto o primeiro admin nao for criado.
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

### Troubleshooting

- `502 Bad Gateway`: validar `docker compose ps`, `docker logs --tail 50 chatwoot-rails` e `docker logs --tail 50 chatwoot-sidekiq`.
- `SSL certificate problem`: validar se o certificado continua presente em `/etc/letsencrypt/live/chatwoot.consertapramim.com/`.
- `erro de memoria` ou reinicio de container: validar `free -h`, `docker stats` e se `vm.overcommit_memory` continua em `1`.
- `pagina em branco` apos login: validar se o `FRONTEND_URL` em `/opt/chatwoot/.env` continua `https://chatwoot.consertapramim.com`.
