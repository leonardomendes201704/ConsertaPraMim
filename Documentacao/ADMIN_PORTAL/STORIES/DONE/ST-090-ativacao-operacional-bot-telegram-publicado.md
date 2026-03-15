# ST-090 - Ativacao operacional do bot Telegram no ambiente publicado

## Como
time de operacao, backend e devops do ecossistema ConsertaPraMim

## Eu quero
ativar o bot Telegram publicado com `LongPolling`, garantindo que `TelegramBridge` e `CPM Full` compartilhem a mesma configuracao `TelegramAutomation`

## Para
permitir o fluxo real `Telegram -> CPM Full -> Chatwoot` em producao, sem disputa do mesmo bot entre `development` e `production`.

## Criterios de aceite

1. O `web-cpmfull` publicado consome `TelegramAutomation__*` no compose da VPS.
2. O workflow `deploy-vps` escreve `TELEGRAM_AUTOMATION_TELEGRAM_BRIDGE_BASE_URL` no `.env.vps`.
3. A operacao orienta ativar o `BotToken` em apenas um environment quando o transporte estiver em `LongPolling`.
4. O runbook cobre troubleshooting de disputa de `getUpdates` e falta de `TelegramAutomation` no CPM Full.

## Tasks

- [x] propagar `TELEGRAM_AUTOMATION_TELEGRAM_BRIDGE_BASE_URL` no workflow e no `.env.vps.example`;
- [x] injetar `TelegramAutomation__*` no `Backend/docker-compose.vps.web-cpmfull.yml`;
- [x] atualizar changelog, indice, epic e manual com a ativacao operacional do bot;
- [x] preparar a ativacao de secrets apenas no environment `production`.
