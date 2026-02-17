# ST-004 - Chat realtime cliente x prestador no app do prestador

Status: Done
Epic: EPIC-001

## Objetivo

Permitir que o prestador opere conversas com clientes em tempo real no app, com historico, indicador de nao lidas e notificacao com deep link.

## Criterios de aceite

- Endpoints e contratos dedicados para chat no canal mobile provider.
- Lista de conversas e tela de chat realtime com historico e anexos.
- Badge de mensagens nao lidas no app do prestador.
- Notificacao in-app (toast) com deep link para abrir a conversa correta.

## Tasks

- [x] Definir contrato mobile provider de conversas e mensagens.
- [x] Integrar SignalR no app do prestador.
- [x] Implementar lista de conversas e chat.
- [x] Implementar notificacoes in-app/toast/deeplink.
- [x] Atualizar documentacao e diagramas.

## Diagramas

- Fluxo: `Documentacao/DIAGRAMAS/PROVIDER_APP_WEB/ST-004-chat-realtime-cliente-prestador-app-provider/fluxo-chat-realtime-app-prestador.mmd`
- Sequencia: `Documentacao/DIAGRAMAS/PROVIDER_APP_WEB/ST-004-chat-realtime-cliente-prestador-app-provider/sequencia-chat-realtime-app-prestador.mmd`
