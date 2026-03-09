# EPIC-029 - Analytics avancado da landing no Portal Admin

## Objetivo

Transformar o modulo atual `Analytics Landing` em um cockpit analitico mais acionavel, permitindo segmentacao, comparacao e leitura de qualidade de trafego, engajamento e conversao da landing no Portal Admin.

## Resultado esperado

- filtros analiticos mais ricos por dispositivo, origem, campanha, secao e faixa geografica;
- heatmap e scrollmap contextualizados por secao e por funil;
- ranking dos elementos mais clicados e CTAs com melhor desempenho;
- visao comparativa de sessoes, conversao e tempo engajado;
- datasets exportaveis e governanca operacional sobre o uso da trilha.

## Escopo

- novas leituras e componentes no `Analytics Landing`;
- comparativos por origem do lead e status de conversao;
- drill-down operacional por sessao, secao, CTA e elemento clicado;
- exportacao e filtros analiticos mais ricos;
- integracao com a configuracao runtime da trilha para deixar thresholds/segmentos governaveis.

## Fora de escopo

- BI externo completo;
- replay visual pixel a pixel;
- modelagem preditiva de conversao baseada em ML neste ciclo.

## Historias relacionadas

- ST-061 - Filtros avancados e visao comparativa do analytics da landing
- ST-062 - Heatmap contextual, scrollmap e ranking de elementos no Admin
- ST-063 - Sessoes, cohorts e qualidade de trafego da landing no Admin
- Epic correlata (Landing): `Documentacao/LANDING_PAGE/EPICS/EPIC-005-evolucao-analytics-mapas-e-atribuicao-landing.md`
