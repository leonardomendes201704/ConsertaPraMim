# ST-091 - Bootstrap publico da primeira mensagem do bot Telegram

## Como
time de operacao, atendimento e backend do ecossistema ConsertaPraMim

## Eu quero
que a primeira mensagem enviada diretamente ao bot Telegram publicado crie ou atualize automaticamente o lead tecnico no CPM Full, sem depender de login previo no painel web do bridge

## Para
garantir que o fluxo real `Telegram -> CPM Full -> Chatwoot` aconteca desde o primeiro contato do usuario no bot publicado.

## Criterios de aceite

1. A primeira mensagem do bot publicado bootstrapa lead tecnico deterministico por `TelegramChatId`.
2. O `ChatbotConversationId` usado no mirror inbound passa a ser estavel entre mensagens da mesma conversa.
3. O board inicial e resolvido automaticamente entre `clientes` e `prestadores`.
4. O bot envia um ACK inicial apenas quando o lead nasce e nao existe handoff humano ativo.
5. O changelog, o manual operacional, o epic e o README do bridge registram explicitamente o comportamento novo.

## Tasks

- [x] bootstrapar lead tecnico no inbound do `TelegramBridge` antes do mirror;
- [x] gerar `ChatbotConversationId` e `UserId` deterministicos a partir do `TelegramChatId`;
- [x] resolver board inicial por heuristica leve e enviar ACK inicial ao usuario;
- [x] adicionar teste de regressao e atualizar documentacao operacional.
