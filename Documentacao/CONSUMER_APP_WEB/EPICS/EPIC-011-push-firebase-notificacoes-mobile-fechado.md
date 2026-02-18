# EPIC-011 - Push Firebase para notificacoes com app fechado (cliente)

Status: Done
Trilha: CONSUMER_APP_WEB

## Objetivo

Habilitar push notification no app cliente (FCM) para receber eventos mesmo com o app fechado, mantendo fluxo web/navegador inalterado.

## Problema de negocio

- Notificacoes realtime via SignalR atendem bem app aberto, mas nao cobrem app fechado/background.
- Cliente pode perder mensagens de chat e atualizacoes importantes de pedidos/propostas quando nao esta com o app em primeiro plano.

## Resultado esperado

- Registro/desregistro de device token no backend por endpoints mobile dedicados.
- Envio de push por FCM para eventos de notificacao geral e chat.
- App cliente:
  - registra token no login;
  - mostra toast/sino no foreground;
  - abre a tela correta ao tocar na notificacao;
  - desregistra token no logout.
- Navegacao web continua funcionando com email/senha sem dependencia de push nativo.

## Historia vinculada

- ST-013 - Push Firebase no app cliente com registro de token, toast e acao por notificacao.
