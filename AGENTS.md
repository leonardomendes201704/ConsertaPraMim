# AGENTS.md

## Escopo

Estas diretrizes valem para todo o repositorio `ConsertaPraMimWeb`.

## Diretriz obrigatoria de changelog

1. Toda mudanca que altere comportamento funcional, fluxo de negocio, API, UI, configuracao operacional, deploy ou testes deve registrar entrada no changelog.
2. O changelog oficial da solution fica em:
   - `Documentacao/ADMIN_PORTAL/CHANGELOG/CHANGELOG.md`
3. A entrada deve ser adicionada em `## Unreleased`, seguindo o template ja definido no arquivo:
   - data + story/identificador
   - tipo (`feat|fix|refactor|docs|test`)
   - resumo objetivo
   - arquivos principais
   - risco/impacto

## Excecoes (quando NAO precisa registrar)

1. Ajuste puramente local de desenvolvimento sem impacto no repositorio final.
2. Mudanca exclusivamente cosmetica sem impacto funcional (ex.: espacos, formatacao automatica).

## Definicao de pronto (DoD)

Uma tarefa so deve ser considerada concluida quando:

1. codigo estiver implementado;
2. validacao/build/testes aplicaveis tiverem sido executados;
3. changelog em `Unreleased` estiver atualizado (quando aplicavel).
