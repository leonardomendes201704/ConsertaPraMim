# ST-092 - Hotfix do deploy do CPM Full para ativar TelegramAutomation no ambiente publicado

## Contexto

Depois da ativacao operacional do bot Telegram em `production`, o `ConsertaPraMim.Web.TelegramBridge` passou a consumir mensagens reais por `LongPolling`, mas o `ConsertaPraMim.Web.CpmFull` publicado continuou respondendo `409 Automacao Telegram desabilitada no ambiente atual.`.

## Problema observado

- O bot recebia updates reais da Bot API.
- O bridge chamava `POST /api/integrations/telegram/automation/lead` e `POST /api/integrations/telegram/automation/message`.
- O `web-cpmfull` publicado respondia `409`, impedindo a criacao do lead e o espelhamento no Chatwoot.

## Causa raiz

O job `deploy-web-cpmfull` do workflow `.github/workflows/deploy-vps.yml` continuava escrevendo `Backend/.env.vps` sem o bloco `TELEGRAM_AUTOMATION_*`.

Como consequencia:

- `TelegramAutomation__Enabled=false`
- `TelegramAutomation__MirrorMessagesEnabled=false`
- `TelegramAutomation__SharedSecret=` vazio

no container `cpm-prd-cpmfull`, mesmo com os secrets do environment `production` ja cadastrados.

## Objetivo

Garantir que o deploy publicado do `ConsertaPraMim.Web.CpmFull` receba toda a configuracao `TelegramAutomation`, alinhando runtime e pipeline com o que ja estava ativo no `TelegramBridge`.

## Entrega aplicada

1. O job `deploy-web-cpmfull` passou a derivar e escrever no `.env.vps` todas as variaveis `TELEGRAM_AUTOMATION_*` consumidas pelo compose do `web-cpmfull`.
2. O workflow passou a preencher `TELEGRAM_AUTOMATION_TELEGRAM_BRIDGE_BASE_URL` com fallback interno para `http://<container-prefix>-telegrambridge:<porta>`.
3. O hotfix operacional recriou o container `cpm-prd-cpmfull` com `TelegramAutomation` ativa, `MirrorMessagesEnabled=true` e `SharedSecret` preenchido.
4. A trilha documental foi atualizada com troubleshooting especifico para o sintoma `bot recebe update, mas o CPM Full responde 409`.

## Validacao esperada

1. Enviar nova mensagem para `@chatwootcpm_bot`.
2. Confirmar no log do `telegrambridge` resposta `2xx` do endpoint `/api/integrations/telegram/automation/lead`.
3. Confirmar no CPM Full a criacao ou atualizacao do lead com `Source = Telegram`.
4. Confirmar no Chatwoot a criacao ou reaproveitamento da conversa humana.

## Risco

- Alto no ambiente publicado, porque o problema bloqueava completamente o fluxo `Telegram -> CPM Full -> Chatwoot` mesmo com o bot operacional.
