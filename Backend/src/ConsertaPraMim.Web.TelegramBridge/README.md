# ConsertaPraMim.Web.TelegramBridge

Painel web em ASP.NET Core (.NET 8) para conversar com usuarios do Telegram em tempo real.

## Funcionalidades

- Visual de chat inspirado no WhatsApp.
- Login cliente/prestador com vinculo automatico de conversa por usuario autenticado (sem `chatId` manual).
- Envio e recebimento em tempo real via SignalR.
- Upload de anexos (imagem, video e documento).
- Polling da Telegram Bot API para capturar mensagens do usuario.
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
5. O usuario autenticado entra direto na conversa vinculada ao login e o orquestrador IA responde automaticamente.
6. Fluxos `Client` continuam podendo abrir pedido e consultar agenda/pedidos; fluxos `Provider` alimentam o board `prestadores` do CPM Full e nao devem abrir `service request` de cliente.
7. Com `MirrorMessagesEnabled=true`, mensagens recebidas do Telegram passam a ser espelhadas para o CPM Full/Chatwoot, e respostas humanas vindas do Chatwoot passam a ser entregues de volta ao Telegram pelo endpoint interno protegido.
8. Rode o projeto:

```bash
dotnet run --project Backend/src/ConsertaPraMim.Web.TelegramBridge/ConsertaPraMim.Web.TelegramBridge.csproj
```

## Endpoints internos

- `GET /api/chats`
- `GET /api/chats/{chatId}/messages`
- `POST /api/chats/{chatId}/messages`
- `POST /api/internal/telegram/messages/send`
- `Hub SignalR: /hubs/telegram-chat`
