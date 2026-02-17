# EPIC-004 - Solicitacao de servico no app com paridade ao portal e endpoints mobile dedicados

Status: Done
Trilha: CONSUMER_APP_WEB

## Objetivo

Garantir que o fluxo de "solicitar servico" no app cliente siga a mesma logica funcional do portal do cliente, mas usando contrato de API exclusivo para mobile/app.

## Problema de negocio

- O app tinha fluxo parcialmente mockado e diferente do portal.
- O contrato de criacao nao era dedicado ao canal mobile.
- Havia risco de divergencia de regra e regressao entre canais.

## Resultado esperado

- Fluxo do app alinhado ao portal do cliente:
  1. escolha de categoria + descricao;
  2. resolucao de CEP com preenchimento de endereco;
  3. revisao e publicacao do pedido.
- Endpoints dedicados ao app (sem acoplamento com os portais):
  - `GET /api/mobile/client/service-requests/categories`
  - `GET /api/mobile/client/service-requests/zip-resolution?zipCode=...`
  - `POST /api/mobile/client/service-requests`
- Tela de categorias e wizard de novo pedido consumindo dados reais da API.
- Pedido criado no app entrando no mesmo fluxo operacional do backend usado pelo portal.

## Historias vinculadas

- ST-005 - Fluxo de solicitar servico no app com paridade ao portal e endpoints mobile dedicados
