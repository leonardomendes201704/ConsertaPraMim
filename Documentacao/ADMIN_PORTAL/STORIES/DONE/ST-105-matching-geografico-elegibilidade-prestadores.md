# ST-105 - Matching geografico e elegibilidade de prestadores

Status: Done
Epic: EPIC-JORNADA-001

## Objetivo

Encontrar apenas prestadores realmente elegiveis para cada caso com base em categoria, raio e status operacional.

## Criterios de aceite

- O sistema filtra por categoria e subcategoria.
- O sistema respeita raio de atendimento e localizacao do cliente.
- O sistema considera status operacional e capacidade minima.
- Prestadores fora do recorte nao sao disparados.

## Tasks

- [x] Revisar modelo de categoria e raio de atendimento do prestador.
- [x] Implementar consulta geoespacial por coordenada e raio.
- [x] Adicionar filtros por status, bloqueios e capacidade.
- [x] Criar ranking dos elegiveis.
- [x] Registrar trilha de quem foi elegivel e por que.
- [x] Cobrir regiao sem cobertura e categoria sem oferta suficiente.

## Entrega implementada

- Foi criado o `JourneyProviderMatchingService`, que le jornadas de `clientes` em `Agendamento confirmado`, carrega o snapshot atual da jornada e executa o matching geografico dos prestadores.
- Foi criado o `JourneyProviderMatchingWorker`, que roda periodicamente com base na configuracao `JourneyProviderMatching`.
- O matching considera categoria, subcategoria detectada, raio de atendimento, disponibilidade declarada, status operacional, pendencias de compliance, restricoes de confianca e conflito com outros atendimentos.
- O ranking dos elegiveis leva em conta distancia, nota, volume de avaliacoes, status operacional, confianca e aderencia da subcategoria.
- A tabela `dbo.cpm_web_journey_executions` agora persiste `MatchingStatus`, `MatchingSummary`, `MatchingRequestedCategory`, `MatchingRequestedSubcategory`, `MatchingEvaluatedProviders`, `MatchingEligibleProviders`, `MatchingCandidatesJson` e `MatchingLastRunAtUtc`.
- O modal do lead no Kanban ganhou a secao `Matching geografico`, exibindo status, resumo, contagens e lista dos prestadores avaliados com motivo de bloqueio ou elegibilidade.
- Quando o matching encontra candidatos elegiveis, a jornada avanca para `Em matching`; quando nao encontra cobertura suficiente, a jornada vai para `Sem match`.
- Foi adicionada cobertura de regressao para o motor de matching e para a persistencia SQL do snapshot da jornada.

## Validacao executada

- `dotnet build Backend\\src\\ConsertaPraMim.Web.CpmFull\\ConsertaPraMim.Web.CpmFull.csproj -p:UseAppHost=false -p:BaseIntermediateOutputPath=C:\\Users\\devcr\\AppData\\Local\\Temp\\codex-st105-cpm-obj\\ -p:MSBuildProjectExtensionsPath=C:\\Users\\devcr\\AppData\\Local\\Temp\\codex-st105-cpm-obj\\ -p:OutputPath=C:\\Users\\devcr\\AppData\\Local\\Temp\\codex-st105-cpm-out\\ -p:DefaultItemExcludes=obj/**`
- `dotnet test Backend\\tests\\ConsertaPraMim.Tests.Unit\\ConsertaPraMim.Tests.Unit.csproj --filter "(FullyQualifiedName~JourneyProviderMatchingServiceTests|FullyQualifiedName~SqlAdminKanbanServiceChatwootPersistenceTests)" -p:UseAppHost=false -p:UseSharedCompilation=false -p:BaseIntermediateOutputPath=C:\\Users\\devcr\\AppData\\Local\\Temp\\codex-st105-test-obj\\ -p:MSBuildProjectExtensionsPath=C:\\Users\\devcr\\AppData\\Local\\Temp\\codex-st105-test-obj\\ -p:OutputPath=C:\\Users\\devcr\\AppData\\Local\\Temp\\codex-st105-test-out\\ -p:DefaultItemExcludes=obj/** --logger "console;verbosity=minimal"`
- `git diff --check`
- Varredura de encoding nos arquivos tocados
