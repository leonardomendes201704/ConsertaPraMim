# ST-002 - Detalhe de pedido e envio de proposta no app do prestador

Status: Done
Epic: EPIC-001

## Objetivo

Permitir ao prestador abrir detalhes do pedido no app, analisar contexto do cliente/local e enviar proposta comercial sem depender do portal web.

## Criterios de aceite

- Endpoint dedicado para detalhe de pedido no app:
  - `GET /api/mobile/provider/requests/{requestId}`
- Endpoint dedicado para envio de proposta no app:
  - `POST /api/mobile/provider/requests/{requestId}/proposals`
- Regras de negocio respeitadas:
  - prestador so envia proposta em pedido elegivel;
  - bloqueio de duplicidade por prestador/pedido;
  - validacao de ownership/acesso por raio/categoria/regras atuais.
- Tela de detalhes exibe resumo do pedido, local e status.
- Tela permite enviar proposta com valor estimado opcional e mensagem.

## Tasks

- [x] Criar DTOs mobile de detalhe de pedido e proposta do prestador.
- [x] Evoluir servico mobile provider com operacoes de detalhe e submit de proposta.
- [x] Criar endpoints dedicados com respostas sem acoplamento com portais.
- [x] Implementar tela de detalhe no app do prestador.
- [x] Implementar formulario de envio de proposta no app.
- [x] Atualizar documentacao Swagger e diagramas.

## Diagramas

- Fluxo: `Documentacao/DIAGRAMAS/PROVIDER_APP_WEB/ST-002-detalhe-pedido-envio-proposta-app-prestador/fluxo-detalhe-pedido-envio-proposta-app-prestador.mmd`
- Sequencia: `Documentacao/DIAGRAMAS/PROVIDER_APP_WEB/ST-002-detalhe-pedido-envio-proposta-app-prestador/sequencia-detalhe-pedido-envio-proposta-app-prestador.mmd`
