# EPIC-002 - Cancelamento de pedido em cascata com politica de 48h e notificacao multi-prestador

## Objetivo

Permitir que o cliente cancele o pedido como uma acao propria, com regra unica de antecedencia minima de 48 horas, tratamento consistente de multiplos agendamentos do mesmo pedido e comunicacao automatica para todos os prestadores que ja interagiram com a solicitacao.

## Problema atual

- O sistema hoje cancela apenas agendamentos individuais.
- O pedido pode voltar para `Matching` em vez de encerrar como `Canceled`.
- A regra atual de antecedencia nao cobre o requisito de 48 horas para cancelamento do pedido pelo cliente.
- Prestadores que enviaram proposta ou tiveram interacao com o pedido podem nao ser notificados quando o cliente desiste da solicitacao.
- A UX atual nao explicita o impacto do cancelamento sobre todos os agendamentos vinculados.

## Resultado esperado

- Cliente cancela o pedido inteiro a partir da tela de detalhes.
- A regra considera todos os agendamentos ativos vinculados ao pedido.
- Se houver agendamentos ativos fora da politica, o cancelamento do pedido e bloqueado com mensagem clara.
- Se elegivel, o cancelamento ocorre em cascata nos agendamentos cancelaveis.
- O pedido termina em `Canceled` de forma definitiva.
- Prestadores com interacao relevante recebem notificacao contextual de encerramento do pedido.

## Guardrails

- A regra de 48h vale para cancelamento iniciado pelo cliente.
- O fluxo nao pode deixar o pedido em `Matching` apos cancelamento do pedido.
- O historico de agendamento e a trilha de auditoria precisam continuar rastreaveis.
- A UI deve mostrar impacto por agendamento antes da confirmacao.
- Todas as mensagens de front devem permanecer em PT-BR.

## Story vinculada

- `ST-009` - Cancelamento de pedido em cascata com politica de 48h.
