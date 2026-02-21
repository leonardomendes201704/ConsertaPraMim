# Deploy das 4 aplicacoes na VPS (Docker)

Este guia sobe:
- API (`ConsertaPraMim.API`)
- Portal Admin (`ConsertaPraMim.Web.Admin`)
- Portal Cliente (`ConsertaPraMim.Web.Client`)
- Portal Prestador (`ConsertaPraMim.Web.Provider`)

## 1) Preparacao na VPS (primeira vez)

No servidor:

```bash
cd ~
git clone <URL_DO_REPO> ConsertaPraMimWeb
cd ConsertaPraMimWeb
cp Backend/.env.vps.example Backend/.env.vps
nano Backend/.env.vps
```

Preencha pelo menos:
- `VPS_PUBLIC_HOST`
- `DB_PASSWORD`
- `JWT_SECRET_KEY`

Observacao: `DB_PASSWORD` pode conter `;` (ex.: senhas fortes). O `docker-compose.vps.yml` ja envolve o valor de senha corretamente na connection string.

Depois:

```bash
chmod +x scripts/deploy/vps-deploy.sh
MSSQL_CONTAINER_NAME=mssql scripts/deploy/vps-deploy.sh
```

## 2) Rede Docker com SQL existente

O deploy usa a rede Docker `conserta_net` e espera o SQL no container `mssql`.

O script `scripts/deploy/vps-deploy.sh` ja faz:
- criar rede `conserta_net` (se nao existir)
- conectar o container SQL nessa rede (se necessario)

## 3) Portas expostas

- API: `5193`
- Admin: `5151`
- Cliente: `5069`
- Prestador: `5140`

Se quiser alterar, mude no `Backend/.env.vps`.

## 4) Deploy manual apos update

Na VPS:

```bash
cd ~/ConsertaPraMimWeb
MSSQL_CONTAINER_NAME=mssql scripts/deploy/vps-deploy.sh
```

## 5) Deploy automatico no git push (GitHub Actions)

Existe workflow em:
- `.github/workflows/deploy-vps.yml`

Configure estes secrets no GitHub:
- `VPS_HOST`
- `VPS_USER`
- `VPS_SSH_KEY`
- `VPS_SSH_PORT` (opcional, default 22)
- `VPS_REPO_DIR` (opcional, default `~/ConsertaPraMimWeb`)
- `VPS_PUBLIC_HOST`
- `VPS_DB_PASSWORD`
- `JWT_SECRET_KEY`
- `SEED_DEFAULT_PASSWORD`
- `VPS_MSSQL_CONTAINER_NAME` (opcional, default `mssql`)
- `FIREBASE_SERVICE_ACCOUNT_PATH` (opcional)

Depois disso, `push` em `master` ou `main` dispara deploy automatico.

## 6) Comandos uteis

Status:

```bash
cd ~/ConsertaPraMimWeb
docker compose -f Backend/docker-compose.vps.yml --env-file Backend/.env.vps ps
```

Logs da API:

```bash
docker logs -f cpm-api
```

Restart rapido:

```bash
docker compose -f Backend/docker-compose.vps.yml --env-file Backend/.env.vps restart
```

## Observacoes

- O compose foi configurado em `APP_ENVIRONMENT=Development` para evitar redirecionamento HTTPS forcado sem reverse proxy/TLS.
- Para producao com HTTPS, o ideal e colocar Nginx/Caddy + certificados e entao migrar para `Production`.
