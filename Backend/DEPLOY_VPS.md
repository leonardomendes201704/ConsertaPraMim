# Deploy na VPS com HTTPS

Este guia publica 8 projetos Docker independentes:
- Site raiz CPM Full (`cpm-prd-cpmfull`)
- API (`cpm-prd-api`)
- Portal Admin (`cpm-prd-admin`)
- Portal Cliente (`cpm-prd-cliente`)
- Portal Prestador (`cpm-prd-prestador`)
- Mobile WebView Cliente (`cpm-prd-app-cliente`)
- Mobile WebView Prestador (`cpm-prd-app-prestador`)
- Mobile WebView Admin (`cpm-prd-app-admin`)

Arquivos compose:
- `Backend/docker-compose.vps.web-cpmfull.yml`
- `Backend/docker-compose.vps.api.yml`
- `Backend/docker-compose.vps.web-admin.yml`
- `Backend/docker-compose.vps.web-client.yml`
- `Backend/docker-compose.vps.web-provider.yml`
- `Backend/docker-compose.vps.mobile-webview-client.yml`
- `Backend/docker-compose.vps.mobile-webview-provider.yml`
- `Backend/docker-compose.vps.mobile-webview-admin.yml`

Arquivos de apoio para HTTPS:
- `Backend/docker/vps/nginx.portals.https.conf.example`
- `Backend/.env.vps.example`

## Topologia recomendada

Em `main/master` (PROD), os portais web, o CPM Full raiz e a API devem ficar atras de Nginx em `80/443`, com os containers publicados em `127.0.0.1`.

Em `dev-local` (DEV), a mesma stack sobe na VPS com bind `0.0.0.0` e portas dedicadas para nao conflitar com producao, podendo ser exposta por subdominios HTTPS (recomendado) ou por `IP:porta` (fallback).

URLs publicas recomendadas:
- `https://www.consertapramim.com`
- `https://consertapramim.com` -> redirect 301 para `https://www.consertapramim.com`
- `https://admin.consertapramim.com`
- `https://cliente.consertapramim.com`
- `https://prestador.consertapramim.com`
- `https://api.consertapramim.com`

Mapeamento interno esperado:
- `www.consertapramim.com` -> `127.0.0.1:5088`
- `admin.consertapramim.com` -> `127.0.0.1:5151`
- `cliente.consertapramim.com` -> `127.0.0.1:5069`
- `prestador.consertapramim.com` -> `127.0.0.1:5140`
- `api.consertapramim.com` -> `127.0.0.1:5193`

Observacao operacional:
- os apps ASP.NET agora usam `ForwardedHeaders` para interpretar corretamente `X-Forwarded-Proto`, `X-Forwarded-For` e `X-Forwarded-Host` atras do Nginx;
- a API aceita redirecionamento HTTPS controlado por `ENFORCE_API_HTTPS_REDIRECTION=true`;
- o CPM Full publicado na raiz responde `GET /health`, interpreta proxy reverso e depende de SQL Server para operacao completa;
- os portais web (`admin`, `cliente`, `prestador`) e o CPM Full exibem rodape fixo de ambiente de homologacao somente quando `DEPLOY_PROFILE=development`;
- os Mobile WebViews (`:5181/:5182/:5183` em prod e `:6181/:6182/:6183` em dev) sao HTTP por padrao quando publicados por porta direta; use `PUBLIC_MOBILE_*_WEBVIEW_URL` com URL HTTPS apenas se houver proxy TLS dedicado para esses endpoints;
- no `dev-local`, os `healthchecks` do workflow validam pelos endpoints publicados em `http://<VPS_PUBLIC_HOST>:porta`;
- no `main/master`, os `healthchecks` continuam validando pela malha local (`127.0.0.1`) na propria VPS;
- o deploy agora forca `docker compose -p <CONTAINER_PREFIX>-<servico>` para isolar `DEV/PROD` e evitar remocao indevida com `--remove-orphans` entre jobs paralelos;
- o nome `PUBLIC_LANDING_URL` foi mantido por compatibilidade no workflow e nos demais servicos, mas essa URL agora representa o dominio raiz servido pelo `ConsertaPraMim.Web.CpmFull`.

## 1) DNS na Hostinger/HostGator

Garanta que estes registros apontem para a IP publica da VPS:
- `consertapramim.com`
- `www.consertapramim.com`
- `admin.consertapramim.com`
- `cliente.consertapramim.com`
- `prestador.consertapramim.com`
- `api.consertapramim.com`

Observacao:
- se `consertapramim.com` e `www.consertapramim.com` ja apontam para a VPS, mantenha;
- os portais web nao devem mais ser publicados como `http://www.consertapramim.com:5151`.

