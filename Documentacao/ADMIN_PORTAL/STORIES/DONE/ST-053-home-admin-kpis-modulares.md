# ST-053 - Home admin com KPIs modulares e carregamento incremental

Status: Done
Epic: EPIC-022

## Objetivo

Separar cada KPI da home do portal admin em componente independente, com carregamento individual por endpoint especifico, skeleton/ghost, spinner e estados de erro localizados.

## Criterios de aceite

- Cada KPI principal da home admin e do painel de no-show e renderizado por componente separado.
- Cada componente consulta endpoint proprio, preservando o recorte global de filtros da tela.
- Cards exibem skeleton/ghost no boot e spinner durante refresh individual.
- Falha em um KPI nao derruba os demais cards nem a tela inteira.
- Manual QA cobre comportamento de carregamento individual, erro local e refresh seletivo.

## Tasks

- [x] Mapear KPIs da home, criar epic/story e definir contrato de componente/card.
- [x] Criar DTOs e endpoints dedicados por KPI para dashboard geral e no-show.
- [x] Implementar componentes reutilizaveis e carga incremental dos KPIs gerais.
- [x] Implementar carga incremental dos KPIs de no-show, QA e fechamento E2E.

## Task 2 - Contratos e endpoints dedicados

- Criado DTO reutilizavel `AdminKpiCardDto` com `title`, `value`, `caption` e linhas de detalhe.
- API passou a expor:
  - `GET /api/admin/dashboard/kpis/{kpiKey}`
  - `GET /api/admin/no-show-dashboard/kpis/{kpiKey}`
- Portal admin recebeu proxies autenticados:
  - `GET /AdminHome/Kpis/dashboard/{kpiKey}`
  - `GET /AdminHome/Kpis/no-show/{kpiKey}`
- Servicos de dashboard/no-show ganharam cache curto em memoria para evitar recomputacao integral a cada card durante o mesmo ciclo de refresh.

## Task 3 - KPIs gerais componentizados

- Criado componente Razor reutilizavel `_IncrementalMetricCard.cshtml` para os cards executivos da home.
- Cards gerais (`usuarios`, `pedidos`, `propostas`, `creditos`, `agenda`, `recompra`, `NPS`) agora iniciam com skeleton/ghost.
- Refresh posterior passa a usar spinner local por card, sem bloquear tabelas, eventos e widgets secundarios.
- Atualizacao monolitica dos KPIs gerais via snapshot foi removida do JavaScript; a fonte oficial desses cards agora e o endpoint dedicado de cada KPI.

## Task 4 - KPIs de no-show incrementais e fechamento

- Os nove KPIs do painel de no-show foram migrados para o mesmo componente Razor reutilizavel, mantendo identidade visual propria e consumo individual dos endpoints dedicados.
- O JavaScript da home deixou de reaproveitar o snapshot agregado para preencher esses cards; agora o refresh isolado dos KPIs de no-show acontece junto com os cards executivos sem travar tabelas, ranking e breakdowns.
- Manual QA recebeu o caso `QA-ADM-057` para validar skeleton, spinner, erro localizado e resiliencia dos cards incrementais.
- Story encerrada com sincronismo no board, changelog e artefatos de documentacao operacional.

## KPIs alvo da fase

### Dashboard geral

- Usuarios totais
- Usuarios online
- Pedidos ativos
- Propostas aceitas
- Conversas ativas
- Creditos concedidos
- Creditos consumidos
- Saldo em aberto
- Creditos a expirar
- Operacao da agenda
- Taxa de recompra
- NPS operacional

### Painel no-show

- Taxa de no-show
- Comparecimento
- Confirmacao dupla
- Risco alto
- Fila operacional
- Reincidencia cliente (90d)
- Reincidencia prestador (90d)
- Usuarios criticos (cliente)
- Usuarios criticos (prestador)
