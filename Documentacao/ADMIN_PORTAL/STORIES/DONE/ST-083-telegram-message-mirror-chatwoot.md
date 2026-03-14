# ST-083 - Mensagens do Telegram espelhadas para o Chatwoot

## Como
operacao de atendimento que usa o bot Telegram como porta de entrada e o Chatwoot como camada humana

## Eu quero
que mensagens novas recebidas no Telegram aparecam automaticamente na conversa humana correta do Chatwoot

## Para
eliminar copia e cola manual, manter o historico humano atualizado e preservar o CPM Full como trilha auditavel entre bot, funil e atendimento.

## Criterios de aceite

1. Mensagem elegivel recebida no Telegram deve ser enfileirada e entregue para a conversa humana correta do Chatwoot.
2. A deduplicacao deve impedir mensagens repetidas quando o mesmo `ChannelMessageId` for processado novamente.
3. O lead deve registrar historico `telegram_message_synced_to_chatwoot`.
4. O vinculo Telegram do lead deve atualizar `LastTelegramMessageSyncedAt`.
5. Falhas externas nao devem quebrar o fluxo principal do bot; a entrega deve ficar rastreavel para retentativa.
6. Manual QA/Operacao, changelog, epic e indice central devem refletir a entrega.

## Tasks

- [x] criar contrato interno para espelhamento de mensagens Telegram no CPM Full;
- [x] implementar fila `telegram_to_chatwoot` com deduplicacao por `Direction + DeliveryKey`;
- [x] entregar mensagem `incoming` no Chatwoot reaproveitando bootstrap da conversa quando necessario;
- [x] registrar historico funcional e timestamp operacional no vinculo Telegram do lead;
- [x] adicionar cobertura de regressao para entrega Telegram -> Chatwoot e validacao de configuracao;
- [x] atualizar manual QA/Operacao, changelog, epic e indice central.
