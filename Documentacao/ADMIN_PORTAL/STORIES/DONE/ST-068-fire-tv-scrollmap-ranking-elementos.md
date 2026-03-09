# ST-068 - Scrollmap e ranking de elementos no app Fire TV

## Como
operacao/comercial/growth

## Eu quero
visualizar no app Fire TV o scrollmap e o ranking dos elementos mais clicados da landing

## Para
entender rapidamente, em uma tela executiva, quais blocos estao retendo atencao e quais CTAs/elementos estao concentrando interacao.

## Criterios de aceite

1. O backend de analytics passa a consolidar `scrollmap` por milestones percentuais e ranking de elementos clicados a partir da telemetria ja persistida.
2. O endpoint Fire TV passa a devolver scrollmap e ranking de elementos em payload proprio de TV.
3. O runtime config `FireTvDashboard` controla visibilidade do scrollmap, ranking de elementos e quantidade de itens ranqueados.
4. O app Fire TV renderiza um painel de scrollmap e um painel de ranking de elementos com legibilidade 10-foot.
5. O build web/app da TV permanece valido e a leitura continua read-only.

## Tasks

- [x] introduzir `AdminLandingAnalyticsInsightsDto` com `scrollmap` e `elementRanking`;
- [x] consolidar scrollmap por sessao usando `maxScrollPercent` contra milestones runtime da landing;
- [x] consolidar ranking de elementos a partir de `elementKey`, `elementLabel` e `elementHref`;
- [x] expor os novos blocos no snapshot Fire TV com toggles runtime;
- [x] renderizar paines `Scrollmap` e `Elementos mais clicados` no app TV;
- [x] atualizar runbook/changelog e coberturas de teste.
