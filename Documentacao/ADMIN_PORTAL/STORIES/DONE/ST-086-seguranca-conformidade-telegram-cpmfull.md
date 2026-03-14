# ST-086 - Seguranca e conformidade da trilha Telegram no CPM Full

## Como
time de seguranca, operacao e suporte do ecossistema ConsertaPraMim

## Eu quero
endurecer a trilha Telegram -> CPM Full -> Chatwoot com mascaramento, retention e protecao dos endpoints internos

## Para
reduzir exposicao de PII, segredos e payloads sensiveis sem perder rastreabilidade operacional do atendimento.

## Criterios de aceite

1. Telefone, chat id, token e dados sensiveis devem aparecer mascarados em logs e telas tecnicas.
2. Endpoints internos da automacao devem continuar exigindo segredo compartilhado, e o runbook deve deixar explicito que webhook publico futuro do Telegram so pode entrar com validacao de origem/segredo.
3. Payloads antigos da fila Telegram e anexos locais do bridge devem possuir retention controlada.
4. Manual QA/Operacao, epic, changelog, README do bridge e indice central devem refletir a entrega.

## Tasks

- [x] adicionar sanitizador dedicado da trilha Telegram no CPM Full e no Telegram Bridge;
- [x] mascarar `chatId`, e-mail, telefone, token, segredo e erros tecnicos no detalhe do lead, drawer de diagnostico e logs;
- [x] adicionar worker de retention para `dbo.cpm_web_telegram_delivery_queue`;
- [x] adicionar worker de retention de anexos em `wwwroot/uploads/telegram-bridge`;
- [x] validar e documentar o uso obrigatorio de `X-Telegram-Automation-Key` nos endpoints internos;
- [x] publicar runbook de rotacao de token/segredo e checklist de QA da trilha endurecida.
