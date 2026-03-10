# Deploy na VPS com HTTPS

Este guia publica 8 projetos Docker independentes:
- Landing publica (`backend-web-landing`)
- API (`backend-api`)
- Portal Admin (`backend-web-admin`)
- Portal Cliente (`backend-web-client`)
- Portal Prestador (`backend-web-provider`)
- Mobile WebView Cliente (`backend-mobile-webview-client`)
- Mobile WebView Prestador (`backend-mobile-webview-provider`)
- Mobile WebView Admin (`backend-mobile-webview-admin`)

Arquivos compose:
- `Backend/docker-compose.vps.web-landing.yml`
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

Em `main/master` (PROD), os portais web, a landing e a API devem ficar atras de Nginx em `80/443`, com os containers publicados em `127.0.0.1`.

Em `dev-local` (DEV), a mesma stack sobe na VPS por `IP:porta`, com bind `0.0.0.0` e portas dedicadas para nao conflitar com producao.

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
- a landing publica e independente da API, com `healthcheck` proprio em `/health`;
- os `healthchecks` do workflow validam os servicos pela malha local (`127.0.0.1`) na propria VPS.

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
- `CONTAINER_PREFIX` (`cpm` em prod / `cpm-dev` em dev)
- `VOLUME_PREFIX` (`cpm` em prod / `cpm_dev` em dev)
- `VPS_PUBLIC_HOST` (host ou IP cru, sem `http://` ou `https://`)
- `INTERNAL_API_URL`
- `PUBLIC_LANDING_URL`
- `PUBLIC_API_URL`
- `PUBLIC_ADMIN_URL`
- `PUBLIC_CLIENT_URL`
- `PUBLIC_PROVIDER_URL`
- `DB_PASSWORD`
- `DB_HOST` (normalmente `mssql`)
- `JWT_SECRET_KEY`
- `SEED_DEFAULT_PASSWORD`

Exemplo minimo (PROD):

```env
DEPLOY_PROFILE=production
APP_ENVIRONMENT=Production
BIND_HOST=127.0.0.1
CONTAINER_PREFIX=cpm
VOLUME_PREFIX=cpm
VPS_PUBLIC_HOST=SEU_IP_OU_HOST_DA_VPS

INTERNAL_API_URL=http://cpm-api:8080
PUBLIC_LANDING_URL=https://www.consertapramim.com
PUBLIC_API_URL=https://api.consertapramim.com
PUBLIC_ADMIN_URL=https://admin.consertapramim.com
PUBLIC_CLIENT_URL=https://cliente.consertapramim.com
PUBLIC_PROVIDER_URL=https://prestador.consertapramim.com
ENFORCE_API_HTTPS_REDIRECTION=true

API_PORT=5193
LANDING_PORT=5088
ADMIN_PORT=5151
CLIENT_PORT=5069
PROVIDER_PORT=5140

DB_NAME=ConsertaPraMimDb
DB_USER=sa
DB_PASSWORD=ALTERAR_AQUI
DB_HOST=mssql

JWT_SECRET_KEY=ALTERAR_PARA_UMA_CHAVE_BEM_FORTE_COM_32+_CARACTERES
SEED_DEFAULT_PASSWORD=ALTERAR_AQUI
```

Exemplo minimo (DEV na mesma VPS):

