# ST-009 - UI Admin Operacao de pedidos, propostas e chat

Status: Done  
Epic: EPIC-001

## Objetivo

Disponibilizar telas operacionais para investigacao e atuacao admin em pedidos, propostas e conversas.

## Criterios de aceite

- Tela de pedidos com filtros, status e acao administrativa.
- Tela de propostas com filtro e acao de invalidacao.
- Tela de conversa com historico e anexos.
- Envio de notificacao manual por usuario/pedido.

## Tasks

- [x] Criar modulo `AdminServiceRequests` com listagem e detalhe.
- [x] Criar modulo `AdminProposals` com listagem e acao de invalidacao.
- [x] Criar modulo `AdminChats` com historico e preview de anexos.
- [x] Criar formulario de envio de notificacao manual.
- [x] Implementar guardas de permissao e confirmacoes de operacao.
- [x] Garantir navegacao cruzada entre Usuario -> Pedido -> Proposta -> Conversa.
