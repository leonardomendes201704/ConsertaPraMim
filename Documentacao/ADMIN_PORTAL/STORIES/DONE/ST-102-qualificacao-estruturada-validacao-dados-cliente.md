# ST-102 - Qualificacao estruturada e validacao de dados do cliente

Status: Done
Epic: EPIC-JORNADA-001

## Objetivo

Coletar e validar os dados minimos da solicitacao para chegar a categoria, endereco e contexto confiaveis antes do agendamento.

## Criterios de aceite

- O bot confirma categoria, endereco, cidade, CEP, telefone e contexto do problema.
- Dados obrigatorios e complementares ficam claramente separados.
- A qualificacao vira campos estruturados, nao apenas texto livre.
- Casos de baixa confianca seguem para excecao controlada.

## Tasks

- [x] Definir contrato de dados obrigatorios para seguir sem humano.
- [x] Implementar extracao assistida por IA com validacao deterministica.
- [x] Normalizar categoria e subcategoria usando catalogo interno.
- [x] Validar CEP/endereco e geocodificar latitude/longitude.
- [x] Persistir score de confianca da qualificacao.
- [x] Criar fallback de confirmacao quando a confianca estiver baixa.

## Entrega implementada

- Criado o `JourneyQualificationService` no CPM Full para centralizar a triagem estruturada da jornada.
- O contrato omnichannel passou a trafegar `problemDescription`, `street`, `neighborhood`, `state`, `postalCode`, `city`, `latitude` e `longitude`.
- A qualificacao agora combina classificacao deterministica por catalogo interno, geocodificacao por CEP, extracao opcional via OpenAI e score de confianca com limiar configuravel.
- O snapshot da qualificacao passou a ser salvo em `dbo.cpm_web_journey_executions`, com JSON completo e colunas resumidas para leitura operacional.
- O estado da jornada passou a refletir `dados_pendentes`, `confirmacao_necessaria` e `qualificacao_validada`.
- O modal do lead no Kanban passou a exibir a secao `Qualificacao estruturada` com contexto, categoria, endereco, score e campos faltantes.

## Validacao executada

- `dotnet build Backend\\src\\ConsertaPraMim.Web.CpmFull\\ConsertaPraMim.Web.CpmFull.csproj -p:UseAppHost=false -p:BaseIntermediateOutputPath=C:\\Users\\devcr\\AppData\\Local\\Temp\\codex-st102-cpm-obj\\ -p:MSBuildProjectExtensionsPath=C:\\Users\\devcr\\AppData\\Local\\Temp\\codex-st102-cpm-obj\\ -p:OutputPath=C:\\Users\\devcr\\AppData\\Local\\Temp\\codex-st102-cpm-out\\ -p:DefaultItemExcludes=obj/**`
- `dotnet build Backend\\src\\ConsertaPraMim.Web.TelegramBridge\\ConsertaPraMim.Web.TelegramBridge.csproj -p:UseAppHost=false -p:BaseIntermediateOutputPath=C:\\Users\\devcr\\AppData\\Local\\Temp\\codex-st102-bridge-obj\\ -p:MSBuildProjectExtensionsPath=C:\\Users\\devcr\\AppData\\Local\\Temp\\codex-st102-bridge-obj\\ -p:OutputPath=C:\\Users\\devcr\\AppData\\Local\\Temp\\codex-st102-bridge-out\\ -p:DefaultItemExcludes=obj/**`
- `dotnet test Backend\\tests\\ConsertaPraMim.Tests.Unit\\ConsertaPraMim.Tests.Unit.csproj --filter "(FullyQualifiedName~JourneyQualificationServiceTests|FullyQualifiedName~TelegramLeadAutomationServiceTests|FullyQualifiedName~LandingLeadServiceTests|FullyQualifiedName~ServiceRequestServiceTests|FullyQualifiedName~SqlAdminKanbanServiceChatwootPersistenceTests)" -p:UseAppHost=false -p:UseSharedCompilation=false -p:BaseIntermediateOutputPath=C:\\Users\\devcr\\AppData\\Local\\Temp\\codex-st102-test-obj\\ -p:MSBuildProjectExtensionsPath=C:\\Users\\devcr\\AppData\\Local\\Temp\\codex-st102-test-obj\\ -p:OutputPath=C:\\Users\\devcr\\AppData\\Local\\Temp\\codex-st102-test-out\\ -p:DefaultItemExcludes=obj/** --logger "console;verbosity=minimal"`
- `git diff --check`

## Risco / impacto

- Medio. A story altera o intake real da jornada, a leitura operacional no Kanban e a persistencia SQL do snapshot de qualificacao antes do autoagendamento.
