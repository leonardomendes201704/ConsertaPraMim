# ST-062 - Heatmap contextual, scrollmap e ranking de elementos no Admin

## Como
Time administrativo de growth/operacao

## Eu quero
visualizar mapas de clique e scroll com contexto de secao, alem de ranking dos elementos mais acionados

## Para
sair da leitura bruta da grade e tomar decisoes de UX e conversao com mais clareza.

## Criterios de aceite

1. O Admin deve conseguir ver heatmap por secao da landing, por origem e por status de conversao.
2. O scrollmap deve mostrar profundidade maxima, media e taxa de chegada por marco configurado.
3. Deve existir ranking de elementos clicados com secao, label, href, percentual de sessoes e participacao na conversao.
4. A tela deve permitir alternar entre `visitas totais`, `modal aberto`, `lead enviado` e `sem conversao`.
5. A configuracao da granularidade, marcos e agrupamentos deve continuar governavel via banco + Admin `Configuracoes`.

## Tasks

- [ ] especificar novos widgets/tabelas de heatmap por secao;
- [ ] modelar visualizacao de scrollmap com barras/faixas e comparativo temporal;
- [ ] definir ranking de elementos clicados com filtros e exportacao;
- [ ] planejar drill-down da celula/elemento para sessoes correlatas;
- [ ] descrever testes de consistencia entre o agregado do mapa e os eventos base.
