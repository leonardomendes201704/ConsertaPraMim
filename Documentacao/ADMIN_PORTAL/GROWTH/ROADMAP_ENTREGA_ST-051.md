# ST-051 - Integracao roadmap no cockpit de growth

Status: Released
Epic: EPIC-020
Owner: Growth Operacional

## Objetivo

Conectar o `Cockpit Growth` com o backlog real do produto para transformar leitura de KPI em decisao de execucao.

## O que foi integrado

- Snapshot automatico do roadmap (`Backlog`, `In Progress`, `Done`) dentro do `Cockpit Growth`.
- Taxa de entrega exibida no cockpit com base no total de stories concluídas.
- Taxa de execucao ativa (stories em andamento) para leitura de capacidade.
- Lista priorizada de stories (em andamento + backlog critico) com progresso de tarefas.
- Atalho para o board completo (`Roadmap`) e detalhe de cada story na `Wiki`.

## Regra operacional

- KPI sem contexto de entrega nao fecha ciclo de decisao.
- Toda revisao semanal deve cruzar:
  1. North Star e guardrails;
  2. capacidade de execucao (stories em progresso);
  3. backlog critico sem owner/sem progresso.

## Saida esperada

- Reunioes de growth com decisao orientada por resultado + capacidade real de entrega.
- Menor risco de meta sem execucao operacional correspondente.
