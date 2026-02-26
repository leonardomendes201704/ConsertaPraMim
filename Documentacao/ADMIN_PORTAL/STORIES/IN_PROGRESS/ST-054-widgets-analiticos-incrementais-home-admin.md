# ST-054 - Widgets analiticos incrementais na home admin

Status: In Progress
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
- [ ] Implementar contratos e endpoints dedicados para widgets analiticos e operacionais da home admin.
- [ ] Componentizar receita, status, categoria, operacao, prestadores, rankings, outliers e falhas com carga isolada.
- [ ] Componentizar Eventos Recentes com endpoint dedicado, preservar filtros locais/ordenacao e fechar QA E2E.
