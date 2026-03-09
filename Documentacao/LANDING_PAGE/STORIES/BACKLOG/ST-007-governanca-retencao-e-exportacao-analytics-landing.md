# ST-007 - Governanca, retencao e exportacao da telemetria da landing

## Como
Time de operacao/compliance

## Eu quero
ter governanca sobre o volume, a retencao, a anonimização e a exportacao dos dados de telemetria da landing

## Para
manter a trilha analitica util para growth sem perder controle operacional, custo e responsabilidade de dados.

## Criterios de aceite

1. A telemetria da landing deve ter politica de retencao configuravel por banco + Admin `Configuracoes`, com defaults retrocompativeis.
2. Deve existir estrategia clara para diferenciar `IP bruto`, `IP hash`, localidade estimada e dados anonimizados, respeitando o nivel de uso operacional esperado.
3. O Admin deve poder exportar datasets consolidados da landing sem depender de acesso direto ao banco.
4. O sistema deve suportar limpeza/compactacao de eventos antigos sem quebrar KPIs agregados ja persistidos.
5. Manual operacional e de QA da trilha deve passar a incluir retencao, reprocessamento e troubleshooting dos dados analiticos.

## Tasks

- [ ] definir politica de retencao por tipo de dado (`LandingAccessEvents`, `LandingTelemetryEvents`, agregados, exports);
- [ ] mapear quais campos permanecem em bruto e quais devem ser hash/anonimizados no pipeline;
- [ ] planejar job operacional de limpeza/reducao de granularidade por janela de tempo;
- [ ] especificar exportacao CSV/JSON para sessoes, eventos, funis e mapas agregados;
- [ ] definir parametros runtime de retencao, compactacao, limites de export e janela de consulta;
- [ ] prever alertas de volume anormal e protecoes contra crescimento descontrolado da telemetria;
- [ ] documentar impactos de LGPD/operacao e checklist de rollout da trilha.