## 2) Preparacao na VPS

```bash
cd ~
git clone <URL_DO_REPO> ConsertaPraMimWeb
cd ConsertaPraMimWeb
cp Backend/.env.vps.example Backend/.env.vps
nano Backend/.env.vps
chmod +x scripts/deploy/vps-deploy.sh scripts/deploy/vps-deploy-service.sh
```

Preencha no `Backend/.env.vps` pelo menos:
- `DEPLOY_PROFILE` (`production` ou `development`)
- `APP_ENVIRONMENT` (`Production` ou `Development`)
- `BIND_HOST` (`127.0.0.1` em prod / `0.0.0.0` em dev)
- `CONTAINER_PREFIX` (`cpm-prd` em prod / `cpm-hml` em dev)
- `VOLUME_PREFIX` (`cpm_prd` em prod / `cpm_hml` em dev)
- `VPS_PUBLIC_HOST` (host ou IP cru, sem `http://` ou `https://`)
- `INTERNAL_API_URL`
- `PUBLIC_LANDING_URL`
- `PUBLIC_API_URL`
- `PUBLIC_ADMIN_URL`
- `PUBLIC_CLIENT_URL`
- `PUBLIC_PROVIDER_URL`
- `PUBLIC_MOBILE_CLIENT_WEBVIEW_URL` (ex.: `http://<host>:5181` em prod, ou URL HTTPS dedicada se houver proxy)
- `PUBLIC_MOBILE_PROVIDER_WEBVIEW_URL` (ex.: `http://<host>:5182` em prod, ou URL HTTPS dedicada se houver proxy)
- `PUBLIC_MOBILE_ADMIN_WEBVIEW_URL` (ex.: `http://<host>:5183` em prod, ou URL HTTPS dedicada se houver proxy)
- `DB_PASSWORD`
- `DB_HOST` (normalmente `mssql`)
- `JWT_SECRET_KEY`
- `SEED_DEFAULT_PASSWORD`
- obrigatorio para Chatwoot no CPM Full publicado: `CPMFULL_CHATWOOT_*`
- opcional para endurecimento do Chatwoot no CPM Full publicado: `CPMFULL_CHATWOOT_ALLOWED_WEBHOOK_IPS`, `CPMFULL_CHATWOOT_WEBHOOK_PAYLOAD_CLEANUP_ENABLED`, `CPMFULL_CHATWOOT_WEBHOOK_PAYLOAD_RETENTION_DAYS`, `CPMFULL_CHATWOOT_WEBHOOK_PAYLOAD_CLEANUP_INTERVAL_MINUTES`

Observacao critica:
- o job `deploy-web-cpmfull` precisa escrever `CPMFULL_CHATWOOT_*` em `Backend/.env.vps` para o container publicado enxergar `Chatwoot__Enabled=true`;
- sem esses secrets no environment do GitHub Actions, o CPM Full sobe normalmente, mas a integracao fica desabilitada em runtime e o Kanban responde `Integracao com Chatwoot desabilitada no ambiente atual.`;
- depois da correcao `CPMFULL-016`, o healthcheck do workflow tambem consulta `/internal/health/chatwoot` sempre que `CPMFULL_CHATWOOT_ENABLED=true`, para impedir falso positivo de deploy.
- a allowlist de IP do webhook e opcional; nao habilite `CPMFULL_CHATWOOT_ALLOWED_WEBHOOK_IPS` sem antes confirmar qual IP/faixa realmente chega ao CPM Full apos Nginx/proxy reverso.
- o worker de retention do webhook usa `CPMFULL_CHATWOOT_WEBHOOK_PAYLOAD_RETENTION_DAYS` e `CPMFULL_CHATWOOT_WEBHOOK_PAYLOAD_CLEANUP_INTERVAL_MINUTES`; o padrao publicado e expurgar payload bruto e assinatura apos `14` dias, preservando somente metadados operacionais.

Exemplo minimo (PROD):

