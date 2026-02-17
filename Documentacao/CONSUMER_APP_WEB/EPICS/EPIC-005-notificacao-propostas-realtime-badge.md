# EPIC-005 - Notificacao realtime de propostas no app com badge por pedido

Status: Done
Trilha: CONSUMER_APP_WEB

## Objetivo

Permitir que o cliente seja notificado em tempo real no app quando um prestador enviar proposta, com:

- alerta no sino (contador de nao lidas);
- toast no app;
- badge de quantidade de propostas por pedido.

## Problema de negocio

- O app nao recebia notificacoes realtime da proposta criada pelo prestador.
- O cliente precisava atualizar telas manualmente para perceber novas propostas.
- O card de pedido nao mostrava de forma clara quantas propostas aquele pedido ja recebeu.

## Resultado esperado

- App conecta no `notificationHub` com JWT e entra no grupo do usuario.
- Quando o prestador envia proposta:
  - cliente recebe evento `ReceiveNotification`;
  - sino incrementa contador de nao lidas;
  - toast aparece em tempo real;
  - pedido impactado incrementa `proposalCount` no estado local.
- Endpoints mobile de pedidos retornam `proposalCount` para baseline consistente apos recarga.

## Historias vinculadas

- ST-006 - Notificacao realtime de proposta no sino, toast no app e badge por pedido
