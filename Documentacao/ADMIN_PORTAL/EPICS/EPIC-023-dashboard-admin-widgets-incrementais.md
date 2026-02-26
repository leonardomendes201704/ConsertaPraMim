# EPIC-023 - Widgets incrementais da home admin para analiticos e listas operacionais

Status: In Progress
Trilha: OPERACAO_ADMIN

## Objetivo

Desacoplar os widgets analiticos e operacionais restantes da home do portal admin, transformando cada bloco em componente independente com carregamento por endpoint dedicado, skeleton, spinner e falha localizada.

## Problema de negocio

- A home admin ainda depende de um snapshot unico para blocos analiticos relevantes como receita, reputacao, falhas de pagamento e eventos recentes.
- Um atraso ou erro em um subconjunto de dados compromete a percepcao de disponibilidade da tela inteira.
- Evoluir tabelas/listas especificas da home exige alterar uma view monolitica, elevando risco de regressao cruzada.
- Eventos recentes, reputacao e falhas de pagamento exigem leitura rapida e resiliente, mesmo quando outros widgets demoram mais.

## Resultado esperado

- Cada widget da home admin passa a ter componente proprio e endpoint dedicado.
- O portal exibe skeleton no boot, spinner no refresh e erro isolado por widget.
- O snapshot principal deixa de ser a fonte de verdade desses blocos, reduzindo acoplamento da home.
- A tela fica pronta para refresh seletivo, troubleshooting localizado e evolucao incremental por widget.

## Metricas de sucesso

- Tempo medio percebido para primeiro widget analitico renderizado.
- Percentual de widgets que permanecem operacionais quando outro endpoint falha.
- Reducao de regressao ao alterar widgets especificos da home admin.
- Menor tempo de manutencao para adicionar ou revisar um bloco analitico na home.

## Escopo

### Inclui

- Componentizacao dos widgets listados da home admin.
- Endpoints dedicados por widget no backend e proxies autenticados no portal.
- Skeleton/ghost, spinner e erro localizado por widget.
- Preservacao de filtros globais e filtros locais de eventos recentes.
- Atualizacao de manual QA, changelog e board.

### Nao inclui

- Reescrita do mapa operacional.
- Reescrita do painel de no-show ja componentizado.
- Alteracao de layout fora da home admin.

## Historias vinculadas

- ST-054 - Widgets analiticos incrementais na home admin.
