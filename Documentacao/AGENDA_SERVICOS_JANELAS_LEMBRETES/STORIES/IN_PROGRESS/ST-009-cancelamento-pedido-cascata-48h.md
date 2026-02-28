# ST-009 - Cancelamento de pedido em cascata com politica de 48h

Status: In Progress  
Epic: EPIC-002

## Objetivo

Entregar o fluxo E2E para o cliente cancelar o pedido inteiro, respeitando antecedencia minima de 48 horas em todos os agendamentos ativos do pedido, com cancelamento em cascata, status final `Canceled` no pedido e notificacao para prestadores com interacao no caso.

## Criterios de aceite

- Cliente visualiza acao propria de `Cancelar pedido` no detalhe do pedido.
- O sistema valida todos os agendamentos ativos antes de permitir o cancelamento.
- O cancelamento e bloqueado se algum agendamento violar a regra de 48 horas ou estiver em estado nao elegivel.
- Quando elegivel, todos os agendamentos cancelaveis do pedido sao cancelados em cascata.
- O pedido fica com status `Canceled` e nao retorna para `Matching`.
- Prestadores com interacao definida pela regra recebem notificacao contextual.
- A tela do cliente exibe o impacto do cancelamento antes da confirmacao.

## Tasks

- [x] Task 1 - Criar Epic + Story + tasks, atualizar indice da trilha e publicar diagrama inicial do fluxo.
- [x] Task 2 - Implementar backend do cancelamento de pedido com validacao agregada de 48h, cascata e fan-out.
- [x] Task 3 - Implementar UI do cliente com acao `Cancelar pedido` e explicacao do impacto por agendamento.
- [ ] Task 4 - Atualizar QA/manual, executar validacoes finais e encerrar a story.

## Diagramas

- Fluxo: `Documentacao/DIAGRAMAS/AGENDA_SERVICOS_JANELAS_LEMBRETES/ST-009-cancelamento-pedido-cascata-48h/fluxo-cancelamento-pedido-cliente.mmd`
