# ST-118 - Hotfix para novos cadastros aparecerem no topo do onboarding de prestadores

## Contexto

Os novos cadastros de `/cadastro-profissional` estavam sendo sincronizados corretamente para o board `/admin/funil/prestadores`, mas apareciam no fim da coluna `Novo cadastro`, reduzindo a visibilidade operacional dos registros mais recentes.

## Objetivo

Garantir que novos cards sincronizados e novos leads entrem no topo visual da coluna correspondente no Kanban.

## Criterios de aceite

1. Um cadastro publico novo deve aparecer acima dos cards antigos em `Novo cadastro`.
2. A ordenacao manual do Kanban continua funcionando apos drag-and-drop.
3. Existe regressao automatizada validando que o cadastro sincronizado mais recente ficou na primeira posicao da coluna.

## Entrega implementada

- O `SqlAdminKanbanService` passou a calcular `SortOrder` de insercao no topo para novos leads e novas projecoes sincronizadas.
- O sync de `professional_registrations` agora posiciona cadastros mais recentes acima dos antigos.
- Foi adicionada regressao automatizada garantindo que o novo cadastro sincronizado fique na primeira posicao da coluna `Novo cadastro`.

## Validacao executada

- `dotnet build Backend\src\ConsertaPraMim.Web.CpmFull\ConsertaPraMim.Web.CpmFull.csproj -p:UseAppHost=false -p:BaseIntermediateOutputPath=C:\Users\devcr\AppData\Local\Temp\codex-st118-cpm-obj\ -p:MSBuildProjectExtensionsPath=C:\Users\devcr\AppData\Local\Temp\codex-st118-cpm-obj\ -p:OutputPath=C:\Users\devcr\AppData\Local\Temp\codex-st118-cpm-out\ -p:DefaultItemExcludes=obj/**`
- `dotnet test Backend\tests\ConsertaPraMim.Tests.Unit\ConsertaPraMim.Tests.Unit.csproj --filter "FullyQualifiedName~SqlAdminKanbanServiceChatwootPersistenceTests" -p:UseAppHost=false -p:UseSharedCompilation=false -p:BaseIntermediateOutputPath=C:\Users\devcr\AppData\Local\Temp\codex-st118-test-obj\ -p:MSBuildProjectExtensionsPath=C:\Users\devcr\AppData\Local\Temp\codex-st118-test-obj\ -p:OutputPath=C:\Users\devcr\AppData\Local\Temp\codex-st118-test-out\ -p:DefaultItemExcludes=obj/** --logger "console;verbosity=minimal"`

## Risco/impacto

- Medio. A correcao altera a regra de ordenacao inicial de novos cards no Kanban.