```env
DEPLOY_PROFILE=production
APP_ENVIRONMENT=Production
BIND_HOST=127.0.0.1
CONTAINER_PREFIX=cpm-prd
VOLUME_PREFIX=cpm_prd
VPS_PUBLIC_HOST=SEU_IP_OU_HOST_DA_VPS

INTERNAL_API_URL=http://cpm-prd-api:8080
PUBLIC_LANDING_URL=https://www.consertapramim.com
PUBLIC_API_URL=https://api.consertapramim.com
PUBLIC_ADMIN_URL=https://admin.consertapramim.com
PUBLIC_CLIENT_URL=https://cliente.consertapramim.com
PUBLIC_PROVIDER_URL=https://prestador.consertapramim.com
PUBLIC_MOBILE_CLIENT_WEBVIEW_URL=http://SEU_IP_OU_HOST_DA_VPS:5181
PUBLIC_MOBILE_PROVIDER_WEBVIEW_URL=http://SEU_IP_OU_HOST_DA_VPS:5182
PUBLIC_MOBILE_ADMIN_WEBVIEW_URL=http://SEU_IP_OU_HOST_DA_VPS:5183
ENFORCE_API_HTTPS_REDIRECTION=true

API_PORT=5193
LANDING_PORT=5088
ADMIN_PORT=5151
CLIENT_PORT=5069
PROVIDER_PORT=5140
MOBILE_CLIENT_WEBVIEW_PORT=5181
MOBILE_PROVIDER_WEBVIEW_PORT=5182
MOBILE_ADMIN_WEBVIEW_PORT=5183

DB_NAME=ConsertaPraMimDb
DB_USER=sa
DB_PASSWORD=ALTERAR_AQUI
DB_HOST=mssql

JWT_SECRET_KEY=ALTERAR_PARA_UMA_CHAVE_BEM_FORTE_COM_32+_CARACTERES
SEED_DEFAULT_PASSWORD=ALTERAR_AQUI
CPMFULL_CHATWOOT_ENABLED=true
CPMFULL_CHATWOOT_BASE_URL=https://chatwoot.consertapramim.com
CPMFULL_CHATWOOT_API_ACCESS_TOKEN=ALTERAR_AQUI
CPMFULL_CHATWOOT_ACCOUNT_ID=1
CPMFULL_CHATWOOT_CLIENTS_INBOX_ID=1
CPMFULL_CHATWOOT_PROVIDERS_INBOX_ID=2
CPMFULL_CHATWOOT_WEBHOOK_SECRET=ALTERAR_AQUI
CPMFULL_CHATWOOT_ALLOWED_WEBHOOK_IPS=
CPMFULL_CHATWOOT_WEBHOOK_PAYLOAD_CLEANUP_ENABLED=true
CPMFULL_CHATWOOT_WEBHOOK_PAYLOAD_RETENTION_DAYS=14
CPMFULL_CHATWOOT_WEBHOOK_PAYLOAD_CLEANUP_INTERVAL_MINUTES=360
```

Exemplo minimo (DEV na mesma VPS):

```env
DEPLOY_PROFILE=development
APP_ENVIRONMENT=Development
BIND_HOST=0.0.0.0
CONTAINER_PREFIX=cpm-hml
VOLUME_PREFIX=cpm_hml
VPS_PUBLIC_HOST=187.77.48.150

INTERNAL_API_URL=http://cpm-hml-api:8080
PUBLIC_LANDING_URL=http://187.77.48.150:6088
PUBLIC_API_URL=http://187.77.48.150:6193
PUBLIC_ADMIN_URL=http://187.77.48.150:6151
PUBLIC_CLIENT_URL=http://187.77.48.150:6069
PUBLIC_PROVIDER_URL=http://187.77.48.150:6140
PUBLIC_MOBILE_CLIENT_WEBVIEW_URL=http://187.77.48.150:6181
PUBLIC_MOBILE_PROVIDER_WEBVIEW_URL=http://187.77.48.150:6182
PUBLIC_MOBILE_ADMIN_WEBVIEW_URL=http://187.77.48.150:6183
ENFORCE_API_HTTPS_REDIRECTION=false

API_PORT=6193
LANDING_PORT=6088
ADMIN_PORT=6151
CLIENT_PORT=6069
PROVIDER_PORT=6140
MOBILE_CLIENT_WEBVIEW_PORT=6181
MOBILE_PROVIDER_WEBVIEW_PORT=6182
MOBILE_ADMIN_WEBVIEW_PORT=6183

DB_NAME=ConsertaPraMimDbDev
DB_USER=sa
DB_PASSWORD=ALTERAR_AQUI
DB_HOST=mssql

JWT_SECRET_KEY=ALTERAR_PARA_UMA_CHAVE_BEM_FORTE_COM_32+_CARACTERES
SEED_DEFAULT_PASSWORD=ALTERAR_AQUI
CPMFULL_CHATWOOT_ENABLED=true
CPMFULL_CHATWOOT_BASE_URL=https://chatwoot.consertapramim.com
CPMFULL_CHATWOOT_API_ACCESS_TOKEN=ALTERAR_AQUI
CPMFULL_CHATWOOT_ACCOUNT_ID=1
CPMFULL_CHATWOOT_CLIENTS_INBOX_ID=1
CPMFULL_CHATWOOT_PROVIDERS_INBOX_ID=2
CPMFULL_CHATWOOT_WEBHOOK_SECRET=ALTERAR_AQUI
CPMFULL_CHATWOOT_ALLOWED_WEBHOOK_IPS=
CPMFULL_CHATWOOT_WEBHOOK_PAYLOAD_CLEANUP_ENABLED=true
CPMFULL_CHATWOOT_WEBHOOK_PAYLOAD_RETENTION_DAYS=14
CPMFULL_CHATWOOT_WEBHOOK_PAYLOAD_CLEANUP_INTERVAL_MINUTES=360
```

