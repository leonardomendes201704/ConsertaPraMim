# ST-101 - Intake omnichannel e maquina de estados da jornada de servico

Status: Done
Epic: EPIC-JORNADA-001

## Objetivo

Unificar a entrada por `landing/site`, `portal do cliente` e `Telegram` em uma unica jornada operacional de servico com estado persistido, auditavel e projetado no Kanban do CPM Full.

## Escopo entregue

- Foi criado um contrato unico de intake da jornada entre `API/Application`, `CPM Full` e a trilha do `Telegram`.
- O CPM Full passou a persistir `journey_executions` e `journey_events` para separar estado autonomo da jornada do card visual do Kanban.
- A deduplicacao passou a considerar `LandingLeadId`, `ServiceRequestId`, `ChatbotConversationId`, `TelegramChatId`, telefone, e-mail e janela temporal de 48 horas.
- A abertura de pedido pelo portal do cliente e a captura da landing agora sincronizam a mesma jornada no CPM Full.
- O detalhe do lead no Kanban ganhou a secao `Jornada automatica`.
- Historicos em PT-BR foram adicionados para criacao, atualizacao, reentrada omnichannel e vinculo de pedido.

## Criterios de aceite

- [x] A mesma jornada pode nascer da landing ou do Telegram.
- [x] Existe um estado persistido e auditavel da jornada, separado do canal de origem.
- [x] Existe deduplicacao minima por telefone, e-mail e identificadores de canal.
- [x] O card do Kanban passa a refletir a jornada, nao apenas a conversa.

## Tasks

- [x] Mapear os payloads da landing e do Telegram para um contrato unico `JourneyIntakeCommand`.
- [x] Criar agregado `ServiceJourneyExecution` com estado, canal de origem e trilha de eventos.
- [x] Definir estrategia de deduplicacao por telefone, e-mail, `TelegramChatId` e janela temporal.
- [x] Ligar a jornada ao `ServiceRequest` e ao card do Kanban.
- [x] Criar historico funcional em PT-BR para cada transicao automatica relevante.
- [x] Cobrir cenarios de reentrada do mesmo cliente em canais diferentes.

## Validacao executada

- `dotnet build Backend\src\ConsertaPraMim.Web.CpmFull\ConsertaPraMim.Web.CpmFull.csproj ...`
- `dotnet test Backend\tests\ConsertaPraMim.Tests.Unit\ConsertaPraMim.Tests.Unit.csproj --filter "FullyQualifiedName~TelegramLeadAutomationServiceTests|FullyQualifiedName~LandingLeadServiceTests|FullyQualifiedName~ServiceRequestServiceTests|FullyQualifiedName~SqlAdminKanbanServiceChatwootPersistenceTests" ...`
- `git diff --check`
- varredura de encoding UTF-8 e caracteres quebrados nos arquivos tocados

## Observacoes

- A validacao do `ConsertaPraMim.API` completo segue limitada por erros pre-existentes de referencias e dependencias no projeto `ConsertaPraMim.Application` do workspace atual.
- O `dotnet test` retornou codigo `0`, mas a saida textual do runner continuou truncada pelo host local do Windows.

