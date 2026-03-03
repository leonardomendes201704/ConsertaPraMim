# ConsertaPraMim.Web.TelegramBridge

Painel web em ASP.NET Core (.NET 8) para conversar com usuarios do Telegram em tempo real.

## Funcionalidades

- Visual de chat inspirado no WhatsApp.
- Login cliente com vinculo automatico de conversa por `ClientId` (sem `chatId` manual).
- Envio e recebimento em tempo real via SignalR.
- Upload de anexos (imagem, video e documento).
- Polling da Telegram Bot API para capturar mensagens do usuario.
- Download e persistencia local de anexos recebidos no Telegram.

## Configuracao

1. Defina o token do bot em `appsettings.Development.json` ou via variavel de ambiente:
   - `TelegramBridge__BotToken`
2. Garanta configuracao de `ApiBaseUrl` para autenticar no endpoint `POST /api/auth/login`.
3. O cliente autenticado entra direto na conversa vinculada ao login.
3. Rode o projeto:

```bash
dotnet run --project Backend/src/ConsertaPraMim.Web.TelegramBridge/ConsertaPraMim.Web.TelegramBridge.csproj
```

## Endpoints internos

- `GET /api/chats`
- `GET /api/chats/{chatId}/messages`
- `POST /api/chats/{chatId}/messages`
- `Hub SignalR: /hubs/telegram-chat`
