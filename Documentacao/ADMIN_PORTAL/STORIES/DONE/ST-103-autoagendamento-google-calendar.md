# ST-103 - Autoagendamento com Google Calendar

Status: Done
Epic: EPIC-JORNADA-001

## Objetivo

Permitir que o cliente receba e confirme uma janela de atendimento sem depender de operador humano.

## Criterios de aceite

- [x] O sistema sugere slots validos com base na agenda Google configurada.
- [x] O cliente confirma a janela no proprio canal.
- [x] O evento e criado ou atualizado no `Google Calendar`.
- [x] O `EventId` fica vinculado a jornada e ao Kanban.

## Tasks

- [x] Criar adaptador de leitura e escrita do `Google Calendar`.
- [x] Definir algoritmo de sugestao de janelas e regras de indisponibilidade.
- [x] Persistir `GoogleCalendarEventId`, horario e status da agenda.
- [x] Implementar reagendamento e cancelamento idempotentes.
- [x] Registrar eventos de agenda no historico do Kanban.
- [x] Cobrir conflitos, duplicidade de evento e indisponibilidade da agenda.

## Entrega implementada

- Foi criado o `JourneyGoogleCalendarGateway`, autenticado por `service account`, para consultar indisponibilidade via `freeBusy` e criar, atualizar ou cancelar eventos na agenda Google oficial.
- O `JourneySchedulingService` passou a sugerir janelas em horario comercial, respeitando duracao do atendimento, antecedencia minima, dias habilitados e janela maxima de busca.
- O `TelegramAutomationController` ganhou o endpoint interno `POST /api/integrations/telegram/automation/scheduling/turn`.
- O `TelegramBridge` passou a chamar essa trilha via `TelegramJourneySchedulingClient` durante o fluxo inbound do bot, priorizando a resposta de autoagendamento quando a jornada esta pronta para agenda.
- A jornada passou a persistir status de agendamento, slots sugeridos, janela confirmada, timestamps, `GoogleCalendarEventId` e `GoogleCalendarEventLink`.
- O modal do lead no Kanban ganhou a secao `Agendamento automatico`, com status, resumo, slots sugeridos e link para abrir o evento.
- Foram adicionados historicos operacionais em PT-BR para `agenda_janela_sugerida`, `agenda_confirmada`, `agenda_confirmacao_falhou`, `agenda_cancelada` e `agenda_sem_disponibilidade`.

## Validacao executada

- `dotnet build Backend\\src\\ConsertaPraMim.Web.CpmFull\\ConsertaPraMim.Web.CpmFull.csproj -p:UseAppHost=false -p:BaseIntermediateOutputPath=C:\\Users\\devcr\\AppData\\Local\\Temp\\codex-st103-cpm-obj\\ -p:MSBuildProjectExtensionsPath=C:\\Users\\devcr\\AppData\\Local\\Temp\\codex-st103-cpm-obj\\ -p:OutputPath=C:\\Users\\devcr\\AppData\\Local\\Temp\\codex-st103-cpm-out\\ -p:DefaultItemExcludes=obj/**`
- `dotnet build Backend\\src\\ConsertaPraMim.Web.TelegramBridge\\ConsertaPraMim.Web.TelegramBridge.csproj -p:UseAppHost=false -p:BaseIntermediateOutputPath=C:\\Users\\devcr\\AppData\\Local\\Temp\\codex-st103-bridge-obj\\ -p:MSBuildProjectExtensionsPath=C:\\Users\\devcr\\AppData\\Local\\Temp\\codex-st103-bridge-obj\\ -p:OutputPath=C:\\Users\\devcr\\AppData\\Local\\Temp\\codex-st103-bridge-out\\ -p:DefaultItemExcludes=obj/**`
- `dotnet test Backend\\tests\\ConsertaPraMim.Tests.Unit\\ConsertaPraMim.Tests.Unit.csproj --filter "(FullyQualifiedName~JourneySchedulingServiceTests|FullyQualifiedName~TelegramInboundUpdateProcessorTests|FullyQualifiedName~SqlAdminKanbanServiceChatwootPersistenceTests)" -p:UseAppHost=false -p:UseSharedCompilation=false -p:BaseIntermediateOutputPath=C:\\Users\\devcr\\AppData\\Local\\Temp\\codex-st103-test-obj\\ -p:MSBuildProjectExtensionsPath=C:\\Users\\devcr\\AppData\\Local\\Temp\\codex-st103-test-obj\\ -p:OutputPath=C:\\Users\\devcr\\AppData\\Local\\Temp\\codex-st103-test-out\\ -p:DefaultItemExcludes=obj/** --logger "console;verbosity=minimal"`
- `git diff --check`
- Varredura de encoding UTF-8 e caracteres quebrados nos arquivos tocados

## Observacoes

- O `dotnet test` retornou codigo `0`, mas a saida textual do runner continuou truncada pelo host local do Windows.
- A validacao do `ConsertaPraMim.API` completo continua limitada por erros pre-existentes de referencias e dependencias em `ConsertaPraMim.Application`, nao por esta story.

## Risco / impacto

- Medio. A story altera a jornada real do cliente no Telegram, grava eventos na agenda Google oficial e passa a persistir o estado de autoagendamento no Kanban.
