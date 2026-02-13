# Realtime Presenca e Chat Tracking

Este diretorio centraliza o planejamento das evolucoes de tempo real:

- Status operacional do prestador (Ausente, Online, EmAtendimento).
- Confirmacao de leitura no chat (estilo WhatsApp).

## Estrutura

- `EPICS/`: visao macro de negocio.
- `STORIES/BACKLOG/`: historias ainda nao iniciadas.
- `STORIES/IN_PROGRESS/`: historias em andamento.
- `STORIES/DONE/`: historias concluidas.

## Fluxo de trabalho

1. Ao iniciar uma historia, mover de `STORIES/BACKLOG/` para `STORIES/IN_PROGRESS/`.
2. Atualizar checkboxes das tasks na propria historia.
3. Ao concluir, mover para `STORIES/DONE/`.
4. Registrar o que foi entregue no changelog principal do projeto.

## Convencao de IDs

- Epic: `EPIC-001`
- Story: `ST-001`, `ST-002`, ...
- Task: checklist dentro da story (`- [ ]` / `- [x]`)
