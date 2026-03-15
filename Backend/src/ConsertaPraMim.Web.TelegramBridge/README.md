# ConsertaPraMim.Web.TelegramBridge

Painel web em ASP.NET Core (.NET 8) para conversar com usuarios do Telegram em tempo real.

## Funcionalidades

- Visual de chat inspirado no WhatsApp.
- Login cliente/prestador com vinculo automatico de conversa por usuario autenticado (sem `chatId` manual).
- Envio e recebimento em tempo real via SignalR.
- Upload de anexos (imagem, video e documento).
- Captura inbound da Telegram Bot API por `long polling` ou `webhook` seguro.
- Download e persistencia local de anexos recebidos no Telegram.

## Configuracao

1. Defina o token do bot em `appsettings.Development.json` ou via variavel de ambiente:
   - `TelegramBridge__BotToken`
2. Configure a URL da API principal:
   - `ApiBaseUrl` (ex.: `http://localhost:5193`)
3. Configure a chave OpenAI por secret/env (nao versionar):
   - `TelegramBridgeAi__ApiKey`
4. Para a automacao Telegram -> CPM Full -> Chatwoot, configure a secao `TelegramAutomation`:
   - `TelegramAutomation__Enabled`
   - `TelegramAutomation__ClientsAutomationEnabled`
   - `TelegramAutomation__ProvidersAutomationEnabled`
   - `TelegramAutomation__MirrorMessagesEnabled`
   - `TelegramAutomation__RequireHumanHandoffForOutbound`
   - `TelegramAutomation__CpmFullBaseUrl`
   - `TelegramAutomation__SharedSecret`
   - `TelegramAutomation__RequestTimeoutSeconds`
5. Configure tambem a secao `TelegramBridge`:
   - `TelegramBridge__UpdateTransport`
   - `TelegramBridge__WebhookPublicBaseUrl`
   - `TelegramBridge__WebhookPath`
   - `TelegramBridge__WebhookSecretToken`
   - `TelegramBridge__WebhookDropPendingUpdates`
   - `TelegramBridge__AttachmentRetentionEnabled`
   - `TelegramBridge__AttachmentRetentionDays`
   - `TelegramBridge__AttachmentRetentionIntervalMinutes`
6. O usuario autenticado entra direto na conversa vinculada ao login e o orquestrador IA responde automaticamente.
7. Fluxos `Client` continuam podendo abrir pedido e consultar agenda/pedidos; fluxos `Provider` alimentam o board `prestadores` do CPM Full e nao devem abrir `service request` de cliente.
8. Com `MirrorMessagesEnabled=true`, mensagens recebidas do Telegram passam a ser espelhadas para o CPM Full/Chatwoot, e respostas humanas vindas do Chatwoot passam a ser entregues de volta ao Telegram pelo endpoint interno protegido.
9. O bridge mascara `chatId`, e-mail, telefone, token e segredo em logs/diagnosticos tecnicos. Os endpoints internos continuam protegidos por `TelegramAutomation__SharedSecret`.
10. Com `TelegramBridge__UpdateTransport=LongPolling`, o bootstrap remove webhook anterior do bot e mantem `getUpdates` como canal inbound.
11. Com `TelegramBridge__UpdateTransport=Webhook`, o bridge registra automaticamente `setWebhook` na Bot API com `TelegramBridge__WebhookPublicBaseUrl + TelegramBridge__WebhookPath`, exige o header `X-Telegram-Bot-Api-Secret-Token` e desabilita o worker de long polling.
12. O endpoint publico do webhook fica em `POST /api/integrations/telegram/webhook` e deve ser publicado em HTTPS.
13. Para diagnostico operacional interno, o bridge expoe `GET /api/internal/telegram/observability/dashboard`, protegido por `TelegramAutomation__SharedSecret` e consumido pelo drawer `Diagnostico Telegram` do CPM Full.
14. Rode o projeto:

```bash
dotnet run --project Backend/src/ConsertaPraMim.Web.TelegramBridge/ConsertaPraMim.Web.TelegramBridge.csproj
```

## Publicacao na VPS

1. O workflow `.github/workflows/deploy-vps.yml` agora publica o bridge como servico `web-telegrambridge`.
2. A compose dedicada fica em `Backend/docker-compose.vps.web-telegrambridge.yml`.
3. O Dockerfile publicado fica em `Backend/docker/vps/Dockerfile.web.telegrambridge`.
4. A porta do bridge e `5175` em `main/master` e `6175` em `dev-local`.
5. A URL publica recomendada e `https://telegram.consertapramim.com`.
6. O healthcheck da pipeline valida `GET /health`.
7. Para operar em modo webhook na VPS, configurar no environment do GitHub Actions:
   - `PUBLIC_TELEGRAM_BRIDGE_URL`
   - `TELEGRAM_BRIDGE_BOT_TOKEN`
   - `TELEGRAM_BRIDGE_UPDATE_TRANSPORT`
   - `TELEGRAM_BRIDGE_WEBHOOK_PUBLIC_BASE_URL`
   - `TELEGRAM_BRIDGE_WEBHOOK_PATH`
   - `TELEGRAM_BRIDGE_WEBHOOK_SECRET_TOKEN`
   - `TELEGRAM_AUTOMATION_ENABLED`
   - `TELEGRAM_AUTOMATION_SHARED_SECRET`
8. Se `PUBLIC_TELEGRAM_BRIDGE_URL` nao estiver configurada em `development`, a pipeline cai no fallback `http://<VPS_PUBLIC_HOST>:6175/health`.

## Endpoints internos

- `GET /api/chats`
- `GET /api/chats/{chatId}/messages`
- `POST /api/chats/{chatId}/messages`
- `POST /api/integrations/telegram/webhook`
- `POST /api/internal/telegram/messages/send`
- `GET /api/internal/telegram/observability/dashboard`
- `GET /health`
- `Hub SignalR: /hubs/telegram-chat`
