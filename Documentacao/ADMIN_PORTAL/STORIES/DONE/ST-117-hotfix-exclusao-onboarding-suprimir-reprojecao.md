# ST-117 - Hotfix da exclusao operacional para suprimir reprojecao no onboarding

## Contexto

Depois da sincronizacao incremental do onboarding de prestadores, a exclusao de um card sincronizado no Kanban removia apenas `kanban_leads` e artefatos locais. No refresh seguinte, a mesma origem externa voltava a ser projetada e o card reaparecia.

## Objetivo

Garantir que a exclusao operacional de cards sincronizados suprima a reprojecao automatica da mesma `SourceKey`, sem exigir restart e sem apagar o registro bruto da origem.

## Criterios de aceite

1. Ao excluir um card vindo de `/cadastro-profissional`, ele nao pode reaparecer no proximo refresh do board.
2. A supressao da origem deve ser persistida em tabela propria do Kanban.
3. A solucao nao deve apagar automaticamente o registro bruto da origem publica.
4. Existe teste de regressao para o fluxo `sincroniza -> exclui -> refresh -> nao reaparece`.

## Entrega implementada

- O `SqlAdminKanbanService` passou a criar e usar `dbo.cpm_web_kanban_deleted_sources`.
- A exclusao operacional agora registra a `SourceKey` de cards sincronizados antes de remover o lead local.
- A reprojecao de `Solicitacao site #...`, `Cadastro profissional #...` e `Profissional ativo #...` passa a respeitar essa supressao.
- Foi adicionada regressao automatizada cobrindo o caso de onboarding de prestadores.

## Validacao executada

- `dotnet build Backend\src\ConsertaPraMim.Web.CpmFull\ConsertaPraMim.Web.CpmFull.csproj -p:UseAppHost=false -p:BaseIntermediateOutputPath=C:\Users\devcr\AppData\Local\Temp\codex-st117-cpm-obj\ -p:MSBuildProjectExtensionsPath=C:\Users\devcr\AppData\Local\Temp\codex-st117-cpm-obj\ -p:OutputPath=C:\Users\devcr\AppData\Local\Temp\codex-st117-cpm-out\ -p:DefaultItemExcludes=obj/**`
- `dotnet test Backend\tests\ConsertaPraMim.Tests.Unit\ConsertaPraMim.Tests.Unit.csproj --filter "FullyQualifiedName~SqlAdminKanbanServiceChatwootPersistenceTests" -p:UseAppHost=false -p:UseSharedCompilation=false -p:BaseIntermediateOutputPath=C:\Users\devcr\AppData\Local\Temp\codex-st117-test-obj\ -p:MSBuildProjectExtensionsPath=C:\Users\devcr\AppData\Local\Temp\codex-st117-test-obj\ -p:OutputPath=C:\Users\devcr\AppData\Local\Temp\codex-st117-test-out\ -p:DefaultItemExcludes=obj/** --logger "console;verbosity=minimal"`

## Risco/impacto

- Medio. A correcao altera a exclusao operacional do Kanban e a reprojecao de cards sincronizados por fonte externa.
