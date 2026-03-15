# ST-094 - Ativacao publica do webhook do TelegramBridge

## Contexto

Depois que o `TelegramBridge` passou a suportar `Webhook`, o bot ainda operava em `LongPolling` porque faltavam DNS publico, TLS valido e publicacao do host dedicado na borda da VPS.

## Objetivo

Ativar o endpoint publico `https://telegram.consertapramim.com/api/integrations/telegram/webhook` com certificado valido, proxy reverso para o `TelegramBridge` publicado e registro do `setWebhook` na Bot API.

## Entrega aplicada

1. O DNS `telegram.consertapramim.com` passou a apontar para a mesma VPS do ecossistema publicado.
2. A borda Nginx da VPS passou a expor `telegram.consertapramim.com`, fazendo proxy para `127.0.0.1:5175`.
3. O certificado TLS do host foi emitido e instalado no proxy reverso, deixando `https://telegram.consertapramim.com/health` saudavel.
4. Os secrets de `production` passaram a usar `TelegramBridge:UpdateTransport=Webhook`, `WebhookPublicBaseUrl=https://telegram.consertapramim.com` e `WebhookSecretToken` dedicado.
5. A Bot API passou a responder `getWebhookInfo.url = https://telegram.consertapramim.com/api/integrations/telegram/webhook`, com `pending_update_count = 0`.

## Validacao esperada

1. Acessar `https://telegram.consertapramim.com/health` e confirmar retorno `Healthy`.
2. Consultar `getWebhookInfo` da Bot API e confirmar a URL publica do webhook.
3. Enviar nova mensagem ao bot e validar lead no CPM Full.
4. Confirmar bootstrap/handoff no Chatwoot na mesma trilha operacional ja homologada.

## Risco

- Alto, porque a mudanca tira o bot publicado de `LongPolling` em producao e passa a depender da borda HTTPS e do segredo do webhook para recebimento de updates.
