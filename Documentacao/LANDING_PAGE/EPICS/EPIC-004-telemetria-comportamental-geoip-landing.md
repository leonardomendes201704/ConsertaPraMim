# EPIC-004 - Telemetria comportamental e GeoIP da landing

## Objetivo

Adicionar uma camada propria de analytics comportamental na landing publica para medir engajamento, interesse comercial e origem geografica estimada do trafego, com persistencia historica, configuracao runtime via banco e visibilidade operacional no ecossistema admin.

## Resultado esperado

- registrar acessos da landing com `visitorId`, `sessionId` e localidade estimada por IP;
- capturar telemetria fase 1 no browser com heartbeat, scroll depth e cliques em elementos rastreaveis;
- gerar base para heatmap agregado inicial sem replay de sessao;
- expor metricas publicas/administrativas em APIs internas e no Portal Admin;
- permitir governanca operacional via `Configuracoes Runtime` para ativar/desativar e ajustar limites sem hardcode de negocio.

## Escopo

- enriquecimento GeoIP dos acessos da landing com cidade, UF, pais e metadados da consulta;
- nova persistencia para eventos de telemetria comportamental;
- endpoint publico de configuracao da telemetria para o front da landing;
- endpoint publico de ingestao de eventos da landing;
- agregacoes administrativas para KPI, funil de engajamento, distribuicao geografica e heatmap fase 1;
- configuracao runtime persistida em `SystemSettings` e editavel no Admin.

## Fora de escopo

- gravacao de sessao em video;
- replay detalhado de movimento de mouse;
- geolocalizacao precisa por GPS/navegador com permissao explicita;
- automacao comercial ou CRM externo.

## Historias relacionadas

- ST-004 - Telemetria fase 1 e GeoIP da landing
- EPIC correlata (Admin): `Documentacao/ADMIN_PORTAL/EPICS/EPIC-028-analytics-comportamental-da-landing-no-admin.md`