## 2.1) Validacao do rodape de Homologacao (HML)

Com `DEPLOY_PROFILE=development`:
- `http://<host>:6151` (admin), `:6069` (cliente), `:6140` (prestador) e `:6088` (CPM Full raiz) devem exibir o rodape fixo:
  `Ambiente de Homologacao (HML) - uso exclusivo para testes`.

Com `DEPLOY_PROFILE=production`:
- os mesmos projetos web nao devem renderizar esse rodape.

## 3) Instalar Nginx e Certbot na VPS

Firewall recomendado:
- deixar abertas publicamente apenas `80` e `443`;
- em `production`, manter `5088`, `5151`, `5069`, `5140` e `5193` bloqueadas externamente, porque os containers atendem via `127.0.0.1`;
- em `development`, abrir apenas as portas de DEV se necessario (`6088`, `6151`, `6069`, `6140`, `6193`) e restringir por IP de origem no firewall sempre que possivel.

Ubuntu/Debian:

```bash
sudo apt update
sudo apt install -y nginx python3 python3-venv libaugeas0
sudo python3 -m venv /opt/certbot
sudo /opt/certbot/bin/pip install --upgrade pip certbot certbot-nginx
sudo ln -sf /opt/certbot/bin/certbot /usr/bin/certbot
```

## 4) Configurar o Nginx

Copie o template de apoio para a configuracao real:

```bash
sudo cp Backend/docker/vps/nginx.portals.https.conf.example /etc/nginx/sites-available/consertapramim.conf
sudo nano /etc/nginx/sites-available/consertapramim.conf
```

Substitua os placeholders:
- `__ROOT_DOMAIN__` -> `consertapramim.com`
- `__WWW_DOMAIN__` -> `www.consertapramim.com`
- `__ADMIN_DOMAIN__` -> `admin.consertapramim.com`
- `__CLIENT_DOMAIN__` -> `cliente.consertapramim.com`
- `__PROVIDER_DOMAIN__` -> `prestador.consertapramim.com`
- `__API_DOMAIN__` -> `api.consertapramim.com`

Ative o site e recarregue:

```bash
sudo ln -sf /etc/nginx/sites-available/consertapramim.conf /etc/nginx/sites-enabled/consertapramim.conf
sudo nginx -t
sudo systemctl reload nginx
```

## 5) Emitir os certificados HTTPS

Primeira emissao:

```bash
sudo certbot --nginx \
  -d consertapramim.com \
  -d www.consertapramim.com \
  -d admin.consertapramim.com \
  -d cliente.consertapramim.com \
  -d prestador.consertapramim.com \
  -d api.consertapramim.com
```

Se voce ja emitiu certificado antes apenas para admin/cliente/prestador/api, rode com `--expand`:

```bash
sudo certbot --nginx --expand \
  -d consertapramim.com \
  -d www.consertapramim.com \
  -d admin.consertapramim.com \
  -d cliente.consertapramim.com \
  -d prestador.consertapramim.com \
  -d api.consertapramim.com
```

Valide a renovacao automatica:

```bash
sudo systemctl list-timers | grep certbot
sudo certbot renew --dry-run
```

## 6) Deploy manual dos containers

Deploy completo:

```bash
cd ~/ConsertaPraMimWeb
MSSQL_CONTAINER_NAME=mssql-mssql-1 MSSQL_HOST_ALIAS=mssql scripts/deploy/vps-deploy.sh
```

Deploy por servico:

