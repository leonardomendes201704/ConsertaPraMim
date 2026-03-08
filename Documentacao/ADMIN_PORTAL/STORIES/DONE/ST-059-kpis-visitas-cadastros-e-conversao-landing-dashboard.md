# ST-059 - KPIs de visitas, cadastros e conversao da landing na home admin

Status: Done
Epic: EPIC-027

## Objetivo

Permitir que a home do portal admin acompanhe, no mesmo dashboard incremental, as visitas da landing publica, os cadastros de cliente e prestador captados nela e a taxa de conversao do topo do funil.

## Criterios de aceite

- A landing persiste cada acesso relevante (`/`, `/Cliente`, `/Prestador`) em tabela dedicada.
- Cada acesso e cada lead passam a compartilhar `visitorId` estavel para correlacao.
- O dashboard admin passa a calcular:
  - `Visitas`
  - `Cadastros Prestador`
  - `Cadastros Cliente`
  - `Taxa de Conversao`
- Os novos KPIs respeitam o recorte global de periodo da home admin.
- O KPI `Visitas` exibe tambem visitantes unicos.
- O KPI `Taxa de Conversao` mostra a relacao entre visitas e cadastros, com detalhe de visitantes convertidos.
- Manual QA/Operacao, changelog, indice e diagrama Mermaid atualizados no mesmo ciclo.
- Correcao acompanhada de teste de regressao/servico para os calculos do dashboard.

## Tasks

- [x] Registrar Epic/Story/indice da trilha de KPIs da landing no dashboard admin.
- [x] Persistir acessos da landing com `visitorId` e correlacionar leads/cadastros no backend.
- [x] Expor agregados no dashboard admin e renderizar novos KPIs incrementais na home.
- [x] Atualizar manual QA/Operacao, diagrama Mermaid, changelog e testes de regressao.
