# ST-104 - Kanban autonomo e timers operacionais da jornada

Status: Backlog
Epic: EPIC-JORNADA-001

## Objetivo

Fazer o card do cliente caminhar sozinho pelas etapas do Kanban com base em eventos reais e timers operacionais.

## Criterios de aceite

- O card muda de etapa automaticamente conforme a jornada progride.
- Timers vencidos geram acao automatica ou excecao.
- A operacao entende o motivo de cada mudanca de etapa.

## Tasks

- [ ] Criar matriz de transicoes automatizadas do Kanban da jornada.
- [ ] Implementar worker ou orquestrador de transicao automatica.
- [ ] Persistir motivo e origem de cada mudanca de etapa.
- [ ] Criar timers para `dados pendentes`, `agenda pendente`, `aceite pendente` e `avaliacoes pendentes`.
- [ ] Cobrir idempotencia para transicoes repetidas ou fora de ordem.