```bash
cd ~/ConsertaPraMimWeb
MSSQL_CONTAINER_NAME=mssql-mssql-1 MSSQL_HOST_ALIAS=mssql scripts/deploy/vps-deploy-service.sh "$PWD" api
MSSQL_CONTAINER_NAME=mssql-mssql-1 MSSQL_HOST_ALIAS=mssql scripts/deploy/vps-deploy-service.sh "$PWD" web-cpmfull
MSSQL_CONTAINER_NAME=mssql-mssql-1 MSSQL_HOST_ALIAS=mssql scripts/deploy/vps-deploy-service.sh "$PWD" web-admin
MSSQL_CONTAINER_NAME=mssql-mssql-1 MSSQL_HOST_ALIAS=mssql scripts/deploy/vps-deploy-service.sh "$PWD" web-client
MSSQL_CONTAINER_NAME=mssql-mssql-1 MSSQL_HOST_ALIAS=mssql scripts/deploy/vps-deploy-service.sh "$PWD" web-provider
MSSQL_CONTAINER_NAME=mssql-mssql-1 MSSQL_HOST_ALIAS=mssql scripts/deploy/vps-deploy-service.sh "$PWD" mobile-webview-client
MSSQL_CONTAINER_NAME=mssql-mssql-1 MSSQL_HOST_ALIAS=mssql scripts/deploy/vps-deploy-service.sh "$PWD" mobile-webview-provider
MSSQL_CONTAINER_NAME=mssql-mssql-1 MSSQL_HOST_ALIAS=mssql scripts/deploy/vps-deploy-service.sh "$PWD" mobile-webview-admin
```

Observacao operacional:
- o script `scripts/deploy/vps-deploy-service.sh` faz `build` antes do `up` e remove o container alvo do ambiente corrente (`<CONTAINER_PREFIX>-api`, `<CONTAINER_PREFIX>-cpmfull`, `<CONTAINER_PREFIX>-admin`, `<CONTAINER_PREFIX>-cliente`, `<CONTAINER_PREFIX>-prestador`, `<CONTAINER_PREFIX>-app-*`) para evitar conflito de `container_name`;
- durante a migracao de nomenclatura, o script tambem remove automaticamente nomes legados (`cpm-*` / `cpm-dev-*` e sufixos antigos `web-*` / `mobile-webview-*`) para evitar conflito de porta no primeiro deploy com o novo padrao;
- em `production`, `web-cpmfull`, `api`, `web-admin`, `web-client` e `web-provider` ficam publicados em `127.0.0.1`, por isso o acesso externo precisa passar pelo Nginx;
- em `development`, os mesmos servicos podem ser publicados por `IP:porta` (bind `0.0.0.0`) para validacao rapida;
- os portais usam `INTERNAL_API_URL` para chamadas server-side na rede Docker e `PUBLIC_API_URL` para URLs injetadas no browser;
- os compose files dos portais forcam `URLS` e `ASPNETCORE_URLS` para a porta do ambiente (`ADMIN_PORT`, `CLIENT_PORT`, `PROVIDER_PORT`), evitando bind interno acidental nas portas legadas de `appsettings.Development.json`;
- o CPM Full usa `ConnectionStrings__DefaultConnection` para SQL Server e pode receber a integracao Chatwoot por `CPMFULL_CHATWOOT_*`; `PUBLIC_LANDING_URL` continua representando a URL publica raiz para os demais servicos.

## 7) Validacao pos-deploy

Validacao interna na VPS:

```bash
curl -I http://127.0.0.1:5088/health
curl -I http://127.0.0.1:5193/health
curl -I http://127.0.0.1:5151/Account/Login
curl -I http://127.0.0.1:5069/Account/Login
curl -I http://127.0.0.1:5140/Account/Login
```

Validacao publica:

```bash
curl -I https://consertapramim.com
curl -I https://www.consertapramim.com
curl -I https://www.consertapramim.com/health
curl -I https://api.consertapramim.com/health
curl -I https://admin.consertapramim.com/Account/Login
curl -I https://cliente.consertapramim.com/Account/Login
curl -I https://prestador.consertapramim.com/Account/Login
```

Validacao da malha Docker:

```bash
docker inspect cpm-prd-admin --format '{{range .Config.Env}}{{println .}}{{end}}' | grep -E '^(ApiBaseUrl|BrowserApiBaseUrl)='
docker inspect cpm-prd-cliente --format '{{range .Config.Env}}{{println .}}{{end}}' | grep -E '^(ApiBaseUrl|BrowserApiBaseUrl)='
docker inspect cpm-prd-prestador --format '{{range .Config.Env}}{{println .}}{{end}}' | grep -E '^(ApiBaseUrl|BrowserApiBaseUrl)='
docker run --rm --network conserta_net curlimages/curl:8.12.1 -I http://cpm-prd-api:8080/health
# em DEV (CONTAINER_PREFIX=cpm-hml):
# docker run --rm --network conserta_net curlimages/curl:8.12.1 -I http://cpm-hml-api:8080/health
docker run --rm --network conserta_net curlimages/curl:8.12.1 -I https://api.consertapramim.com/health
```

Esperado:
- `ApiBaseUrl=http://<CONTAINER_PREFIX>-api:8080` (ex.: `cpm-prd-api` em prod, `cpm-hml-api` em dev)
- `BrowserApiBaseUrl=https://api.consertapramim.com` (prod) ou `http://<IP>:6193` (dev-local)

