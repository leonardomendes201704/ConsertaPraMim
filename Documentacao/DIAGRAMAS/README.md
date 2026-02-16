# Diretriz de Diagramas Mermaid

Este diretorio centraliza os diagramas tecnicos de fluxo e sequencia por funcionalidade.

## Regra obrigatoria

Toda funcionalidade nova ou alterada deve atualizar os diagramas Mermaid no mesmo ciclo de entrega.

## Estrutura padrao

- `INDEX.md`: catalogo de todos os diagramas versionados.
- `<TRILHA>/<STORY-ID>-<slug>/`
- `fluxo-<contexto>.mmd`
- `sequencia-<contexto>.mmd`

Exemplo:

- `Documentacao/DIAGRAMAS/ADMIN_PORTAL/ST-017-aplicacao-creditos-mensalidade-visibilidade/fluxo-credito-mensalidade.mmd`
- `Documentacao/DIAGRAMAS/ADMIN_PORTAL/ST-017-aplicacao-creditos-mensalidade-visibilidade/sequencia-credito-mensalidade.mmd`

## Checklist obrigatorio por entrega

- Criar/atualizar `fluxo-*.mmd`.
- Criar/atualizar `sequencia-*.mmd`.
- Atualizar `Documentacao/DIAGRAMAS/INDEX.md`.
- Referenciar os diagramas na story correspondente.

