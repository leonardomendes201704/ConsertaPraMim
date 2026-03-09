# EPIC-005 - Evolucao do analytics comportamental, mapas e atribuicao da landing

## Objetivo

Evoluir a telemetria fase 1 da landing publica para um pacote de analytics mais util para growth e operacao, saindo do heatmap agregado basico para uma leitura contextual por secao, origem, dispositivo, campanha, tempo engajado e funil de CTA.

## Resultado esperado

- medir tempo engajado real por sessao com mais confiabilidade do que apenas heartbeat bruto;
- contextualizar cliques, scroll e aberturas de modal por secao da landing e por CTA;
- distinguir trafego geral, interesse em cliente, interesse em prestador e conversao em lead;
- preparar base para scrollmap, heatmap por secao e ranking de elementos clicados;
- manter todos os parametros funcionais/configuraveis persistidos em banco e editaveis no Admin em `Configuracoes`.

## Escopo

- enriquecimento da sessao da landing com classificacao de dispositivo, origem de trafego e secao ativa;
- captura de eventos adicionais de fase 2 no browser sem replay completo de sessao;
- melhoria da modelagem de tempo engajado, profundidade de scroll e CTA funnel;
- novos parametros runtime para granularidade, amostragem, limites e retencao;
- base de dados e agregacoes para leituras comparativas no Admin.

## Fora de escopo

- replay em video da sessao;
- gravacao continua de movimento de mouse;
- integracao com ferramenta third-party de analytics/heatmap;
- enriquecimento por GPS do navegador;
- automacao comercial externa ou CRM neste ciclo.

## Decisoes e restricoes

- todo parametro funcional/configuravel desta trilha deve nascer com persistencia em banco e edicao via Admin `Configuracoes`;
- defaults devem ser seguros e retrocompativeis para tenants antigos;
- a telemetria deve continuar degradando de forma segura quando endpoints internos falharem;
- o modelo deve privilegiar agregacao operacional e governanca, nao coleta excessiva sem objetivo analitico claro.

## Historias relacionadas

- ST-005 - Sessao engajada, atribuicao de CTA e contexto de secao da landing
- ST-006 - Heatmap fase 2, scrollmap e ranking de elementos da landing
- ST-007 - Governanca, retencao e exportacao da telemetria da landing
- Epic correlata (Admin): `Documentacao/ADMIN_PORTAL/EPICS/EPIC-029-analytics-avancado-da-landing-no-admin.md`
