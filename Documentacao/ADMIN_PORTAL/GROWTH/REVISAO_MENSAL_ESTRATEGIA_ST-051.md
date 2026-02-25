# ST-051 - Processo de revisao mensal de estrategia

Status: Released
Epic: EPIC-020
Owner: Comite de Growth

## Objetivo

Padronizar o fechamento mensal de growth para revisar resultado do ciclo, registrar decisao estrategica e definir as apostas do mes seguinte com ownership claro.

## Entradas obrigatorias

- Performance da North Star (`RQ72`) no mes.
- Guardrails de liquidez, conversao, no-show/disputas e qualidade.
- Capacidade de entrega (stories em progresso, backlog critico e throughput do roadmap).
- Contexto financeiro (receita, margem operacional e restricoes de budget).

## Agenda oficial

1. Fechamento do mes (resultado vs meta).
2. Pipeline e capacidade de entrega.
3. Monetizacao e unidade economica.
4. Riscos estruturais e bloqueios.
5. Bets do proximo ciclo com owner + KPI.

## Registro da ata mensal

No `Cockpit Growth` deve ser registrado:

- Resumo executivo do ciclo.
- Decisoes estrategicas aprovadas.
- Riscos e bloqueios.
- Bets do proximo mes.
- Notas de budget/capacidade.

Persistencia: trilha auditavel em `GrowthMonthlyReview` (`monthly_review_recorded`).

## Criterio de qualidade da revisao

- 100% das decisoes com owner responsavel.
- Toda bet deve declarar KPI de sucesso e horizonte de medicao.
- Risco critico sem plano de mitigacao invalida a revisao.
- Ata publicada ate o 2o dia util do mes seguinte.
