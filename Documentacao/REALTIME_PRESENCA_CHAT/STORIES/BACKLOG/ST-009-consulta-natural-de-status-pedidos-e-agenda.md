# ST-009 - Consulta natural de status, pedidos e agenda

Status: Backlog  
Epic: EPIC-002

## Objetivo

Permitir que o cliente consulte pedidos, status, propostas e agendamentos pelo Telegram em linguagem natural, sem comandos tecnicos.

## Criterios de aceite

- Bot entende perguntas de status (ex.: "como esta meu pedido?", "quais agendamentos tenho?").
- Resposta traz informacoes do cliente autenticado e nunca de outro usuario.
- Bot resume dados de forma objetiva e humana, com proximas acoes sugeridas.
- Cliente pode pedir detalhamento de um pedido especifico dentro da mesma conversa.
- Historico da consulta fica persistido para continuidade de contexto.
- Cobertura de testes para consultas mais frequentes e limites de retorno.

## Tasks

- [ ] Definir intents de consulta (`GetOrderStatus`, `ListOrders`, `ListAppointments`, `GetOrderDetails`).
- [ ] Criar/ajustar endpoints API para consulta consolidada de pedidos/agendamentos por cliente.
- [ ] Implementar politicas de resumo e paginacao para respostas conversacionais.
- [ ] Implementar deteccao de referencia contextual (pedido atual, ultimo pedido citado).
- [ ] Implementar resposta amigavel para casos sem dados encontrados.
- [ ] Garantir trilha auditavel de consulta e resposta no historico conversacional.
- [ ] Criar testes unitarios e integracao para intents de consulta e autorizacao.
- [ ] Criar/atualizar diagrama de fluxo Mermaid da funcionalidade.
- [ ] Criar/atualizar diagrama de sequencia Mermaid da funcionalidade.
