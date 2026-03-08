# EPIC-028 - Analytics comportamental da landing no Portal Admin

## Objetivo

Dar ao time administrativo uma visao operacional e analitica propria da landing publica, consolidando visitas, engajamento, conversao, distribuicao geografica e sinais de interesse comercial em telas dedicadas no Portal Admin.

## Resultado esperado

- menu proprio para analytics da landing;
- overview com KPIs de visitas, sessoes, heartbeat, scroll, cliques e leads;
- visualizacao geografica estimada por cidade/UF/pais;
- heatmap agregado fase 1 para cliques em zonas da pagina;
- detalhe por sessao com timeline de eventos e contexto tecnico.

## Escopo

- novas APIs autenticadas de analytics da landing para o admin;
- tela de overview com filtros em drawer/offcanvas;
- tela de detalhe operacional por sessao/visitante;
- reaproveitamento dos dados de `LandingAccessEvents`, `LandingLeads` e novos eventos de telemetria.

## Fora de escopo

- replay completo de sessao;
- dashboards externos de BI;
- automacao de campanhas baseada nos eventos.

## Historias relacionadas

- ST-060 - Dashboard e detalhe operacional de analytics da landing
- Epic correlata (Landing): `Documentacao/LANDING_PAGE/EPICS/EPIC-004-telemetria-comportamental-geoip-landing.md`
