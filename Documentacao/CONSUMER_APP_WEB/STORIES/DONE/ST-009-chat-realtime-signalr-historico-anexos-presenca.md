# ST-009 - Chat realtime SignalR com historico, anexos e presenca no app

Status: Done
Epic: EPIC-007

## Objetivo

Implementar no app cliente um chat realtime de verdade entre cliente e prestador, usando o `ChatHub` da API para:

- listar conversas ativas;
- abrir historico real por pedido/prestador;
- enviar mensagens e anexos;
- refletir recibos de entrega/leitura;
- mostrar presenca e status do prestador.

## Criterios de aceite

- Tela `CHAT_LIST` deixa de usar dados mock e passa a carregar `GetMyActiveConversations`.
- Tela `CHAT` carrega historico real via `GetHistory`.
- Tela `CHAT` entra na sala da conversa via `JoinRequestChat`.
- Envio de mensagem de texto usa `SendMessage`.
- Envio de anexo usa `POST /api/chat-attachments/upload` e encaminha anexos no `SendMessage`.
- Mensagens recebidas em tempo real atualizam a tela de chat sem refresh.
- Lista de conversas recebe atualizacao realtime para nova mensagem e nao lidas.
- Recibos de entrega/leitura (`MarkConversationDelivered`/`MarkConversationRead`) atualizam status visual.
- Presenca (`ReceiveUserPresence`) e status operacional (`ReceiveProviderStatus`) do prestador sao exibidos no header do chat.
- App limpa conexao de chat ao encerrar sessao.

## Tasks

- [x] Criar servico dedicado `realtimeChat.ts` com conexao SignalR para `chatHub`.
- [x] Implementar normalizacao resiliente de payload (camelCase/PascalCase) para mensagens, recibos e conversas.
- [x] Implementar metodos de dominio do app:
  - `getMyActiveConversations`
  - `joinRequestConversation`
  - `getConversationHistory`
  - `sendConversationMessage`
  - `markConversationDelivered`
  - `markConversationRead`
  - `getConversationParticipantPresence`
  - `uploadConversationAttachments`
- [x] Substituir `ChatList.tsx` mockado por consumo real + eventos realtime.
- [x] Substituir `Chat.tsx` mockado por historico real, envio de texto/anexo e status de mensagem.
- [x] Integrar `App.tsx` para abrir chat por conversa real (`requestId + providerId`) e encerrar conexao no logout.
- [x] Validar build do app apos integracao (`npm run build`).
- [x] Atualizar documentacao da trilha e catalogo de diagramas.
- [x] Gerar diagramas Mermaid (fluxo e sequencia) para a funcionalidade.

## Arquivos impactados

### App

- `conserta-pra-mim app/services/realtimeChat.ts`
- `conserta-pra-mim app/components/ChatList.tsx`
- `conserta-pra-mim app/components/Chat.tsx`
- `conserta-pra-mim app/App.tsx`
- `conserta-pra-mim app/types.ts`

### Documentacao

- `Documentacao/CONSUMER_APP_WEB/EPICS/EPIC-007-chat-realtime-cliente-prestador-app.md`
- `Documentacao/CONSUMER_APP_WEB/INDEX.md`
- `Documentacao/CONSUMER_APP_WEB/DOCUMENTACAO_COMPLETA_CONSERTA_PRA_MIM_APP.md`
- `Documentacao/DIAGRAMAS/INDEX.md`
- `Documentacao/DIAGRAMAS/CONSUMER_APP_WEB/APP-009-chat-realtime-signalr/fluxo-chat-realtime-signalr.mmd`
- `Documentacao/DIAGRAMAS/CONSUMER_APP_WEB/APP-009-chat-realtime-signalr/sequencia-chat-realtime-signalr.mmd`

## Diagramas

- Fluxo:
  - `Documentacao/DIAGRAMAS/CONSUMER_APP_WEB/APP-009-chat-realtime-signalr/fluxo-chat-realtime-signalr.mmd`
- Sequencia:
  - `Documentacao/DIAGRAMAS/CONSUMER_APP_WEB/APP-009-chat-realtime-signalr/sequencia-chat-realtime-signalr.mmd`
