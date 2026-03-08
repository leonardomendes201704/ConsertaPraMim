# Landing Page

## Objetivo

Centralizar a documentacao da landing publica publicada em `https://www.consertapramim.com`, incluindo backlog, operacao, QA e topologia de deploy.

## Conteudo

- Manual QA/Operacao: `Documentacao/LANDING_PAGE/MANUAL_QA_OPERACAO_LANDING.md`
- Epic: `Documentacao/LANDING_PAGE/EPICS/EPIC-001-landing-page-publica-www-e-deploy-vps.md`
- Epic: `Documentacao/LANDING_PAGE/EPICS/EPIC-002-captacao-leads-publicos-landing.md`
- Epic: `Documentacao/LANDING_PAGE/EPICS/EPIC-003-notificacoes-admin-landing-e-cadastro.md`
- Epic: `Documentacao/LANDING_PAGE/EPICS/EPIC-004-telemetria-comportamental-geoip-landing.md`
- Epic correlata (admin): `Documentacao/ADMIN_PORTAL/EPICS/EPIC-027-kpis-landing-no-dashboard-admin.md`
- Epic correlata (admin): `Documentacao/ADMIN_PORTAL/EPICS/EPIC-028-analytics-comportamental-da-landing-no-admin.md`
- Story concluida: `Documentacao/LANDING_PAGE/STORIES/DONE/ST-001-landing-page-publica-www.md`
- Story concluida: `Documentacao/LANDING_PAGE/STORIES/DONE/ST-002-captura-leads-publicos-landing.md`
- Story concluida: `Documentacao/LANDING_PAGE/STORIES/DONE/ST-003-push-admin-para-acesso-publico-e-lead-captado-na-landing.md`
- Story concluida: `Documentacao/LANDING_PAGE/STORIES/DONE/ST-004-telemetria-fase-1-e-geoip-da-landing.md`
- Story correlata (admin): `Documentacao/ADMIN_PORTAL/STORIES/DONE/ST-059-kpis-visitas-cadastros-e-conversao-landing-dashboard.md`
- Story correlata (admin): `Documentacao/ADMIN_PORTAL/STORIES/DONE/ST-060-dashboard-e-detalhe-operacional-de-analytics-da-landing.md`
- Diagrama Mermaid: `Documentacao/DIAGRAMAS/LANDING_PAGE/ST-001-landing-page-publica-www/fluxo-publicacao-landing-vps.mmd`
- Diagrama Mermaid: `Documentacao/DIAGRAMAS/LANDING_PAGE/ST-002-captura-leads-publicos-landing/fluxo-captura-leads-landing.mmd`
- Diagrama Mermaid: `Documentacao/DIAGRAMAS/LANDING_PAGE/ST-003-push-admin-para-acesso-publico-e-lead-captado-na-landing/fluxo-push-admin-landing.mmd`
- Diagrama Mermaid: `Documentacao/DIAGRAMAS/LANDING_PAGE/ST-004-telemetria-fase-1-e-geoip-da-landing/fluxo-telemetria-landing.mmd`
- Diagrama Mermaid correlato: `Documentacao/DIAGRAMAS/ADMIN_PORTAL/ST-059-kpis-visitas-cadastros-e-conversao-landing-dashboard/fluxo-kpis-landing-dashboard-admin.mmd`
- Diagrama Mermaid correlato: `Documentacao/DIAGRAMAS/ADMIN_PORTAL/ST-060-dashboard-e-detalhe-operacional-de-analytics-da-landing/fluxo-analytics-landing-admin.mmd`

## Escopo atual

- projeto `ConsertaPraMim.Web.Landing` publicado em `www` com deploy integrado na VPS;
- landing publica com healthcheck, `robots.txt`, `sitemap.xml`, branding social e captacao comercial por modal;
- captura de leads comerciais de cliente e prestador diretamente na landing, com persistencia centralizada na API;
- notificacoes administrativas para acessos publicos e leads captados pela landing em integracao com o ecossistema admin;
- persistencia historica de acessos com `visitorId` para alimentar os KPIs `Visitas`, `Cadastros Prestador`, `Cadastros Cliente` e `Taxa de Conversão` na home admin;
- telemetria comportamental fase 1 entregue com `sessionId`, GeoIP estimado, heartbeat, scroll milestones, click tracking e heatmap agregado inicial;
- modulo `Analytics Landing` publicado no Portal Admin com filtros em drawer, breakdown geografico, heatmap e detalhe por sessao.
