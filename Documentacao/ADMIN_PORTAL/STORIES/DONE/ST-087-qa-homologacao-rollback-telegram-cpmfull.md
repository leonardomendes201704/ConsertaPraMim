# ST-087 - QA, homologacao e rollback da trilha Telegram no CPM Full

## Como
time de QA, operacao e suporte do ecossistema ConsertaPraMim

## Eu quero
fechar a trilha Telegram com cobertura automatizada, checklist final de homologacao e plano de rollback

## Para
operar a automacao Telegram -> CPM Full -> Chatwoot com seguranca, previsibilidade e resposta rapida a incidentes.

## Criterios de aceite

1. Existem testes automatizados cobrindo criacao de lead, fila/worker bidirecional, idempotencia e falha com retentativa.
2. O manual possui checklist final de homologacao da trilha `clientes`, `prestadores`, bootstrap Chatwoot, espelhamento inbound, handoff humano, diagnostico e seguranca.
3. O manual documenta rollback por feature flags sem derrubar o fluxo principal do bot.
4. O epic `EPIC-TELEGRAM-001` fica encerrado com status final `Completed`.

## Tasks

- [x] ampliar cobertura automatizada da automacao de lead Telegram;
- [x] criar testes do worker bidirecional da fila Telegram;
- [x] validar idempotencia de outbound humano sem `ChatwootMessageId`;
- [x] consolidar checklist final de homologacao no manual QA/Operacao;
- [x] documentar rollback por feature flags e encerramento do epic Telegram;
- [x] atualizar changelog e indice central.
