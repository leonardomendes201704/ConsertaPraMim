# ST-072 - Deploy branch-aware `dev-local` e `main` na mesma VPS

## Como
DevOps / engenharia de plataforma

## Eu quero
que `dev-local` publique um ambiente DEV por `IP:porta` e `main/master` publique PROD por dominio/subdominios

## Para
validar funcionalidades em ambiente paralelo sem interromper producao e sem risco de conflito na infraestrutura compartilhada.

## Criterios de aceite

1. O workflow `.github/workflows/deploy-vps.yml` dispara para `dev-local`, `main` e `master`.
2. Em `dev-local`, os servicos sobem em portas DEV (`6193`, `6088`, `6151`, `6069`, `6140`, `6181-6183`) com bind externo (`0.0.0.0`).
3. Em `main/master`, os servicos mantem portas PROD (`5193`, `5088`, `5151`, `5069`, `5140`, `5181-5183`) com bind local (`127.0.0.1`) para operacao atras de Nginx.
4. Containers e volumes de DEV e PROD coexistem sem conflito via prefixos distintos.
5. O script `vps-deploy-service.sh` resolve o nome do container por prefixo do ambiente.
6. O runbook `Backend/DEPLOY_VPS.md` documenta o fluxo completo DEV/PROD e requisitos de secrets/environments.

## Tasks

- [x] habilitar trigger da branch `dev-local` no workflow de deploy;
- [x] implementar profile branch-aware com portas, bind host e prefixos por ambiente;
- [x] tornar escrita de `.env.vps` branch-aware no pipeline (URLs e HTTPS redirection);
- [x] parametrizar compose files por `CONTAINER_PREFIX`, `VOLUME_PREFIX`, `BIND_HOST` e `INTERNAL_API_URL`;
- [x] ajustar script de deploy para nome dinamico de container;
- [x] atualizar manual operacional de VPS com matriz DEV/PROD.

## Ajustes pos-deploy (2026-03-09)

- [x] isolar projeto `docker compose` por ambiente e servico (`-p <CONTAINER_PREFIX>-<servico>`) para evitar colisao e remocao cruzada entre jobs paralelos;
- [x] ajustar healthchecks do workflow para usar `VPS_PUBLIC_HOST` no perfil `development`;
- [x] adicionar diagnostico automatico (`docker ps` + `docker logs`) quando healthcheck falhar;
- [x] atualizar runbook com comandos de troubleshooting rapido para timeout em portas DEV.
- [x] forcar `URLS` + `ASPNETCORE_URLS` nos compose files web para evitar bind interno em portas legadas de `appsettings.Development` no perfil `dev-local`;
- [x] evitar crash da API em `dev-local` por `PendingModelChangesWarning`, mantendo o comportamento estrito em `production`.
