# ST-116 - Hotfix da sincronizacao do cadastro profissional no onboarding de prestadores

## Contexto

Durante o teste real do formulario publico `/cadastro-profissional`, novos prestadores eram gravados em `dbo.cpm_web_professional_registrations`, mas nao apareciam no board `/admin/funil/prestadores` quando o `SqlAdminKanbanService` ja havia sido inicializado anteriormente.

## Objetivo

Garantir que o onboarding de prestadores reflita novos cadastros publicos sem depender de restart da aplicacao.

## Criterios de aceite

1. Depois de abrir o board `prestadores`, um novo cadastro publico ainda deve aparecer ao recarregar a pagina.
2. O novo card deve entrar na coluna `Novo cadastro`.
3. A projecao deve continuar idempotente, sem duplicar o mesmo `Cadastro profissional #<id>`.
4. Existe teste de regressao para o cenario `board inicializado -> novo cadastro publico -> refresh do board`.

## Entrega implementada

- O `SqlAdminKanbanService` passou a sincronizar as fontes externas do board sempre que `GetBoard` e executado.
- Para `prestadores`, a leitura agora reprocessa `professional_registrations` e `professionals` em transacao curta e com lock interno de sincronizacao.
- Foi adicionada regressao automatizada cobrindo o caso em que o admin abre o board antes do cadastro publico acontecer.

## Validacao executada

- `dotnet build Backend\src\ConsertaPraMim.Web.CpmFull\ConsertaPraMim.Web.CpmFull.csproj -p:UseAppHost=false -p:BaseIntermediateOutputPath=C:\Users\devcr\AppData\Local\Temp\codex-st116-cpm-obj\ -p:MSBuildProjectExtensionsPath=C:\Users\devcr\AppData\Local\Temp\codex-st116-cpm-obj\ -p:OutputPath=C:\Users\devcr\AppData\Local\Temp\codex-st116-cpm-out\ -p:DefaultItemExcludes=obj/**`
- `dotnet test Backend\tests\ConsertaPraMim.Tests.Unit\ConsertaPraMim.Tests.Unit.csproj --filter "FullyQualifiedName~SqlAdminKanbanServiceChatwootPersistenceTests" -p:UseAppHost=false -p:UseSharedCompilation=false -p:BaseIntermediateOutputPath=C:\Users\devcr\AppData\Local\Temp\codex-st116-test-obj\ -p:MSBuildProjectExtensionsPath=C:\Users\devcr\AppData\Local\Temp\codex-st116-test-obj\ -p:OutputPath=C:\Users\devcr\AppData\Local\Temp\codex-st116-test-out\ -p:DefaultItemExcludes=obj/** --logger "console;verbosity=minimal"`

## Risco/impacto

- Medio. A correcao altera a sincronizacao do board de prestadores e faz a projecao do onboarding acontecer em toda leitura do Kanban.
