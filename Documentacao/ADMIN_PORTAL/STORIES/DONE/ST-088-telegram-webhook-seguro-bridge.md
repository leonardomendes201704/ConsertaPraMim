# ST-088 - Transporte webhook seguro no TelegramBridge

## Como
time de operacao e backend do ecossistema ConsertaPraMim

## Eu quero
habilitar o `ConsertaPraMim.Web.TelegramBridge` para receber updates do Telegram por webhook seguro, sem perder o fallback atual de long polling

## Para
publicar o bot com transporte inbound mais estavel, compatível com borda HTTPS e com a mesma trilha operacional ja homologada entre Telegram, CPM Full e Chatwoot.

## Criterios de aceite

1. O bridge aceita `LongPolling` e `Webhook` como modos inbound configuraveis.
2. No modo `Webhook`, o bridge registra `setWebhook`, valida `X-Telegram-Bot-Api-Secret-Token` e reaproveita a mesma trilha de persistencia/mirror.
3. No modo `LongPolling`, o bootstrap remove webhook anterior e mantem `getUpdates` como fallback oficial.
4. README, manual QA/Operacao, changelog e epic ficam atualizados com a nova forma de operacao.

## Tasks

- [x] adicionar configuracao `TelegramBridge:UpdateTransport` e parametros do webhook;
- [x] extrair o processamento inbound para servico compartilhado entre polling e webhook;
- [x] publicar endpoint `POST /api/integrations/telegram/webhook` com validacao de secret token;
- [x] registrar/remover webhook automaticamente na Bot API conforme o modo configurado;
- [x] reforcar cobertura automatizada minima e atualizar documentacao operacional.
