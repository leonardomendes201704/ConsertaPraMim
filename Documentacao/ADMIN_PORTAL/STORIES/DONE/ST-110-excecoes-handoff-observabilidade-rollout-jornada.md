# ST-110 - Excecoes, handoff minimo, observabilidade e rollout da jornada

Status: Done
Epic: EPIC-JORNADA-001

## Objetivo

Controlar excecoes, medir a automacao e garantir rollout seguro da jornada autonoma de servico.

## Criterios de aceite

- Existe criterio claro para handoff humano.
- A operacao enxerga gargalos, sem match, falhas de agenda e falhas de disparo.
- A jornada pode ser ativada por feature flag e por canal.
- Existe plano de rollback e rollout incremental.

## Tasks

- [x] Definir matriz de excecoes e gatilhos de handoff.
- [x] Criar painel operacional e gerencial da jornada autonoma.
- [x] Medir conversao por canal, categoria, regiao, onda e etapa.
- [x] Adicionar feature flags por canal e por etapa da automacao.
- [x] Criar runbook de rollout, fallback e troubleshooting.
- [x] Cobrir homologacao ponta a ponta com agenda, dispatch e reviews.

## Entrega implementada

- Foi criado o `JourneyGovernanceService` com rollout percentual, allowlist de canais e feature flags por etapa da jornada.
- O `JourneyAutomationService` passou a aplicar governanca logo no intake, permitindo rollout controlado e bloqueio por canal sem quebrar a captura base do lead.
- `JourneyStageAutomationService`, `JourneyProviderMatchingService`, `JourneyProviderDispatchService` e `JourneyServiceClosureService` passaram a respeitar as flags da governanca antes de executar automacoes.
- Foi formalizada uma matriz padrao de excecoes operacionais, com roteamento coerente para `Excecao operacional` e handoff minimo em casos de timeout, falta de dados, contestacao e desfecho excepcional.
- O `Portal Admin` ganhou o novo painel `/admin/jornada/painel`, com drawer `Filtros`, cards de resumo, backlog por etapa, breakdown por canal/categoria/cidade/onda e snapshot da governanca ativa.
- O snapshot da jornada e o painel passaram a destacar atrasos, jornadas sem match, falhas de agenda, falhas de disparo e principais motivos de excecao para operacao e rollout.
- O manual operacional foi atualizado com checklists de rollout, rollback, homologacao ponta a ponta e troubleshooting da trilha.

## Validacao local

1. Executar build do `ConsertaPraMim.Web.CpmFull`.
2. Executar testes focados em governanca, painel e servicos da jornada.
3. Abrir `/admin/jornada/painel` e validar drawer de filtros, cards, tabelas e bloco `Governanca ativa`.
4. Simular desabilitacao de etapa em `JourneyGovernance` e confirmar que a automacao correspondente deixa de executar sem derrubar o restante do fluxo.
5. Simular excecoes da jornada e validar roteamento para `Excecao operacional` com motivo auditavel.

## Resultado

- A `EPIC-JORNADA-001` ficou fechada com governanca, observabilidade e trilha de rollout operacional documentadas.