```env
DEPLOY_PROFILE=development
APP_ENVIRONMENT=Development
BIND_HOST=0.0.0.0
CONTAINER_PREFIX=cpm-dev
VOLUME_PREFIX=cpm_dev
VPS_PUBLIC_HOST=187.77.48.150

INTERNAL_API_URL=http://cpm-dev-api:8080
PUBLIC_LANDING_URL=http://187.77.48.150:6088
PUBLIC_API_URL=http://187.77.48.150:6193
PUBLIC_ADMIN_URL=http://187.77.48.150:6151
PUBLIC_CLIENT_URL=http://187.77.48.150:6069
PUBLIC_PROVIDER_URL=http://187.77.48.150:6140
ENFORCE_API_HTTPS_REDIRECTION=false

API_PORT=6193
LANDING_PORT=6088
ADMIN_PORT=6151
CLIENT_PORT=6069
PROVIDER_PORT=6140

DB_NAME=ConsertaPraMimDbDev
DB_USER=sa
DB_PASSWORD=ALTERAR_AQUI
DB_HOST=mssql

JWT_SECRET_KEY=ALTERAR_PARA_UMA_CHAVE_BEM_FORTE_COM_32+_CARACTERES
SEED_DEFAULT_PASSWORD=ALTERAR_AQUI
```

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
MSSQL_CONTAINER_NAME=mssql-mssql-1 MSSQL_HOST_ALIAS=mssql scripts/deploy/vps-deploy-service.sh "$PWD" web-landing
MSSQL_CONTAINER_NAME=mssql-mssql-1 MSSQL_HOST_ALIAS=mssql scripts/deploy/vps-deploy-service.sh "$PWD" web-admin
MSSQL_CONTAINER_NAME=mssql-mssql-1 MSSQL_HOST_ALIAS=mssql scripts/deploy/vps-deploy-service.sh "$PWD" web-client
MSSQL_CONTAINER_NAME=mssql-mssql-1 MSSQL_HOST_ALIAS=mssql scripts/deploy/vps-deploy-service.sh "$PWD" web-provider
MSSQL_CONTAINER_NAME=mssql-mssql-1 MSSQL_HOST_ALIAS=mssql scripts/deploy/vps-deploy-service.sh "$PWD" mobile-webview-client
MSSQL_CONTAINER_NAME=mssql-mssql-1 MSSQL_HOST_ALIAS=mssql scripts/deploy/vps-deploy-service.sh "$PWD" mobile-webview-provider
MSSQL_CONTAINER_NAME=mssql-mssql-1 MSSQL_HOST_ALIAS=mssql scripts/deploy/vps-deploy-service.sh "$PWD" mobile-webview-admin
```

Observacao operacional:
- o script `scripts/deploy/vps-deploy-service.sh` faz `build` antes do `up` e remove o container alvo do ambiente corrente (`<CONTAINER_PREFIX>-api`, `<CONTAINER_PREFIX>-web-landing`, etc.) para evitar conflito de `container_name`;
- em `production`, `web-landing`, `api`, `web-admin`, `web-client` e `web-provider` ficam publicados em `127.0.0.1`, por isso o acesso externo precisa passar pelo Nginx;
- em `development`, os mesmos servicos podem ser publicados por `IP:porta` (bind `0.0.0.0`) para validacao rapida;
- os portais usam `INTERNAL_API_URL` para chamadas server-side na rede Docker e `PUBLIC_API_URL` para URLs injetadas no browser;
- a landing usa `PUBLIC_LANDING_URL`, `PUBLIC_CLIENT_URL`, `PUBLIC_PROVIDER_URL`, `PUBLIC_ADMIN_URL` e `PUBLIC_API_URL` para montar CTA, canonical e links publicos.

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
docker inspect cpm-web-admin --format '{{range .Config.Env}}{{println .}}{{end}}' | grep -E '^(ApiBaseUrl|BrowserApiBaseUrl)='
docker inspect cpm-web-client --format '{{range .Config.Env}}{{println .}}{{end}}' | grep -E '^(ApiBaseUrl|BrowserApiBaseUrl)='
docker inspect cpm-web-provider --format '{{range .Config.Env}}{{println .}}{{end}}' | grep -E '^(ApiBaseUrl|BrowserApiBaseUrl)='
docker run --rm --network conserta_net curlimages/curl:8.12.1 -I http://cpm-api:8080/health
# em DEV (CONTAINER_PREFIX=cpm-dev):
# docker run --rm --network conserta_net curlimages/curl:8.12.1 -I http://cpm-dev-api:8080/health
docker run --rm --network conserta_net curlimages/curl:8.12.1 -I https://api.consertapramim.com/health
```

Esperado:
- `ApiBaseUrl=http://<CONTAINER_PREFIX>-api:8080` (ex.: `cpm-api` em prod, `cpm-dev-api` em dev)
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
- `push` para `dev-local`: deploya stack `DEV` na mesma VPS, publicada por `IP:porta`
- `push` para `main/master`: deploya stack `PROD` na mesma VPS, publicada por dominio/subdominios
- `workflow_dispatch`: deploya todos os projetos
- se alterar arquivos globais de infra/deploy, deploya todos

Isolamento automatico por branch (workflow):

- `dev-local`:
  `DEPLOY_PROFILE=development`,
  `BIND_HOST=0.0.0.0`,
  `CONTAINER_PREFIX=cpm-dev`,
  `VOLUME_PREFIX=cpm_dev`,
  `DB_NAME=ConsertaPraMimDbDev`
- `main/master`:
  `DEPLOY_PROFILE=production`,
  `BIND_HOST=127.0.0.1`,
  `CONTAINER_PREFIX=cpm`,
  `VOLUME_PREFIX=cpm`,
  `DB_NAME=ConsertaPraMimDb`

Portas por ambiente:

