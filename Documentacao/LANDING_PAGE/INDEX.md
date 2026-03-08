# Landing Page

## Objetivo

Centralizar a documentacao da landing publica publicada em `https://www.consertapramim.com`, incluindo backlog, operacao, QA e topologia de deploy.

## Conteudo

- Manual QA/Operacao: `Documentacao/LANDING_PAGE/MANUAL_QA_OPERACAO_LANDING.md`
- Epic: `Documentacao/LANDING_PAGE/EPICS/EPIC-001-landing-page-publica-www-e-deploy-vps.md`
- Epic: `Documentacao/LANDING_PAGE/EPICS/EPIC-002-captacao-leads-publicos-landing.md`
- Story concluida: `Documentacao/LANDING_PAGE/STORIES/DONE/ST-001-landing-page-publica-www.md`
- Story concluida: `Documentacao/LANDING_PAGE/STORIES/DONE/ST-002-captura-leads-publicos-landing.md`
- Diagrama Mermaid: `Documentacao/DIAGRAMAS/LANDING_PAGE/ST-001-landing-page-publica-www/fluxo-publicacao-landing-vps.mmd`
- Diagrama Mermaid: `Documentacao/DIAGRAMAS/LANDING_PAGE/ST-002-captura-leads-publicos-landing/fluxo-captura-leads-landing.mmd`

## Escopo atual

- projeto `ConsertaPraMim.Web.Landing` publicado em `www` com deploy integrado na VPS;
- landing publica com healthcheck, `robots.txt`, `sitemap.xml` e links para cliente/prestador/admin/swagger;
- captura de leads comerciais de cliente e prestador diretamente na landing, com persistencia centralizada na API.
