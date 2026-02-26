# ST-053 - Home admin com KPIs modulares e carregamento incremental

Status: In Progress
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

- [ ] Mapear KPIs da home, criar epic/story e definir contrato de componente/card.
- [x] Criar DTOs e endpoints dedicados por KPI para dashboard geral e no-show.
- [ ] Implementar componentes reutilizaveis e carga incremental dos KPIs gerais.
- [ ] Implementar carga incremental dos KPIs de no-show, QA e fechamento E2E.

## Task 2 - Contratos e endpoints dedicados

- Criado DTO reutilizavel `AdminKpiCardDto` com `title`, `value`, `caption` e linhas de detalhe.
- API passou a expor:
  - `GET /api/admin/dashboard/kpis/{kpiKey}`
  - `GET /api/admin/no-show-dashboard/kpis/{kpiKey}`
- Portal admin recebeu proxies autenticados:
  - `GET /AdminHome/Kpis/dashboard/{kpiKey}`
  - `GET /AdminHome/Kpis/no-show/{kpiKey}`
- Servicos de dashboard/no-show ganharam cache curto em memoria para evitar recomputacao integral a cada card durante o mesmo ciclo de refresh.

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
