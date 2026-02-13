# Admin Portal Tracking

Este diretorio centraliza planejamento e execucao do Portal Admin.

## Estrutura

- `EPICS/`: visao macro de negocio.
- `STORIES/BACKLOG/`: historias ainda nao iniciadas.
- `STORIES/IN_PROGRESS/`: historias em andamento.
- `STORIES/DONE/`: historias concluídas.
- `CHANGELOG/`: registro cronologico das entregas.
- `TEMPLATES/`: modelos para novas historias e entradas.

## Fluxo de trabalho

1. Quando iniciar uma historia, mover o arquivo de `STORIES/BACKLOG/` para `STORIES/IN_PROGRESS/`.
2. Atualizar checkboxes das tasks dentro da propria historia.
3. Quando concluir, mover para `STORIES/DONE/`.
4. Registrar a entrega em `CHANGELOG/CHANGELOG.md`.

## Convencao de IDs

- Epic: `EPIC-001`
- Story: `ST-001`, `ST-002`, ...
- Task: checklist dentro da story (`- [ ]` / `- [x]`)

