# ST-040 - Roadmap de produto no Portal Admin com visibilidade do backlog

Status: Done
Epic: EPIC-020

## Objetivo

Exibir no Portal Admin uma visao de roadmap baseada em epics/stories versionadas em markdown, permitindo leitura executiva do backlog e progresso por trilha.

## Criterios de aceite

- Menu do admin possui item `Roadmap`.
- Tela lista epics com status/trilha e progresso por stories.
- Tela exibe board de stories por status (Backlog, In Progress, Done).
- Filtros por texto, epic, trilha e status.
- Fonte dos dados vem de `Documentacao/ADMIN_PORTAL` sem cadastro manual no banco.
- Manual QA/Operacao atualizado com caso de teste da nova view.

## Tasks

- [x] Criar docs de backlog estrategico (epics e stories) para crescimento.
- [x] Implementar service no Web.Admin para leitura e parse de epics/stories markdown.
- [x] Implementar controller + view `Roadmap` com board e filtros.
- [x] Atualizar menu lateral com acesso ao roadmap.
- [x] Atualizar manual QA/Operacao com caso de teste e troubleshooting do roadmap.
- [x] Atualizar changelog e validar build do Web.Admin.