Validacao funcional no navegador:
- abrir `https://www.consertapramim.com`;
- confirmar CTA para `cliente`, `prestador`, `admin` e `swagger`;
- abrir `https://admin.consertapramim.com`;
- abrir `https://cliente.consertapramim.com`;
- abrir `https://prestador.consertapramim.com`;
- confirmar que chat/upload/SignalR nao apresentam `mixed content` no console;
- confirmar que o Swagger da API abre em `https://api.consertapramim.com/swagger` quando habilitado.

## 8) CI/CD com deploy seletivo

Workflow: `.github/workflows/deploy-vps.yml`

Comportamento:
- `push` para `dev-local`: deploya stack `DEV` na mesma VPS (HML), preferencialmente por subdominios HTTPS
- `push` para `main/master`: deploya stack `PROD` na mesma VPS, publicada por dominio/subdominios
- `workflow_dispatch`: deploya todos os projetos
- se alterar arquivos globais de infra/deploy, deploya todos

Isolamento automatico por branch (workflow):

- `dev-local`:
  `DEPLOY_PROFILE=development`,
  `BIND_HOST=0.0.0.0`,
  `CONTAINER_PREFIX=cpm-hml`,
  `VOLUME_PREFIX=cpm_hml`,
  `DB_NAME=ConsertaPraMimDbDev`
- `main/master`:
  `DEPLOY_PROFILE=production`,
  `BIND_HOST=127.0.0.1`,
  `CONTAINER_PREFIX=cpm-prd`,
  `VOLUME_PREFIX=cpm_prd`,
  `DB_NAME=ConsertaPraMimDb`

Portas por ambiente:

- `dev-local`:
  API `6193`, CPM Full raiz `6088`, Admin `6151`, Cliente `6069`, Prestador `6140`,
  Mobile WebViews `6181/6182/6183`
- `main/master`:
  API `5193`, CPM Full raiz `5088`, Admin `5151`, Cliente `5069`, Prestador `5140`,
  Mobile WebViews `5181/5182/5183`

Configuracao recomendada no GitHub:

- criar environments `development` e `production`;
- cadastrar os mesmos nomes de secrets em ambos os environments;
- em `development`, manter `VPS_PUBLIC_HOST` com IP da VPS;
- em `development`, configurar `PUBLIC_*_URL` e `PUBLIC_MOBILE_*_WEBVIEW_URL` com subdominios HML HTTPS;
- em `production`, manter `PUBLIC_*_URL` e `PUBLIC_MOBILE_*_WEBVIEW_URL` com dominios/subdominios HTTPS de producao.

Secrets obrigatorios:
- `VPS_PUBLIC_HOST`
- `VPS_DB_PASSWORD`
- `JWT_SECRET_KEY`
- `SEED_DEFAULT_PASSWORD`
- `VPS_SSH_KEY`

Recomendacao de seguranca:
- criar environments do GitHub Actions `development` e `production`;
- cadastrar os mesmos nomes de secrets nos dois environments;
- restringir aprovacoes/permissoes do environment `production`.

Secrets opcionais:
- `VPS_APP_ENVIRONMENT` (default `Production`)
- `VPS_DB_HOST` (default `mssql`)
- `VPS_MSSQL_CONTAINER_NAME` (default `mssql`)
- `VPS_MSSQL_HOST_ALIAS` (default `mssql`)
- `CPMFULL_CHATWOOT_ENABLED`
- `CPMFULL_CHATWOOT_BASE_URL`
- `CPMFULL_CHATWOOT_API_ACCESS_TOKEN`
- `CPMFULL_CHATWOOT_ACCOUNT_ID`
- `CPMFULL_CHATWOOT_CLIENTS_INBOX_ID`
- `CPMFULL_CHATWOOT_PROVIDERS_INBOX_ID`
- `CPMFULL_CHATWOOT_WEBHOOK_SECRET`
- `CPMFULL_CHATWOOT_REQUEST_TIMEOUT_SECONDS`
- `CPMFULL_CHATWOOT_MAX_RETRY_ATTEMPTS`
- `CPMFULL_CHATWOOT_RETRY_BASE_DELAY_MS`
- `CPMFULL_CHATWOOT_RETRY_WORKER_ENABLED`
- `CPMFULL_CHATWOOT_RETRY_WORKER_INTERVAL_SECONDS`
- `CPMFULL_CHATWOOT_RETRY_WORKER_BATCH_SIZE`
- `CPMFULL_CHATWOOT_SYNC_QUEUE_MAX_ATTEMPTS`
- `FIREBASE_SERVICE_ACCOUNT_PATH`
- `PUBLIC_LANDING_URL`
- `PUBLIC_API_URL`
- `PUBLIC_ADMIN_URL`
- `PUBLIC_CLIENT_URL`
- `PUBLIC_PROVIDER_URL`
- `PUBLIC_MOBILE_CLIENT_WEBVIEW_URL`
- `PUBLIC_MOBILE_PROVIDER_WEBVIEW_URL`
- `PUBLIC_MOBILE_ADMIN_WEBVIEW_URL`
- `ENFORCE_API_HTTPS_REDIRECTION`

