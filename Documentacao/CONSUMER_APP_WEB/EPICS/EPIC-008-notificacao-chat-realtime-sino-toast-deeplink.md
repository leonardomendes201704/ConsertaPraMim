# EPIC-008 - Notificacao de chat realtime no sino com toast e deep link

Status: Done
Trilha: CONSUMER_APP_WEB

## Objetivo

Garantir que o cliente seja avisado imediatamente quando receber mensagem de prestador fora da tela de conversa, com:

- toast em tempo real;
- notificacao no sino;
- navegacao direta para o chat correto ao clicar.

## Problema de negocio

- Mensagens recebidas fora da tela de chat podiam passar despercebidas.
- Cliente precisava navegar manualmente ate encontrar a conversa.
- Isso aumentava tempo de resposta e friccao operacional.

## Resultado esperado

- Mensagem recebida fora da tela `CHAT` gera toast no app.
- Mensagem recebida fora da tela `CHAT` gera item no sino (`MESSAGE`).
- Clique na notificacao/toast abre direto o chat da mensagem (pedido + prestador).
- Fluxo segue usando `ChatHub` real e sem acoplamento com mock.

## Historia vinculada

- ST-010 - Toast + notificacao de mensagem com redirecionamento direto para conversa
