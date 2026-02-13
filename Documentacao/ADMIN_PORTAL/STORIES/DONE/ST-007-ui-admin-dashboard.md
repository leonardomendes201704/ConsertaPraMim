# ST-007 - UI Admin Dashboard

Status: Done  
Epic: EPIC-001

## Objetivo

Construir o dashboard do portal admin com visao geral da plataforma.

## Criterios de aceite

- Tela de dashboard renderiza KPIs principais.
- Widgets mostram dados de usuarios, pedidos, propostas e chats.
- Filtros de periodo atualizam cards e tabelas.
- Tela responsiva para desktop e mobile.

## Tasks

- [x] Criar `AdminHomeController` e view `Index`.
- [x] Integrar chamada ao endpoint `GET /api/admin/dashboard`.
- [x] Montar cards de KPI (usuarios, pedidos ativos, propostas aceitas, conversas ativas).
- [x] Montar tabela de eventos recentes.
- [x] Exibir estado de loading, erro e vazio.
- [x] Adicionar atualizacao automatica por SignalR ou polling controlado.

