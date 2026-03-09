# ST-063 - Sessoes, cohorts e qualidade de trafego da landing no Admin

## Como
Time administrativo de growth/operacao

## Eu quero
acompanhar a qualidade das sessoes da landing por cohorts de tempo, origem e engajamento

## Para
distinguir volume vazio de trafego realmente promissor e orientar investimento/operacao comercial.

## Criterios de aceite

1. O Admin deve conseguir enxergar sessoes por cohort temporal e por faixa de qualidade/engajamento.
2. Deve haver leitura operacional de `tempo engajado`, `profundidade de scroll`, `modal aberto`, `submit com erro`, `submit com sucesso` e `lead gerado` por cohort.
3. A tela deve destacar fontes/segmentos com alta visita e baixa qualidade, ou baixo volume e alta conversao.
4. O detalhe da sessao deve mostrar timeline clara do comportamento e flags de qualidade do trafego.
5. Regras de score, limiares e classificacoes devem nascer parametrizadas em banco + Admin `Configuracoes`.

## Tasks

- [ ] definir score operacional de qualidade de sessao com regras explicitas e auditaveis;
- [ ] planejar cohorts por dia, semana, canal, origem e faixa de engajamento;
- [ ] especificar grid/timeline de sessoes com flags e explicacao do score;
- [ ] prever exportacao e filtros do detalhe por cohort/qualidade;
- [ ] mapear parametros runtime de score, thresholds e janelas temporais;
- [ ] descrever cobertura de testes para score, cohorts e leitura operacional.
