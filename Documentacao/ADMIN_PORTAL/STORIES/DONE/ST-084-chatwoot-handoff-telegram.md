# ST-084 - Handoff humano do Chatwoot volta para o Telegram

## Como
usuario final e operacao humana de atendimento no ecossistema ConsertaPraMim

## Eu quero
que respostas humanas publicas feitas no Chatwoot retornem para o chat original do Telegram

## Para
manter o atendimento no mesmo canal do usuario, registrar o takeover humano no funil e reduzir mudanca manual de canal durante o suporte.

## Criterios de aceite

1. Mensagem humana publica do Chatwoot em conversa originada do Telegram deve enfileirar entrega para o Telegram.
2. O primeiro outbound humano deve marcar `HumanHandoffStartedAt` no vinculo Telegram do lead.
3. O lead deve registrar historicos `chatwoot_handoff_humano_iniciado` e `chatwoot_message_synced_to_telegram`.
4. O bridge deve expor endpoint interno protegido para enviar a resposta humana ao `TelegramChatId` correto.
5. O vinculo Telegram do lead deve atualizar `LastChatwootMessageSyncedAt`.
6. Manual QA/Operacao, changelog, epic, README da bridge e indice central devem refletir a entrega.

## Tasks

- [x] mapear no webhook do Chatwoot quais mensagens humanas publicas devem voltar ao Telegram;
- [x] implementar fila `chatwoot_to_telegram` com idempotencia por `message_id`;
- [x] criar endpoint interno protegido no bridge para entrega humana ao Telegram;
- [x] marcar handoff humano e timestamps no vinculo Telegram do lead;
- [x] adicionar historico operacional e cobertura de regressao para handoff Chatwoot -> Telegram;
- [x] atualizar manual QA/Operacao, changelog, epic, README da bridge e indice central.
