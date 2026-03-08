# Landing Page

## Objetivo

Centralizar a documentacao da landing publica publicada em `https://www.consertapramim.com`, incluindo backlog, operacao, QA e topologia de deploy.

## Conteudo

- Manual QA/Operacao: `Documentacao/LANDING_PAGE/MANUAL_QA_OPERACAO_LANDING.md`
- Epic: `Documentacao/LANDING_PAGE/EPICS/EPIC-001-landing-page-publica-www-e-deploy-vps.md`
- Story concluida: `Documentacao/LANDING_PAGE/STORIES/DONE/ST-001-landing-page-publica-www.md`
- Diagrama Mermaid: `Documentacao/DIAGRAMAS/LANDING_PAGE/ST-001-landing-page-publica-www/fluxo-publicacao-landing-vps.mmd`

## Escopo atual

- novo projeto `ConsertaPraMim.Web.Landing` na solution;
- landing publica com healthcheck, `robots.txt`, `sitemap.xml` e links para cliente/prestador/admin/swagger;
- deploy integrado na VPS com Docker, Nginx, Certbot e workflow seletivo.
