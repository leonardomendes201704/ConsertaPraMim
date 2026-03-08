# Manual QA/Operacao - Landing Page Publica

## Escopo

Este manual cobre a landing publica `ConsertaPraMim.Web.Landing`, publicada em `https://www.consertapramim.com`, e o redirect do dominio raiz `https://consertapramim.com`.

## Componentes envolvidos

- projeto: `Backend/src/ConsertaPraMim.Web.Landing`
- compose: `Backend/docker-compose.vps.web-landing.yml`
- dockerfile: `Backend/docker/vps/Dockerfile.web.landing`
- proxy: `Backend/docker/vps/nginx.portals.https.conf.example`
- deploy: `scripts/deploy/vps-deploy.sh`, `scripts/deploy/vps-deploy-service.sh`
- workflow: `.github/workflows/deploy-vps.yml`

## Configuracao minima

No `Backend/.env.vps`:

```env
PUBLIC_LANDING_URL=https://www.consertapramim.com
LANDING_PORT=5088
PUBLIC_CLIENT_URL=https://cliente.consertapramim.com
PUBLIC_PROVIDER_URL=https://prestador.consertapramim.com
PUBLIC_ADMIN_URL=https://admin.consertapramim.com
PUBLIC_API_URL=https://api.consertapramim.com
```

## Checklist de deploy

1. DNS `consertapramim.com` e `www.consertapramim.com` apontando para a VPS.
2. Template Nginx com `__ROOT_DOMAIN__` e `__WWW_DOMAIN__` substituidos.
3. Certificado emitido para raiz e `www`.
4. Deploy do container:

```bash
cd ~/ConsertaPraMimWeb
MSSQL_CONTAINER_NAME=mssql-mssql-1 MSSQL_HOST_ALIAS=mssql scripts/deploy/vps-deploy-service.sh "$PWD" web-landing
```

## Smoke test tecnico

Validar na VPS:

```bash
curl -I http://127.0.0.1:5088/health
curl -I https://www.consertapramim.com
curl -I https://www.consertapramim.com/health
curl -I https://consertapramim.com
```

Esperado:
- `127.0.0.1:5088/health` -> `200`
- `https://www.consertapramim.com` -> `200`
- `https://www.consertapramim.com/health` -> `200`
- `https://consertapramim.com` -> `301` ou `308` para `https://www.consertapramim.com`

## Checklist funcional

1. Abrir `https://www.consertapramim.com` em desktop.
2. Abrir `https://www.consertapramim.com` em viewport mobile.
3. Confirmar que o menu mobile abre/fecha.
4. Validar CTA:
   - `Portal Cliente`
   - `Portal Prestador`
   - `Portal Admin`
   - `Swagger API`
5. Confirmar ausencia de erros de `Mixed Content` e `Content-Security-Policy` no console.
6. Validar `https://www.consertapramim.com/robots.txt`.
7. Validar `https://www.consertapramim.com/sitemap.xml`.

## Troubleshooting

### `502 Bad Gateway` no `www`

Verificar:

```bash
docker logs --tail 200 cpm-web-landing
docker compose -f Backend/docker-compose.vps.web-landing.yml --env-file Backend/.env.vps ps
ss -ltnp | grep 5088
```

### Redirect do raiz nao funciona

Verificar o bloco `server_name consertapramim.com` no Nginx:

```bash
sudo nginx -T | grep -n "server_name consertapramim.com" -A 10 -B 5
```

### Landing abriu, mas os links estao errados

Verificar variaveis de ambiente do container:

```bash
docker inspect cpm-web-landing --format '{{range .Config.Env}}{{println .}}{{end}}' | grep -E '^(LandingSite__)'
```

Esperado:
- `LandingSite__CanonicalUrl=https://www.consertapramim.com`
- `LandingSite__ClientPortalUrl=https://cliente.consertapramim.com`
- `LandingSite__ProviderPortalUrl=https://prestador.consertapramim.com`
- `LandingSite__AdminPortalUrl=https://admin.consertapramim.com`
- `LandingSite__ApiSwaggerUrl=https://api.consertapramim.com/swagger`
