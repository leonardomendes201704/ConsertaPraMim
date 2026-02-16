# EPIC-003 - Meus Pedidos no app com endpoint mobile dedicado

Status: Done
Trilha: CONSUMER_APP_WEB

## Objetivo

Substituir a listagem mockada de "Meus Pedidos" do app cliente por consumo real da API, com contrato dedicado para mobile/app e separacao nativa entre pedidos finalizados e nao finalizados.

## Resultado esperado

- App deixa de depender de dados mockados para "Meus Pedidos".
- API expone endpoint dedicado ao app: `GET /api/mobile/client/orders`.
- Resposta da API retorna grupos separados:
  - `openOrders` (nao finalizados)
  - `finalizedOrders` (finalizados/cancelados)
- Tela "Meus Pedidos" exibe abas "Ativos" e "Historico" com dados reais.
- Integracao isolada dos portais web (sem reuso obrigatorio do contrato dos portais).

## Historias vinculadas

- ST-003 - Listagem real de "Meus Pedidos" no app com endpoint mobile dedicado
- ST-004 - Detalhes do pedido no app com acompanhamento historico e fluxo operacional correto