Comportamento do workflow:
- o pipeline respeita os valores de `PUBLIC_*_URL` e `PUBLIC_MOBILE_*_WEBVIEW_URL` definidos no environment (`development`/`production`);
- se algum valor publico estiver ausente, aplica fallback para `http://<VPS_PUBLIC_HOST>:<porta-do-ambiente>`;
- em `main/master`, os `PUBLIC_*_URL` e `PUBLIC_MOBILE_*_WEBVIEW_URL` devem apontar para os dominios/subdominios HTTPS de producao.
- no `health-web-cpmfull`, o workflow passa a preferir `PUBLIC_LANDING_URL` quando a branch for `dev-local` e esse secret estiver preenchido; sem isso, continua o fallback para `http://<VPS_PUBLIC_HOST>:6088`.

Observacoes sobre metadados de APK e push de resumo:
- a publicacao de metadados dos APKs (`/api/internal/deploy/apk-publication`) e enviada pelo runner self-hosted para `http://127.0.0.1:<API_PORT>` na propria VPS;
- o push de release dos APKs (`/api/internal/deploy/apk-release`) tambem usa endpoint interno `http://127.0.0.1:<API_PORT>` para evitar falha de conectividade externa em ambiente PROD com bind local;
- o push de resumo do workflow (`/api/internal/deploy/admin-summary`) e enviado pelo runner self-hosted para `http://127.0.0.1:<API_PORT>` na propria VPS;
- quando API/webhook/token nao estiverem disponiveis, o workflow registra `notice` (nao `warning`) por ser etapa opcional de notificacao.
- o upload dos APKs para o fileserver passou a ocorrer localmente no runner self-hosted (na propria VPS), via `docker cp` para o container `filebrowser`; nao depende mais de SSH externo (`porta 22`) a partir de runner hospedado.
- o ajuste de ownership/permissao dos APKs em `/srv/apks` e executado com `docker exec --user 0` (modo estrito, sem `|| true`), evitando falso positivo e falhando apenas quando houver erro real de permissao/filesystem.
- os APKs agora sao segregados por ambiente no fileserver:
  - `dev-local` publica em `/srv/apks/hml` (`/files/apks/hml/...`);
  - `main/master` publica em `/srv/apks/prd` (`/files/apks/prd/...`).
- a view `AdminApplications` do portal admin resolve automaticamente os links para o canal do ambiente ativo (`hml` ou `prd`) usando `DEPLOY_PROFILE`, mesmo quando `Fileserver:ApkBaseUrl` estiver legado sem sufixo de ambiente.
- os builds de APK (`client`, `provider`, `admin`) executam em paralelo apos os healthchecks de deploy e usam cache Gradle no GitHub Actions para reduzir tempo total de pipeline.

## 9) Operacao por projeto

Status:

```bash
docker compose -p cpm-prd-cpmfull -f Backend/docker-compose.vps.web-cpmfull.yml --env-file Backend/.env.vps ps
docker compose -p cpm-prd-api -f Backend/docker-compose.vps.api.yml --env-file Backend/.env.vps ps
docker compose -p cpm-prd-admin -f Backend/docker-compose.vps.web-admin.yml --env-file Backend/.env.vps ps
docker compose -p cpm-prd-cliente -f Backend/docker-compose.vps.web-client.yml --env-file Backend/.env.vps ps
docker compose -p cpm-prd-prestador -f Backend/docker-compose.vps.web-provider.yml --env-file Backend/.env.vps ps
docker compose -p cpm-prd-app-cliente -f Backend/docker-compose.vps.mobile-webview-client.yml --env-file Backend/.env.vps ps
docker compose -p cpm-prd-app-prestador -f Backend/docker-compose.vps.mobile-webview-provider.yml --env-file Backend/.env.vps ps
docker compose -p cpm-prd-app-admin -f Backend/docker-compose.vps.mobile-webview-admin.yml --env-file Backend/.env.vps ps

# Para DEV (branch dev-local), trocar para:
docker compose -p cpm-hml-api -f Backend/docker-compose.vps.api.yml --env-file Backend/.env.vps ps
```

Parar/iniciar individual:

