# ST-104 - Kanban autonomo e timers operacionais da jornada

Status: Done
Epic: EPIC-JORNADA-001

## Objetivo

Fazer o card do cliente caminhar sozinho pelas etapas do Kanban com base em eventos reais e timers operacionais.

## Criterios de aceite

- O card muda de etapa automaticamente conforme a jornada progride.
- Timers vencidos geram acao automatica ou excecao.
- A operacao entende o motivo de cada mudanca de etapa.
- O modal do lead exibe motivo, origem e timer da ultima automacao.
- Transicoes repetidas ou fora de ordem nao geram spam de historico.

## Tasks

- [x] Criar matriz de transicoes automatizadas do Kanban da jornada.
- [x] Implementar worker ou orquestrador de transicao automatica.
- [x] Persistir motivo e origem de cada mudanca de etapa.
- [x] Criar timers para `dados pendentes`, `agenda pendente`, `aceite pendente` e `avaliacoes pendentes`.
- [x] Cobrir idempotencia para transicoes repetidas ou fora de ordem.

## Entrega implementada

- O funil `clientes` passou a usar a trilha completa de etapas da jornada autonoma, com migracao idempotente dos nomes legados no seed do Kanban.
- Foi criado o `JourneyStageAutomationService`, que le os candidatos em `journey_executions`, aplica a matriz de transicoes e grava a automacao de etapa no mesmo agregado da jornada.
- Foi criado o `JourneyStageAutomationWorker`, que executa periodicamente a automacao do Kanban com base nas opcoes `JourneyStageAutomation`.
- A tabela `dbo.cpm_web_journey_executions` agora persiste `LastStageAutomationReason`, `LastStageAutomationOrigin`, `LastStageAutomationAtUtc`, `ActiveTimerCode` e `ActiveTimerDueAtUtc`.
- O modal do lead agora mostra a secao operacional da automacao com motivo, origem, horario da ultima transicao e timer ativo.
- Timers operacionais foram implementados para:
  - `dados pendentes`
  - `confirmacao da agenda`
  - `aceite do prestador`
  - `avaliacao do cliente`
  - `avaliacao do prestador`
- Quando um timer vence, a jornada pode escalar automaticamente para `Excecao operacional`, `Sem match`, `Aguardando avaliacao do prestador` ou `Concluido`, conforme a etapa corrente.

## Validacao executada

- `dotnet build Backend\\src\\ConsertaPraMim.Web.CpmFull\\ConsertaPraMim.Web.CpmFull.csproj -p:UseAppHost=false -p:BaseIntermediateOutputPath=C:\\Users\\devcr\\AppData\\Local\\Temp\\codex-st104-cpm-obj\\ -p:MSBuildProjectExtensionsPath=C:\\Users\\devcr\\AppData\\Local\\Temp\\codex-st104-cpm-obj\\ -p:OutputPath=C:\\Users\\devcr\\AppData\\Local\\Temp\\codex-st104-cpm-out\\ -p:DefaultItemExcludes=obj/**`
- `dotnet test Backend\\tests\\ConsertaPraMim.Tests.Unit\\ConsertaPraMim.Tests.Unit.csproj --filter "(FullyQualifiedName~JourneyStageAutomationServiceTests|FullyQualifiedName~SqlAdminKanbanServiceChatwootPersistenceTests)" -p:UseAppHost=false -p:UseSharedCompilation=false -p:BaseIntermediateOutputPath='C:\\Users\\devcr\\AppData\\Local\\Temp\\codex-st104-test-obj\\$(MSBuildProjectName)\\' -p:MSBuildProjectExtensionsPath='C:\\Users\\devcr\\AppData\\Local\\Temp\\codex-st104-test-obj\\$(MSBuildProjectName)\\' -p:OutputPath='C:\\Users\\devcr\\AppData\\Local\\Temp\\codex-st104-test-out\\$(MSBuildProjectName)\\' -p:DefaultItemExcludes=obj/** --logger "console;verbosity=minimal"`
- `git diff --check`
- Varredura de encoding nos arquivos tocados
