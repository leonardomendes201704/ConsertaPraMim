# ST-110 - Excecoes, handoff minimo, observabilidade e rollout da jornada

Status: Backlog
Epic: EPIC-JORNADA-001

## Objetivo

Controlar excecoes, medir a automacao e garantir rollout seguro da jornada autonoma de servico.

## Criterios de aceite

- Existe criterio claro para handoff humano.
- A operacao enxerga gargalos, sem match, falhas de agenda e falhas de disparo.
- A jornada pode ser ativada por feature flag e por canal.
- Existe plano de rollback e rollout incremental.

## Tasks

- [ ] Definir matriz de excecoes e gatilhos de handoff.
- [ ] Criar painel operacional e gerencial da jornada autonoma.
- [ ] Medir conversao por canal, categoria, regiao, onda e etapa.
- [ ] Adicionar feature flags por canal e por etapa da automacao.
- [ ] Criar runbook de rollout, fallback e troubleshooting.
- [ ] Cobrir homologacao ponta a ponta com agenda, dispatch e reviews.
