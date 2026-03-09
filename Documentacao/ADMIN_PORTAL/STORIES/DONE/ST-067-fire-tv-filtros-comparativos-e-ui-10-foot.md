# ST-067 - Filtros comparativos e UI 10-foot no dashboard Fire TV

## Como
operacao/comercial/growth/lideranca

## Eu quero
ajustar o dashboard Fire TV para uma leitura mais 10-foot, com filtros grandes e comparacao automatica com periodo anterior

## Para
usar a TV como painel executivo continuo sem depender de leitura densa ou interpretacao manual do delta entre periodos.

## Criterios de aceite

1. O app Fire TV exibe filtros grandes de `Janela`, `Origem` e `Comparacao`, navegaveis por controle remoto/D-pad.
2. O endpoint `GET /api/admin/fire-tv/landing-dashboard` aceita os filtros `rangeDays`, `origin` e `comparisonMode` e devolve o estado selecionado no payload.
3. Os 8 KPIs principais passam a exibir, quando habilitado, o valor atual, o valor do periodo comparativo e o delta percentual.
4. O runtime config `FireTvDashboard` persiste em banco e permite controlar opcoes de origem, modos de comparacao e visibilidade do comparativo via `Configuracoes` no portal admin.
5. A UI ganha hierarquia visual propria de TV (`10-foot UI`), com tipografia maior, filtros destacados, cards amplos e estados de foco claros.

## Tasks

- [x] ampliar o contrato do endpoint Fire TV com filtros de origem e comparacao;
- [x] atualizar o runtime config `FireTvDashboard` com opcoes persistidas/editaveis para origem e comparacao;
- [x] recalcular os 8 KPIs com delta contra periodo anterior no backend;
- [x] redesenhar o dashboard do app com filtro rail e cards mais legiveis para TV;
- [x] atualizar testes do controller e service do snapshot Fire TV.
