# EPIC-031 - Deploy dual stack DEV/PROD na mesma VPS

## Objetivo

Permitir que a branch `dev-local` publique um ambiente DEV isolado por `IP:porta`, enquanto `main/master` continua publicando PROD por dominio/subdominios, sem conflito de containers, volumes, portas ou banco.

## Resultado esperado

- deploy automatico por branch no workflow `deploy-vps`;
- stack DEV e PROD coexistindo na mesma VPS com isolamento tecnico;
- DEV publicado em portas dedicadas e bind externo controlado;
- PROD mantido atras de Nginx/HTTPS com bind local;
- rastreabilidade operacional no runbook e changelog.

## Escopo

- ajuste de branch trigger e profile no GitHub Actions;
- isolamento por `CONTAINER_PREFIX`, `VOLUME_PREFIX`, `DB_NAME` e portas;
- escrita de `.env.vps` branch-aware no pipeline;
- parametrizacao dos compose files por prefixo/bind host;
- ajuste do script `vps-deploy-service.sh` para nome dinamico de container;
- atualizacao do `DEPLOY_VPS.md` com matriz DEV/PROD.

## Fora de escopo

- criar segunda VPS;
- promover/replicar banco automaticamente entre DEV e PROD;
- alterar dominios publicos de producao ja ativos.

## Historias relacionadas

- ST-072 - Pipeline branch-aware para deploy DEV/PROD na mesma VPS
- ST-073 - Otimizacao do pipeline de APK com segregacao HML/PRD no fileserver
