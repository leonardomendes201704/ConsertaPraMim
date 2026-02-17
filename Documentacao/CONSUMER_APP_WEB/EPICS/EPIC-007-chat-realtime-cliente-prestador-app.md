# EPIC-007 - Chat realtime cliente-prestador no app

Status: Done
Trilha: CONSUMER_APP_WEB

## Objetivo

Substituir o chat mockado do app por um chat realtime real, reaproveitando a mesma infraestrutura de comunicacao dos portais web (SignalR + ChatHub), mantendo contratos dedicados ao app e sem regressao de fluxo.

## Problema de negocio

- O app exibia conversa simulada, sem persistencia e sem sincronismo com o ecossistema real.
- Cliente e prestador perdiam contexto operacional quando alternavam entre app e portal.
- Nao havia entrega confiavel de mensagens, leitura, presenca e anexos no app.

## Resultado esperado

- Lista de conversas e historico consumidos do `ChatHub` real.
- Envio de mensagens e anexos no app refletindo em tempo real para cliente e prestador.
- Presenca online e status operacional do prestador exibidos na interface.
- Recibos de entrega/leitura refletidos na conversa.
- Fluxo mobile alinhado ao comportamento dos portais para o mesmo pedido/prestador.

## Historias vinculadas

- ST-009 - Chat realtime no app com SignalR, historico real, anexos e presenca
