# ST-085 - Diagnostico operacional do Telegram no CPM Full

## Como
time tecnico e operacao de suporte do ecossistema ConsertaPraMim

## Eu quero
ter diagnostico consolidado da trilha Telegram no Kanban do CPM Full

## Para
identificar falhas entre bot, bridge, CPM Full e Chatwoot, acompanhar fila bidirecional e reprocessar entregas sem SQL manual.

## Criterios de aceite

1. O Kanban deve expor drawer `Diagnostico Telegram` com resumo operacional, fila e falhas recentes.
2. O drawer deve mostrar metricas de volume, falha e latencia do Telegram Bridge.
3. A fila `telegram_to_chatwoot` e `chatwoot_to_telegram` deve ficar visivel no admin.
4. Itens de fila `retrying` ou `dead_letter` devem poder ser reprocessados manualmente.
5. A trilha deve propagar `X-Correlation-ID` entre bridge, CPM Full e Chatwoot para auditoria operacional.
6. Manual QA/Operacao, changelog, epic, README da bridge e indice central devem refletir a entrega.

## Tasks

- [x] concluir snapshot SQL do diagnostico Telegram no `SqlAdminKanbanService`;
- [x] criar cliente interno para consumir observabilidade do Telegram Bridge a partir do CPM Full;
- [x] expor `GET /admin/funil/telegram/diagnostico/json` e `POST /admin/funil/telegram/fila/{queueItemId}/retentativa`;
- [x] adicionar drawer `Diagnostico Telegram` no Kanban com filtros e acoes rapidas;
- [x] propagar `X-Correlation-ID` nas chamadas bridge -> CPM Full e CPM Full -> bridge;
- [x] atualizar manual QA/Operacao, changelog, epic, README da bridge e indice central.
