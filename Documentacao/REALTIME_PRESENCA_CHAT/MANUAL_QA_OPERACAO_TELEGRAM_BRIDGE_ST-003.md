# Manual QA/Operacao - Telegram Bridge Web (ST-003)

## 1. Objetivo

Padronizar como operar e validar o painel `ConsertaPraMim.Web.TelegramBridge`, cobrindo chat realtime, envio de texto e anexos, e recepcao de mensagens do Telegram.

## 2. Pre-requisitos

- Bot Telegram criado com `@BotFather`.
- Token do bot configurado em `TelegramBridge__BotToken` (ou `appsettings`).
- Projeto em execucao:
  - `dotnet run --project Backend/src/ConsertaPraMim.Web.TelegramBridge/ConsertaPraMim.Web.TelegramBridge.csproj`
- Usuario de teste enviou ao menos uma mensagem para o bot (abre a conversa no Telegram).

## 3. Rotina operacional

1. Abrir o painel web e informar o `Chat ID` no formulario lateral.
2. Clicar em `Abrir` para iniciar/fixar a conversa.
3. Enviar mensagem textual e confirmar entrega no Telegram.
4. Validar envio pelo teclado: `Enter` envia mensagem e `Shift+Enter` quebra linha.
5. Enviar anexos pelo botao `+` e confirmar recebimento no Telegram.
6. Responder pelo Telegram e verificar entrada automatica no painel (sem refresh).
7. Em caso de necessidade de auditoria, coletar arquivo salvo em `wwwroot/uploads/telegram-bridge`.

## 4. Checklist QA (smoke)

- [ ] QA-TGB-001: painel carrega lista de conversas sem erro JavaScript.
- [ ] QA-TGB-002: abrir conversa por `Chat ID` cria item na sidebar.
- [ ] QA-TGB-003: envio de mensagem textual aparece no painel e no Telegram.
- [ ] QA-TGB-003A: `Enter` envia mensagem quando o botao `Enviar` esta habilitado.
- [ ] QA-TGB-003B: `Shift+Enter` nao envia e permite quebra de linha no composer.
- [ ] QA-TGB-004: envio de imagem aparece no painel e no Telegram.
- [ ] QA-TGB-005: envio de documento aparece no painel e no Telegram.
- [ ] QA-TGB-006: resposta no Telegram aparece no painel em tempo real.
- [ ] QA-TGB-007: reconexao SignalR mantem recebimento de mensagens apos queda de rede.
- [ ] QA-TGB-008: build `dotnet build Backend/src/src.sln` conclui sem erros.

## 5. Regressao recomendada

- Validar novamente apos mudanca em:
  - endpoint Telegram;
  - servico de polling;
  - hub SignalR;
  - regras de upload/download.

## 6. Troubleshooting

## 6.1 Mensagens nao chegam do Telegram

- Verificar se `TelegramBridge__BotToken` esta preenchido.
- Confirmar se o usuario enviou mensagem inicial ao bot.
- Revisar logs do `TelegramLongPollingBackgroundService`.

## 6.2 Falha no envio de anexo

- Validar tamanho do arquivo (limite em `MaxAttachmentBytes`).
- Validar permissao de escrita em `wwwroot/uploads/telegram-bridge`.
- Conferir retorno HTTP `502` do endpoint `POST /api/chats/{chatId}/messages`.

## 6.3 Mensagens nao atualizam em tempo real

- Verificar conexao com `/hubs/telegram-chat` no browser.
- Confirmar status `Online` no chip do topo da sidebar.
- Validar bloqueio de rede/proxy para WebSocket.
