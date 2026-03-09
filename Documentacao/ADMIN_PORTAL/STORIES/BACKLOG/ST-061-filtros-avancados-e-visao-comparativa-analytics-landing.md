# ST-061 - Filtros avancados e visao comparativa do analytics da landing

## Como
Time administrativo de growth/operacao

## Eu quero
filtrar e comparar a landing por origem, dispositivo, campanha, secao, geografia e status de conversao

## Para
entender com mais precisao onde o trafego qualificado aparece e onde a operacao deve atuar primeiro.

## Criterios de aceite

1. O modulo `Analytics Landing` deve permitir filtros combinados por periodo, origem do interesse, device class, traffic source, secao e geografia estimada.
2. Deve existir modo comparativo entre pelo menos dois recortes relevantes (ex.: `Cliente x Prestador`, `Mobile x Desktop`, `Lead x Sem lead`).
3. A tela deve evidenciar variacao de sessoes, tempo engajado, scroll, cliques e conversao entre os recortes escolhidos.
4. Os filtros e presets precisam ter governanca operacional e defaults seguros, persistidos em banco e editaveis no Admin `Configuracoes` quando aplicavel.
5. O detalhamento deve continuar abrindo sessoes/visitas coerentes com os filtros escolhidos.

## Tasks

- [ ] definir DTOs, queries e agregacoes para filtros por origem, device, secao, geografia e campanha;
- [ ] planejar presets analiticos reutilizaveis no Portal Admin;
- [ ] especificar cards comparativos com delta absoluto e percentual por recorte;
- [ ] prever ordenacao consistente entre overview, grids e drill-down;
- [ ] mapear parametros runtime/presets configuraveis em `SystemSettings` + `Configuracoes`;
- [ ] definir testes de API e UI para filtros compostos e comparativos.
