# Deploy na VPS (projetos Docker separados)

Este guia publica 4 projetos Docker independentes:
- API (`backend-api`)
- Portal Admin (`backend-web-admin`)
- Portal Cliente (`backend-web-client`)
- Portal Prestador (`backend-web-provider`)

Arquivos compose:
- `Backend/docker-compose.vps.api.yml`
- `Backend/docker-compose.vps.web-admin.yml`
- `Backend/docker-compose.vps.web-client.yml`
- `Backend/docker-compose.vps.web-provider.yml`

## 1) Preparacao na VPS (primeira vez)

```bash
cd ~
git clone <URL_DO_REPO> ConsertaPraMimWeb
cd ConsertaPraMimWeb
cp Backend/.env.vps.example Backend/.env.vps
nano Backend/.env.vps
chmod +x scripts/deploy/vps-deploy.sh scripts/deploy/vps-deploy-service.sh
```

Preencha no `Backend/.env.vps` pelo menos:
- `APP_ENVIRONMENT` (`Production` na VPS)
- `VPS_PUBLIC_HOST`
- `DB_PASSWORD`
- `DB_HOST` (normalmente `mssql`)
- `JWT_SECRET_KEY`
- `SEED_DEFAULT_PASSWORD`

## 2) Deploy manual completo (4 projetos)

```bash
cd ~/ConsertaPraMimWeb
MSSQL_CONTAINER_NAME=mssql-mssql-1 MSSQL_HOST_ALIAS=mssql scripts/deploy/vps-deploy.sh
```

## 3) Deploy manual de um projeto especifico

```bash
cd ~/ConsertaPraMimWeb
MSSQL_CONTAINER_NAME=mssql-mssql-1 MSSQL_HOST_ALIAS=mssql scripts/deploy/vps-deploy-service.sh "$PWD" api
MSSQL_CONTAINER_NAME=mssql-mssql-1 MSSQL_HOST_ALIAS=mssql scripts/deploy/vps-deploy-service.sh "$PWD" web-admin
MSSQL_CONTAINER_NAME=mssql-mssql-1 MSSQL_HOST_ALIAS=mssql scripts/deploy/vps-deploy-service.sh "$PWD" web-client
MSSQL_CONTAINER_NAME=mssql-mssql-1 MSSQL_HOST_ALIAS=mssql scripts/deploy/vps-deploy-service.sh "$PWD" web-provider
```

## 4) CI/CD (runner) com deploy seletivo por alteracao

Workflow: `.github/workflows/deploy-vps.yml`

Comportamento:
- `push` para `main/master`: deploya apenas o(s) projeto(s) alterado(s)
- `pull_request` para `main/master`: deploya apenas o(s) projeto(s) alterado(s) (quando PR do mesmo repositorio)
- `workflow_dispatch`: deploya todos os projetos
- se alterar arquivos globais de infra/deploy, deploya todos

Secrets necessarios:
- `VPS_PUBLIC_HOST`
- `VPS_DB_PASSWORD`
- `JWT_SECRET_KEY`
- `SEED_DEFAULT_PASSWORD`

Secrets opcionais:
- `VPS_APP_ENVIRONMENT` (default `Development`)
- `VPS_DB_HOST` (default `mssql`)
- `VPS_MSSQL_CONTAINER_NAME` (default `mssql`)
- `VPS_MSSQL_HOST_ALIAS` (default `mssql`)
- `FIREBASE_SERVICE_ACCOUNT_PATH`

## 5) Operacao por projeto

Status:

```bash
docker compose -f Backend/docker-compose.vps.api.yml --env-file Backend/.env.vps ps
docker compose -f Backend/docker-compose.vps.web-admin.yml --env-file Backend/.env.vps ps
docker compose -f Backend/docker-compose.vps.web-client.yml --env-file Backend/.env.vps ps
docker compose -f Backend/docker-compose.vps.web-provider.yml --env-file Backend/.env.vps ps
```

Parar/iniciar individual:

```bash
docker compose -f Backend/docker-compose.vps.api.yml --env-file Backend/.env.vps stop
docker compose -f Backend/docker-compose.vps.api.yml --env-file Backend/.env.vps start
```

Logs:

```bash
docker logs -f cpm-api
docker logs -f cpm-web-admin
docker logs -f cpm-web-client
docker logs -f cpm-web-provider
```
