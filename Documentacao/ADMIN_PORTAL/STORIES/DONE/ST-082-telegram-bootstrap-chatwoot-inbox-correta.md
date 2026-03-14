# ST-082 - Lead Telegram abre Chatwoot na inbox correta do CPM Full

## Como
operacao de atendimento humano no ecossistema ConsertaPraMim

## Eu quero
que leads originados no bot Telegram abram ou reaproveitem a conversa humana na inbox correta do Chatwoot

## Para
evitar triagem manual fora do funil, preservar o board correto (`clientes` ou `prestadores`) e manter rastreabilidade operacional entre bot, lead e atendimento humano.

## Criterios de aceite

1. Lead Telegram de `clientes` deve abrir ou reaproveitar conversa no inbox `CPM Clientes`.
2. Lead Telegram de `prestadores` deve abrir ou reaproveitar conversa no inbox `CPM Prestadores`.
3. O contato e a conversa do Chatwoot devem refletir `Canal de Origem = Telegram`.
4. Quando o lead Telegram ainda nao tiver conversa vinculada, o CPM Full deve registrar historico `Bootstrap Telegram no Chatwoot`.
5. O reprocessamento nao deve duplicar conversa humana se ja existir conversa no mesmo contato + inbox.
6. Manual QA/Operacao, changelog e epic devem refletir a entrega.

## Tasks

- [x] confirmar o reaproveitamento do `ChatwootLeadSyncService` para leads Telegram;
- [x] registrar historico funcional dedicado para bootstrap via Telegram;
- [x] cobrir `ClientsInboxId` e `ProvidersInboxId` com regressao automatizada;
- [x] validar deduplicacao de conversa existente no mesmo contato/inbox;
- [x] atualizar epic, manual QA/Operacao, changelog e indice central.
