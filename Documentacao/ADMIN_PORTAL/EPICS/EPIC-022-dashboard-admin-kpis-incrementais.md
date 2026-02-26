# EPIC-022 - Dashboard admin incremental por KPI e componentes independentes

Status: Done
Trilha: OPERACAO_ADMIN

## Objetivo

Evoluir a home do portal admin para um modelo modular, no qual cada KPI seja carregado e atualizado de forma independente, com componentes reutilizaveis, feedback visual de carregamento e menor acoplamento entre as secoes do dashboard.

## Problema de negocio

- A home admin hoje depende de um snapshot unico, o que atrasa a percepcao de valor quando apenas alguns KPIs demoram mais para responder.
- O operador nao tem feedback granular de carregamento e interpreta facilmente a tela como travada ou incompleta.
- Qualquer evolucao de KPI exige editar uma view monolitica, aumentando risco de regressao e custo de manutencao.
- Nao existe separacao clara entre KPIs executivos da operacao geral e KPIs operacionais de no-show.

## Resultado esperado

- Cada KPI da home passa a ser um componente isolado e reutilizavel.
- O portal exibe skeleton/ghost e spinner por KPI, sem bloquear toda a pagina.
- Cada card busca seus dados em endpoint dedicado, preservando filtros globais.
- A tela fica preparada para refresh seletivo e futura priorizacao de KPIs criticos.

## Metricas de sucesso

- Tempo medio percebido para primeiro KPI visivel apos abrir a home.
- Percentual de cards que continuam funcionais mesmo quando outro endpoint falha.
- Tempo medio de manutencao para adicionar/editar um KPI da home.
- Reducao de regressao cruzada em cards nao relacionados.

## Escopo

### Inclui

- Componentizacao dos KPIs da home admin.
- Endpoints dedicados por KPI para dashboard geral e painel de no-show.
- Skeleton/ghost, spinner e estados de erro por card.
- Reaproveitamento de filtros globais no carregamento individual.
- Atualizacao de QA/manual/changelog para o novo comportamento.

### Nao inclui

- Reescrita completa das tabelas e graficos secundarios da home.
- Alteracao de layout dos modulos fora da home admin.
- Cache distribuido ou otimizado de longa duracao nesta fase.

## Historias vinculadas

- ST-053 - Home admin com KPIs modulares e carregamento incremental.
