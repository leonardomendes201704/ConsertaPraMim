# EPIC-006 - Historico de proposta clicavel no detalhe do pedido (app)

Status: Done
Trilha: CONSUMER_APP_WEB

## Objetivo

Permitir que o cliente, ao visualizar o historico do pedido no app, consiga abrir os detalhes completos de uma proposta recebida com um clique direto no evento da timeline.

## Problema de negocio

- O cliente via o evento "Proposta recebida", mas sem acao direta para entender os detalhes comerciais da proposta.
- Isso aumentava friccao no acompanhamento do pedido e dificultava comparacao de propostas.

## Resultado esperado

- Evento de proposta na timeline do pedido passa a ser clicavel.
- Clique abre tela dedicada de detalhe da proposta.
- API mobile dedicada retorna detalhes de proposta sem acoplar com contratos dos portais web.
- Contrato de timeline inclui referencia de entidade relacionada (`proposalId`) para navegacao.

## Historias vinculadas

- ST-007 - Timeline de proposta clicavel com tela de detalhes da proposta no app
- ST-008 - Tela de detalhes da proposta com acoes de chat e aceite
