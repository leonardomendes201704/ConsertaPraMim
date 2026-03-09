# ST-006 - Heatmap fase 2, scrollmap e ranking de elementos da landing

## Como
Time de growth/operacao

## Eu quero
enxergar o comportamento da landing por secao, profundidade de scroll e elementos clicados com contexto mais analitico

## Para
identificar com clareza quais blocos realmente recebem atencao, onde os usuarios desistem e quais CTAs concentram interesse.

## Criterios de aceite

1. O heatmap deve deixar de ser apenas uma grade global e passar a permitir leitura por secao e por tipo de origem do lead.
2. A telemetria deve produzir `scrollmap` com profundidade maxima, profundidade media e taxa de chegada por marco configuravel.
3. Deve existir ranking de elementos clicados com `elementKey`, `label`, `href`, secao e taxa relativa sobre sessoes.
4. Deve ser possivel comparar, no mesmo dataset, trafego total, sessoes com modal aberto e sessoes que viraram lead.
5. Granularidade de grade, marcos de scroll e elementos rastreaveis devem ser parametrizaveis via banco + UI Admin `Configuracoes`.

## Tasks

- [ ] evoluir o modelo de telemetria para vincular cada clique e cada milestone de scroll a uma `sectionKey`;
- [ ] permitir grade de heatmap configuravel por secao ou por viewport, com defaults seguros;
- [ ] registrar evento de profundidade final de scroll por sessao para compor `scrollmap`;
- [ ] consolidar ranking de elementos com `elementKey`, `elementLabel`, `elementHref`, `sectionKey` e origem da sessao;
- [ ] preparar agregacoes para comparar heatmap/scrollmap de `Cliente`, `Prestador`, `Neutro`, `Lead gerado` e `Sem conversao`;
- [ ] definir parametros runtime de marcos de scroll, tamanho de grade, exclusao de elementos ruidosos e janela minima de sessao;
- [ ] garantir testes de regressao para agregacao por secao e normalizacao de cliques/scroll;
- [ ] documentar rollout e leitura analitica quando a implementacao acontecer.
