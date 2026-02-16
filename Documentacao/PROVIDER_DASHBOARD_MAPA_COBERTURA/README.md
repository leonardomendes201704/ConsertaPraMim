# Mapa de Cobertura na Dashboard do Prestador

Este diretorio centraliza o planejamento e evolucao do mapa de cobertura do prestador, com:

- visualizacao do raio de interesse;
- pins de pedidos proximos dentro e fora do raio;
- atualizacao de mapa na dashboard com dados em tempo real.

## Estrutura

- `EPICS/`: visao macro de negocio.
- `STORIES/BACKLOG/`: historias ainda nao iniciadas.
- `STORIES/IN_PROGRESS/`: historias em andamento.
- `STORIES/DONE/`: historias concluidas.

## Fluxo de trabalho

1. Ao iniciar uma historia, mover de `STORIES/BACKLOG/` para `STORIES/IN_PROGRESS/`.
2. Atualizar checkboxes das tasks na propria historia.
3. Ao concluir, mover para `STORIES/DONE/`.
4. Registrar entregas relevantes no changelog principal do projeto.
5. Criar/atualizar diagramas Mermaid (`fluxo` e `sequencia`) em `Documentacao/DIAGRAMAS/` e versionar no mesmo commit da funcionalidade.

## Convencao de IDs

- Epic: `EPIC-001`
- Story: `ST-001`, `ST-002`, ...
- Task: checklist dentro da story (`- [ ]` / `- [x]`)
