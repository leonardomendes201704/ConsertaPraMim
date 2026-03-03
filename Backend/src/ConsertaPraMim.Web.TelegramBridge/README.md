# ConsertaPraMim.Web.TelegramBridge

Painel web em ASP.NET Core (.NET 8) para conversar com usuarios do Telegram em tempo real.

## Funcionalidades

- Visual de chat inspirado no WhatsApp.
- Envio e recebimento em tempo real via SignalR.
- Upload de anexos (imagem, video e documento).
- Polling da Telegram Bot API para capturar mensagens do usuario.
- Download e persistencia local de anexos recebidos no Telegram.

## Configuracao

1. Defina o token do bot em `appsettings.Development.json` ou via variavel de ambiente:
   - `TelegramBridge__BotToken`
2. Garanta que o usuario envie mensagem para o bot no Telegram para abrir o chat.
3. Rode o projeto:

```bash
dotnet run --project Backend/src/ConsertaPraMim.Web.TelegramBridge/ConsertaPraMim.Web.TelegramBridge.csproj
```

## Endpoints internos

- `GET /api/chats`
- `GET /api/chats/{chatId}/messages`
- `POST /api/chats/open`
- `POST /api/chats/{chatId}/messages`
- `Hub SignalR: /hubs/telegram-chat`
