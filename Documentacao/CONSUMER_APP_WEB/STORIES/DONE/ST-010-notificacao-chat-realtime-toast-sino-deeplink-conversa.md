# ST-010 - Notificacao de mensagem realtime com toast, sino e deep link para chat

Status: Done
Epic: EPIC-008

## Objetivo

Quando o cliente receber mensagem de prestador e nao estiver na tela de chat aberta, o app deve:

- mostrar toast imediato;
- adicionar notificacao no sino;
- abrir diretamente a conversa correta ao clicar no toast/notificacao.

## Criterios de aceite

- App inicia conexao global no `chatHub` apos login para receber eventos de mensagem.
- Evento `ReceiveChatMessage` recebido fora da tela `CHAT` cria notificacao tipo `MESSAGE`.
- Toast exibe nome do prestador e resumo da mensagem/anexo.
- Item do sino guarda `requestId` e `providerId` para deep link.
- Clicar no toast ou no item da tela de notificacoes abre o chat da conversa correta.
- Se notificacao nao for de mensagem, comportamento existente permanece.

## Tasks

- [x] Evoluir contrato `Notification` com metadados de conversa (`providerId`, `providerName`).
- [x] Expor inicializacao global da conexao de chat (`startRealtimeChatConnection`).
- [x] Assinar eventos de chat no `App.tsx` para capturar mensagens em qualquer tela.
- [x] Implementar regra: somente fora da view `CHAT` deve gerar toast + notificacao.
- [x] Implementar deep link no clique do sino/toast para abrir conversa (`requestId + providerId`).
- [x] Preservar comportamento atual de notificacoes nao-chat.
- [x] Validar build do app apos alteracoes.
- [x] Atualizar documentacao e diagramas Mermaid da funcionalidade.

## Arquivos impactados

### App

- `conserta-pra-mim app/types.ts`
- `conserta-pra-mim app/services/realtimeChat.ts`
- `conserta-pra-mim app/App.tsx`

### Documentacao

- `Documentacao/CONSUMER_APP_WEB/EPICS/EPIC-008-notificacao-chat-realtime-sino-toast-deeplink.md`
- `Documentacao/CONSUMER_APP_WEB/INDEX.md`
- `Documentacao/CONSUMER_APP_WEB/DOCUMENTACAO_COMPLETA_CONSERTA_PRA_MIM_APP.md`
- `Documentacao/CONSUMER_APP_WEB/CHECKLIST_QA_E2E_CONSERTA_PRA_MIM_APP.md`
- `Documentacao/DIAGRAMAS/INDEX.md`
- `Documentacao/DIAGRAMAS/CONSUMER_APP_WEB/APP-010-notificacao-chat-toast-sino-deeplink/fluxo-notificacao-chat-toast-sino-deeplink.mmd`
- `Documentacao/DIAGRAMAS/CONSUMER_APP_WEB/APP-010-notificacao-chat-toast-sino-deeplink/sequencia-notificacao-chat-toast-sino-deeplink.mmd`

## Diagramas

- Fluxo:
  - `Documentacao/DIAGRAMAS/CONSUMER_APP_WEB/APP-010-notificacao-chat-toast-sino-deeplink/fluxo-notificacao-chat-toast-sino-deeplink.mmd`
- Sequencia:
  - `Documentacao/DIAGRAMAS/CONSUMER_APP_WEB/APP-010-notificacao-chat-toast-sino-deeplink/sequencia-notificacao-chat-toast-sino-deeplink.mmd`
