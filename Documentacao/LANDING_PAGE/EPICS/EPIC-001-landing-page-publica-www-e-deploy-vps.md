# EPIC-001 - Landing page publica em `www` com deploy integrado na VPS

## Contexto

O dominio principal `www.consertapramim.com` precisava deixar de apontar para um portal interno e passar a servir um site institucional publico, com topologia separada dos portais de cliente, prestador, admin e da API.

## Objetivo

Entregar uma landing publica versionada na mesma solution, pronta para deploy via Docker/Nginx/Certbot, com:
- healthcheck proprio;
- CTA para os portais existentes;
- documentacao operacional completa;
- suporte ao redirect do dominio raiz `consertapramim.com` para `www`.

## Entregas

- novo projeto `ConsertaPraMim.Web.Landing`;
- Dockerfile e compose dedicados;
- integracao com `scripts/deploy/*` e `.github/workflows/deploy-vps.yml`;
- template Nginx com `www` + redirect do raiz;
- manual QA/Operacao, story concluida e diagrama Mermaid.
