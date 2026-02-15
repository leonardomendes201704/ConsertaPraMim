# Operacao de Servico Pos Agendamento

Este diretorio organiza a evolucao do fluxo apos o agendamento, cobrindo execucao de campo, comunicacao preventiva, aditivos comerciais, financeiro, qualidade e mediacao.

## Estrutura

- `EPICS/`: visao macro de negocio e arquitetura de cada frente.
- `STORIES/BACKLOG/`: historias ainda nao iniciadas.
- `STORIES/IN_PROGRESS/`: historias em andamento.
- `STORIES/DONE/`: historias concluidas.

## Fluxo de trabalho

1. Ao iniciar uma historia, mover de `STORIES/BACKLOG/` para `STORIES/IN_PROGRESS/`.
2. Atualizar checkboxes das tasks na propria historia.
3. Ao concluir, mover para `STORIES/DONE/`.
4. Atualizar changelog geral do projeto com impacto funcional e tecnico.

## Convencao de IDs

- Epic: `EPIC-001`, `EPIC-002`, ...
- Story: `ST-001`, `ST-002`, ...
- Task: checklist dentro da historia (`- [ ]` / `- [x]`)

## Guias tecnicos

- `CONFIGURACAO_LEMBRETES_ST-006.md` - parametros de horarios, retries e confirmacao de presenca.
- `HEURISTICA_RISCO_NO_SHOW_ST-007.md` - score inicial, pesos, thresholds e motivos auditaveis.
- `POLITICA_ACAO_RISCO_NO_SHOW_ST-007.md` - acao automatica por nivel de risco, fila operacional e notificacoes.
- `KPIS_NO_SHOW_ST-008.md` - definicao oficial de KPIs, formulas e baseline de alertas operacionais.
- `RUNBOOK_OPERACIONAL_NO_SHOW_ST-008.md` - procedimento de triagem, contato, escalonamento e fechamento de risco/no-show.
- `VALIDACAO_PERFORMANCE_NO_SHOW_ST-008.md` - metodologia, dataset e resultado da validacao de performance em base maior.

## Objetivo de negocio

Sair do fluxo basico de proposta/agendamento para uma operacao completa estilo marketplace de servicos sob demanda, com previsibilidade para cliente, produtividade para prestador e governanca para administracao.