```bash
docker compose -p cpm-prd-cpmfull -f Backend/docker-compose.vps.web-cpmfull.yml --env-file Backend/.env.vps stop
docker compose -p cpm-prd-cpmfull -f Backend/docker-compose.vps.web-cpmfull.yml --env-file Backend/.env.vps start
```

Logs:

```bash
docker logs -f cpm-prd-cpmfull
docker logs -f cpm-prd-api
docker logs -f cpm-prd-admin
docker logs -f cpm-prd-cliente
docker logs -f cpm-prd-prestador
docker logs -f cpm-prd-app-cliente
docker logs -f cpm-prd-app-prestador
docker logs -f cpm-prd-app-admin

# Stack DEV (branch dev-local)
docker logs -f cpm-hml-cpmfull
docker logs -f cpm-hml-api
docker logs -f cpm-hml-admin
docker logs -f cpm-hml-cliente
docker logs -f cpm-hml-prestador
docker logs -f cpm-hml-app-cliente
docker logs -f cpm-hml-app-prestador
docker logs -f cpm-hml-app-admin
```

## 10) Troubleshooting rapido (DEV por IP:porta)

Quando `http://<IP>:6088|6151|6069|6140|6193` der timeout:

1. Validar se a stack DEV subiu no projeto compose correto:

```bash
docker compose -p cpm-hml-api -f Backend/docker-compose.vps.api.yml --env-file Backend/.env.vps ps
docker compose -p cpm-hml-admin -f Backend/docker-compose.vps.web-admin.yml --env-file Backend/.env.vps ps
docker compose -p cpm-hml-cliente -f Backend/docker-compose.vps.web-client.yml --env-file Backend/.env.vps ps
docker compose -p cpm-hml-prestador -f Backend/docker-compose.vps.web-provider.yml --env-file Backend/.env.vps ps
docker compose -p cpm-hml-cpmfull -f Backend/docker-compose.vps.web-cpmfull.yml --env-file Backend/.env.vps ps
```

2. Validar bind de portas no host:

```bash
sudo ss -ltnp | egrep ':(6193|6151|6069|6140|6088)\b'
```

3. Testar localmente na propria VPS:

```bash
curl -i http://127.0.0.1:6193/health
curl -I http://127.0.0.1:6151/Account/Login
curl -I http://127.0.0.1:6069/Account/Login
curl -I http://127.0.0.1:6140/Account/Login
curl -i http://127.0.0.1:6088/health
```

4. Se os containers estiverem em `Restarting/Exited`, abrir logs:

```bash
docker logs --tail 200 cpm-hml-api
docker logs --tail 200 cpm-hml-admin
docker logs --tail 200 cpm-hml-cliente
docker logs --tail 200 cpm-hml-prestador
docker logs --tail 200 cpm-hml-cpmfull
```

5. Se a API cair com `PendingModelChangesWarning` no `cpm-hml-api`:

```bash
# Confirmar perfil de deploy
grep -E '^(DEPLOY_PROFILE|APP_ENVIRONMENT|DB_NAME)=' Backend/.env.vps

# Reaplicar deploy da API no perfil DEV
MSSQL_CONTAINER_NAME=mssql-mssql-1 MSSQL_HOST_ALIAS=mssql scripts/deploy/vps-deploy-service.sh "$PWD" api
```

Observacao:
- no perfil `development`, a API ignora apenas o warning `RelationalEventId.PendingModelChangesWarning` para nao interromper o boot em ambiente DEV; em `production`, o comportamento padrao (estrito) permanece.

Preflight obrigatorio antes de promover para `main/master`:

```bash
dotnet ef migrations has-pending-model-changes \
  --project Backend/src/ConsertaPraMim.Infrastructure \
  --startup-project Backend/src/ConsertaPraMim.API \
  --context ConsertaPraMimDbContext
```

Se houver pendencia de modelo, nao promover para `main/master` sem resolver migration/snapshot no mesmo ciclo (incluindo changelog e manual operacional atualizados).

6. Se Web Admin/Cliente/Prestador estiverem `Up` mas sem responder na porta publicada:

```bash
docker inspect cpm-hml-admin --format '{{range .Config.Env}}{{println .}}{{end}}' | grep -E '^(ASPNETCORE_URLS|URLS|ADMIN_PORT)='
docker inspect cpm-hml-cliente --format '{{range .Config.Env}}{{println .}}{{end}}' | grep -E '^(ASPNETCORE_URLS|URLS|CLIENT_PORT)='
docker inspect cpm-hml-prestador --format '{{range .Config.Env}}{{println .}}{{end}}' | grep -E '^(ASPNETCORE_URLS|URLS|PROVIDER_PORT)='
```
