# ST-054 - Widgets analiticos incrementais na home admin

Status: Done
Epic: EPIC-023

## Objetivo

Transformar os widgets analiticos e operacionais restantes da home admin em componentes independentes, carregados por endpoints dedicados com skeleton, spinner e falha isolada.

## Criterios de aceite

- Cada widget abaixo passa a ter componente proprio e endpoint dedicado:
  - Receita Mensal de Assinaturas
  - Pedidos por Status
  - Pedidos por Categoria
  - Atendimento Operacional
  - Status dos Prestadores
  - Ranking de Prestadores
  - Ranking de Clientes
  - Outliers de Reputacao
  - Falhas de Pagamento por Prestador
  - Falhas por Canal
  - Eventos Recentes
- Cada widget respeita o recorte global de filtros da home admin.
- Eventos Recentes preserva filtros locais e ordenacao por coluna apos refresh incremental.
- Falha em um widget nao derruba os demais widgets nem o restante da home.
- Manual QA cobre carregamento incremental, erro localizado e consistencia dos widgets apos refresh.

## Tasks

- [x] Criar epic/story/tasks e registrar backlog da componentizacao incremental dos widgets restantes.
- [x] Implementar contratos e endpoints dedicados para widgets analiticos e operacionais da home admin.
- [x] Componentizar receita, status, categoria, operacao, prestadores, rankings, outliers e falhas com carga isolada.
- [x] Componentizar Eventos Recentes com endpoint dedicado, preservar filtros locais/ordenacao e fechar QA E2E.

## Task 2 - Contratos e endpoints dedicados

- Criado contrato reutilizavel `AdminDashboardWidgetDto` para widgets de lista, tabela, receita e eventos recentes.
- API passou a expor `GET /api/admin/dashboard/widgets/{widgetKey}` para os 11 widgets da home admin, reaproveitando o mesmo recorte global de filtros.
- Portal admin recebeu proxy autenticado `GET /AdminHome/Widgets/{widgetKey}` para alimentar componentes independentes sem expor diretamente o token JWT no navegador.
- O `AdminDashboardService` passou a mapear widgets a partir do dashboard ja cacheado, evitando recomputacao integral por bloco.

## Task 3 - Componentizacao dos widgets analiticos (exceto Eventos Recentes)

- Widgets `Receita Mensal de Assinaturas`, `Pedidos por Status`, `Pedidos por Categoria`, `Atendimento Operacional`, `Status dos Prestadores`, `Ranking de Prestadores`, `Ranking de Clientes`, `Outliers de Reputacao`, `Falhas de Pagamento por Prestador` e `Falhas por Canal` foram migrados para partials dedicadas.
- Cada partial recebeu contrato visual incremental (`data-dashboard-widget`) com `skeleton`, `spinner`, bloco de conteudo e erro localizado.
- `AdminHome/index.js` passou a consumir `GET /AdminHome/Widgets/{widgetKey}` por widget, aplicando refresh paralelo e resiliente sem depender de mutacao monolitica do snapshot.
- Falha de um widget nao interrompe os demais blocos da home e nao derruba o refresh do dashboard/no-show.

## Task 4 - Eventos Recentes incremental + fechamento E2E

- Bloco `Eventos Recentes` foi migrado para partial dedicada (`_WidgetRecentEvents`) com contrato incremental (`data-dashboard-widget`) e estados de `skeleton`, `spinner` e erro localizado.
- Refresh global da home passou a atualizar o widget de eventos via endpoint dedicado `GET /AdminHome/Widgets/recent-events`.
- Filtros locais e ordenacao por coluna foram preservados no cliente, mesmo apos refresh incremental, sem regressao no drawer offcanvas.
- Manual QA foi atualizado para explicitar o endpoint dedicado na validacao `QA-ADM-056`.
