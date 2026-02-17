# ST-011 - Refresh on enter nas telas com fetch automatico

Status: Done
Epic: EPIC-009

## Objetivo

Implementar politica de revalidacao de dados em toda navegacao relevante do app, para reduzir divergencia entre estado local e estado real da API.

## Criterios de aceite

- Toda entrada em `DASHBOARD` dispara revalidacao de pedidos do cliente.
- Toda entrada em `ORDERS` dispara revalidacao de pedidos do cliente.
- Toda entrada em `REQUEST_DETAILS` dispara recarga do detalhe do pedido selecionado.
- Toda entrada em `PROPOSAL_DETAILS` dispara recarga do detalhe da proposta selecionada.
- Fluxo de login/sessao restaurada depende da politica central de refresh (sem chamadas duplicadas ad hoc).
- Navegacao de retorno (`back`) reflete estado atualizado.

## Tasks

- [x] Criar token de visita de tela (`viewVisitToken`) no `App.tsx`.
- [x] Implementar efeito central de refresh por `currentView` + token de visita.
- [x] Remover fetches pontuais redundantes em handlers de navegacao e concentrar na politica de entrada de tela.
- [x] Manter retry manual em telas de erro, sem conflito com refresh automatico.
- [x] Validar build do app apos alteracoes.
- [x] Atualizar documentacao e diagramas Mermaid da funcionalidade.

## Arquivos impactados

### App

- `conserta-pra-mim app/App.tsx`

### Documentacao

- `Documentacao/CONSUMER_APP_WEB/EPICS/EPIC-009-revalidacao-dados-a-cada-navegacao.md`
- `Documentacao/CONSUMER_APP_WEB/STORIES/DONE/ST-011-refresh-on-enter-telas-com-fetch-automatico.md`
- `Documentacao/CONSUMER_APP_WEB/INDEX.md`
- `Documentacao/CONSUMER_APP_WEB/DOCUMENTACAO_COMPLETA_CONSERTA_PRA_MIM_APP.md`
- `Documentacao/CONSUMER_APP_WEB/CHECKLIST_QA_E2E_CONSERTA_PRA_MIM_APP.md`
- `Documentacao/DIAGRAMAS/INDEX.md`
- `Documentacao/DIAGRAMAS/CONSUMER_APP_WEB/APP-011-refresh-on-enter/fluxo-refresh-on-enter.mmd`
- `Documentacao/DIAGRAMAS/CONSUMER_APP_WEB/APP-011-refresh-on-enter/sequencia-refresh-on-enter.mmd`

## Diagramas

- Fluxo:
  - `Documentacao/DIAGRAMAS/CONSUMER_APP_WEB/APP-011-refresh-on-enter/fluxo-refresh-on-enter.mmd`
- Sequencia:
  - `Documentacao/DIAGRAMAS/CONSUMER_APP_WEB/APP-011-refresh-on-enter/sequencia-refresh-on-enter.mmd`
