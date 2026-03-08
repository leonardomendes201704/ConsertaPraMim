# ST-004 - Telemetria fase 1 e GeoIP da landing

## Como
Time de growth/operacao

## Eu quero
capturar acessos, engajamento e localidade estimada da landing publica com uma telemetria propria de fase 1

## Para
entender comportamento, medir atencao real na pagina, qualificar origem geografica do trafego e orientar a operacao comercial com dados mais defensaveis.

## Criterios de aceite

1. Cada acesso relevante da landing grava `visitorId`, `sessionId`, URL, host, origem inicial do CTA e localidade estimada por IP sem quebrar o carregamento da pagina.
2. O browser da landing captura heartbeat de sessao visivel, marcos de scroll e cliques em elementos interativos relevantes via endpoint publico proprio.
3. A telemetria fase 1 pode ser ativada/desativada e parametrizada via `Configuracoes Runtime`, com parametros persistidos em banco e defaults retrocompativeis.
4. O Portal Admin passa a ter telas especificas para analytics da landing com KPIs, breakdown geografico, funil de eventos, heatmap agregado inicial e detalhe por sessao.
5. Manual QA/operacao, diagramas, changelog e testes automatizados da trilha sao atualizados no mesmo ciclo.

## Tasks

- [x] estender o modelo de acesso da landing para incluir `sessionId` e campos GeoIP estimados;
- [x] criar modelo/persistencia de eventos comportamentais da landing;
- [x] criar runtime config `LandingAnalytics` em `SystemSettings` + UI de `Configuracoes Runtime`;
- [x] expor endpoint publico de config e endpoint publico de ingestao dos eventos;
- [x] integrar JS da landing para heartbeat, scroll depth, click tracking e session bootstrap;
- [x] construir agregacoes administrativas e APIs autenticadas de overview e detalhe por sessao;
- [x] construir modulo `Analytics Landing` no Portal Admin com filtros em drawer/offcanvas;
- [x] atualizar manual, changelog e diagramas da feature;
- [x] validar build, testes e encoding dos modulos impactados.