- `dev-local`:
  API `6193`, Landing `6088`, Admin `6151`, Cliente `6069`, Prestador `6140`,
  Mobile WebViews `6181/6182/6183`
- `main/master`:
  API `5193`, Landing `5088`, Admin `5151`, Cliente `5069`, Prestador `5140`,
  Mobile WebViews `5181/5182/5183`

Configuracao recomendada no GitHub:

- criar environments `development` e `production`;
- cadastrar os mesmos nomes de secrets em ambos os environments;
- em `development`, manter `VPS_PUBLIC_HOST` com IP da VPS;
- em `production`, manter `PUBLIC_*_URL` com dominios/subdominios HTTPS;
- em `development`, o workflow sobrescreve automaticamente `PUBLIC_*_URL` para `http://<IP>:<PORTA>`.

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
- `FIREBASE_SERVICE_ACCOUNT_PATH`
- `PUBLIC_LANDING_URL`
- `PUBLIC_API_URL`
- `PUBLIC_ADMIN_URL`
- `PUBLIC_CLIENT_URL`
- `PUBLIC_PROVIDER_URL`
- `ENFORCE_API_HTTPS_REDIRECTION`

Em `dev-local`, o workflow sobrescreve `PUBLIC_*_URL` para `http://<VPS_PUBLIC_HOST>:<porta-dev>` automaticamente.
Em `main/master`, os `PUBLIC_*_URL` devem apontar para os dominios/subdominios HTTPS de producao.

Observacoes sobre metadados de APK e push de resumo:
- a publicacao de metadados dos APKs (`/api/internal/deploy/apk-publication`) usa `PUBLIC_API_URL` como base principal; se nao existir, usa fallback para `VPS_PUBLIC_HOST`;
- o push de release dos APKs (`/api/internal/deploy/apk-release`) segue a mesma regra para evitar erro de URI;
- o push de resumo do workflow (`/api/internal/deploy/admin-summary`) e enviado pelo runner self-hosted para `http://127.0.0.1:<API_PORT>` na propria VPS;
- quando API/webhook/token nao estiverem disponiveis, o workflow registra `notice` (nao `warning`) por ser etapa opcional de notificacao.
- o upload dos APKs para o fileserver passou a ocorrer localmente no runner self-hosted (na propria VPS), via `docker cp` para o container `filebrowser`; nao depende mais de SSH externo (`porta 22`) a partir de runner hospedado.
- o ajuste de ownership/permissao dos APKs em `/srv/apks` e executado com `docker exec --user 0` (modo estrito, sem `|| true`), evitando falso positivo e falhando apenas quando houver erro real de permissao/filesystem.

## 9) Operacao por projeto

Status:

```bash
docker compose -f Backend/docker-compose.vps.web-landing.yml --env-file Backend/.env.vps ps
docker compose -f Backend/docker-compose.vps.api.yml --env-file Backend/.env.vps ps
docker compose -f Backend/docker-compose.vps.web-admin.yml --env-file Backend/.env.vps ps
docker compose -f Backend/docker-compose.vps.web-client.yml --env-file Backend/.env.vps ps
docker compose -f Backend/docker-compose.vps.web-provider.yml --env-file Backend/.env.vps ps
docker compose -f Backend/docker-compose.vps.mobile-webview-client.yml --env-file Backend/.env.vps ps
docker compose -f Backend/docker-compose.vps.mobile-webview-provider.yml --env-file Backend/.env.vps ps
docker compose -f Backend/docker-compose.vps.mobile-webview-admin.yml --env-file Backend/.env.vps ps
```

Parar/iniciar individual:

```bash
docker compose -f Backend/docker-compose.vps.web-landing.yml --env-file Backend/.env.vps stop
docker compose -f Backend/docker-compose.vps.web-landing.yml --env-file Backend/.env.vps start
```

Logs:

```bash
docker logs -f cpm-web-landing
docker logs -f cpm-api
docker logs -f cpm-web-admin
docker logs -f cpm-web-client
docker logs -f cpm-web-provider
docker logs -f cpm-mobile-webview-client
docker logs -f cpm-mobile-webview-provider
docker logs -f cpm-mobile-webview-admin

# Stack DEV (branch dev-local)
docker logs -f cpm-dev-web-landing
docker logs -f cpm-dev-api
docker logs -f cpm-dev-web-admin
docker logs -f cpm-dev-web-client
docker logs -f cpm-dev-web-provider
docker logs -f cpm-dev-mobile-webview-client
docker logs -f cpm-dev-mobile-webview-provider
docker logs -f cpm-dev-mobile-webview-admin
```
