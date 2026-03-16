# ST-106 - Motor de disparo em ondas para prestadores

Status: Done localmente
Epic: EPIC-JORNADA-001

## Objetivo

Disparar oportunidades em ondas controladas para maximizar aceite sem gerar spam para a base de prestadores.

## Criterios de aceite

- O sistema envia oportunidades em ondas configuraveis.
- O disparo para automaticamente quando houver aceite valido.
- O sistema registra expiracao, recusa e ausencia de resposta.

## Tasks

- [x] Criar entidades `ServiceDispatchWave` e `ServiceDispatchTarget`.
- [x] Definir tamanho e timeout de cada onda.
- [x] Parar ondas futuras quando o caso for reservado.
- [x] Criar fila de disparo com idempotencia por jornada, prestador e onda.
- [x] Medir aceite por onda e por categoria.
- [x] Cobrir corrida entre dois aceites quase simultaneos.

## Entrega implementada

- O `JourneyProviderDispatchService` passou a preparar ondas para jornadas de `clientes` em `Em matching` e a reabrir novas ondas quando a anterior expira sem aceite valido.
- O `JourneyProviderDispatchWorker` foi adicionado ao runtime do `ConsertaPraMim.Web.CpmFull` para processar automaticamente ondas, fila e expiracao.
- O snapshot da jornada agora persiste `DispatchStatus`, `DispatchSummary`, `DispatchStrategy`, contagens de alvos, ondas registradas, prazo de aceite e prestador reservado.
- Foi criada a tabela `dbo.cpm_web_journey_dispatch_queue`, com `TargetKey` idempotente e lifecycle de fila (`pending`, `processing`, `retrying`, `processed`, `dead_letter`).
- O modal do lead no Kanban ganhou a secao `Disparo em ondas`, exibindo estrategia, ondas, alvos e prestador reservado em PT-BR.
- A reserva do caso ficou protegida por `TryReserveJourneyDispatchTarget`, com lock pessimista na jornada para impedir dois aceites validos no mesmo lead.
- Quando nao ha mais prestadores elegiveis apos as ondas, a jornada e marcada como `Sem match`.

## Validacoes

- `dotnet build Backend\src\ConsertaPraMim.Web.CpmFull\ConsertaPraMim.Web.CpmFull.csproj -p:UseAppHost=false -p:BaseIntermediateOutputPath=C:\Users\devcr\AppData\Local\Temp\codex-st106-cpm-obj\ -p:MSBuildProjectExtensionsPath=C:\Users\devcr\AppData\Local\Temp\codex-st106-cpm-obj\ -p:OutputPath=C:\Users\devcr\AppData\Local\Temp\codex-st106-cpm-out\ -p:DefaultItemExcludes=obj/**`
- `dotnet test Backend\tests\ConsertaPraMim.Tests.Unit\ConsertaPraMim.Tests.Unit.csproj --filter "(FullyQualifiedName~JourneyProviderDispatchServiceTests|FullyQualifiedName~SqlAdminKanbanServiceChatwootPersistenceTests)" -p:UseAppHost=false -p:UseSharedCompilation=false -p:BaseIntermediateOutputPath=C:\Users\devcr\AppData\Local\Temp\codex-st106-test-obj\ -p:MSBuildProjectExtensionsPath=C:\Users\devcr\AppData\Local\Temp\codex-st106-test-obj\ -p:OutputPath=C:\Users\devcr\AppData\Local\Temp\codex-st106-test-out\ -p:DefaultItemExcludes=obj/** --logger "console;verbosity=minimal"`
- `git diff --check`
- varredura de encoding UTF-8 nos arquivos alterados

## Risco / Impacto

Medio. A entrega adiciona uma nova etapa automatizada critica da jornada do cliente, com fila local, expiracao temporal e reserva concorrente de caso, impactando diretamente o fluxo entre matching e conexao com prestadores.
