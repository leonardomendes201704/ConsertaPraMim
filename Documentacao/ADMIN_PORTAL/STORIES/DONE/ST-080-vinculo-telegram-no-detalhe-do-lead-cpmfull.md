# ST-080 - Vinculo Telegram no detalhe do lead do CPM Full

## Como
operacao comercial, suporte e auditoria do ecossistema ConsertaPraMim

## Eu quero
visualizar no detalhe do lead do CPM Full o vinculo tecnico entre a conversa do bot Telegram, o pedido gerado e a conversa humana do Chatwoot

## Para
investigar reprocessamentos, validar deduplicacao e dar suporte operacional sem depender de consulta manual ao banco.

## Criterios de aceite

1. O detalhe do lead deve expor o vinculo tecnico salvo em `dbo.cpm_web_telegram_funil_links`.
2. O modal do Kanban deve mostrar os principais IDs operacionais da trilha Telegram em PT-BR.
3. O bloco `telegram` deve estar disponivel no endpoint `GET /admin/funil/lead/{id}/json`.
4. O lead deve continuar exibindo `Source = Telegram` como origem funcional.
5. Deve existir teste cobrindo persistencia e leitura do vinculo Telegram no detalhe do lead.
6. Changelog, manual QA/Operacao, epic e indice central devem refletir a entrega.

## Tasks

- [x] estender `AdminKanbanLeadDetailsRecord` para incluir o vinculo Telegram;
- [x] carregar o ultimo registro de `cpm_web_telegram_funil_links` no `SqlAdminKanbanService`;
- [x] expor o bloco `telegram` no `LeadDetailsJson` do `KanbanController`;
- [x] atualizar o modal do Kanban com a secao `Vinculo Telegram`;
- [x] adicionar teste de regressao para leitura do vinculo Telegram;
- [x] atualizar `EPIC-TELEGRAM-001`, manual QA/Operacao, changelog e indice central.
