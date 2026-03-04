# Manual QA/Operacao - Fundacao Google Calendar Sync (ST-011)

## 1. Objetivo

Padronizar configuracao, validacao e smoke test da integracao Google Calendar via Service Account para a trilha de agendamentos.

## 2. Escopo da ST-011

- Fundacao tecnica da integracao (`IGoogleCalendarService`).
- Validacao de startup para configuracoes obrigatorias quando integracao estiver habilitada.
- Conversao de janela UTC para timezone de negocio (`America/Sao_Paulo`) no payload enviado ao Google.
- Template de evento com titulo, descricao, local e metadados operacionais.

## 3. Pre-requisitos

- Projeto Google Cloud com `Google Calendar API` habilitada.
- Service Account criada no projeto Google.
- Chave privada da Service Account provisionada em secret manager/local secret.
- Calendario unico da operacao compartilhado com permissao de edicao para o e-mail da Service Account.

## 4. Configuracao da API

Seção `GoogleCalendarSync` em `ConsertaPraMim.API`:

```json
{
  "GoogleCalendarSync": {
    "Enabled": true,
    "ProjectId": "consertapramim-prod",
    "ServiceAccountEmail": "agenda-sync@consertapramim-prod.iam.gserviceaccount.com",
    "PrivateKey": "__SET_VIA_SECRET__",
    "CalendarId": "seu-calendario@group.calendar.google.com",
    "Timezone": "America/Sao_Paulo"
  }
}
```

Regras:
- Se `Enabled=false`, integracao fica desativada e nao bloqueia startup.
- Se `Enabled=true`, todos os campos obrigatorios precisam estar preenchidos.

## 5. Checklist QA (smoke)

- [ ] QA-GCAL-001: API sobe com `Enabled=false` e sem credenciais.
- [ ] QA-GCAL-002: API falha no startup com `Enabled=true` e `ProjectId` ausente.
- [ ] QA-GCAL-003: API falha no startup com `Enabled=true` e `CalendarId` ausente.
- [ ] QA-GCAL-004: API falha no startup com `Enabled=true` e timezone invalido.
- [ ] QA-GCAL-005: build da API conclui sem erro com pacote `Google.Apis.Calendar.v3`.
- [ ] QA-GCAL-006: testes unitarios `GoogleCalendar*` aprovados.

## 6. Procedimento operacional (Service Account + calendario unico)

1. Abrir Google Cloud Console e selecionar/criar projeto da integracao.
2. Ativar a API `Google Calendar API`.
3. Criar Service Account dedicada da integracao.
4. Gerar chave JSON da Service Account e armazenar em cofre de segredos.
5. Abrir Google Calendar e compartilhar o calendario unico com o e-mail da Service Account.
6. Conceder permissao `Make changes to events`.
7. Configurar `GoogleCalendarSync` na API via secrets/variaveis de ambiente.
8. Subir API e validar startup.

## 7. Troubleshooting

## 7.1 Startup bloqueado por validacao

- Conferir `GoogleCalendarSync:Enabled`.
- Se estiver `true`, validar se todos os campos obrigatorios foram informados.
- Confirmar formato do `Timezone` (`America/Sao_Paulo` recomendado).

## 7.2 Falha de autenticacao no Google

- Validar e-mail da Service Account.
- Confirmar chave privada correta (incluindo quebra de linha).
- Confirmar permissao de edicao no calendario compartilhado.

## 7.3 Evento criado com horario incorreto

- Verificar se janela de agendamento foi persistida em UTC no sistema.
- Verificar `GoogleCalendarSync:Timezone` configurado para `America/Sao_Paulo`.

## 8. Evolucao ST-012 (Task 1)

Estrutura de persistencia adicionada para trilha de sincronizacao:

- Tabela: `ServiceAppointmentCalendarSyncs`
- Campos principais: `AppointmentId`, `GoogleEventId`, `SyncStatus`, `LastSyncAtUtc`, `Error`
- Regra: `AppointmentId` unico por agendamento
- Regra: `GoogleEventId` unico quando informado

Validacao operacional:

- Confirmar migration `AddServiceAppointmentCalendarSync` aplicada no ambiente.
- Conferir criacao de indices `IX_ServiceAppointmentCalendarSyncs_AppointmentId` e `IX_ServiceAppointmentCalendarSyncs_SyncStatus_LastSyncAtUtc`.

## 9. Evolucao ST-012 (Task 2)

Integracao da trilha de sync na orquestracao de agendamento do chatbot:

- Sempre que `TelegramChatbotSchedulingService.ScheduleVisitsAsync` cria agendamento com sucesso, a API grava `ServiceAppointmentCalendarSync` com `SyncStatus=Pending`.
- Se o registro de sync ja existir para o `AppointmentId`, ele e atualizado para `Pending` e o campo `Error` e limpo.
- Se o agendamento falhar (`CreateAsync` sem sucesso), nenhum registro de sync e criado/atualizado para aquela visita.

Checklist operacional:

- [ ] QA-GCAL-012-001: agendar visita via endpoint batch do chatbot e validar linha em `ServiceAppointmentCalendarSyncs` para o `AppointmentId` retornado com `SyncStatus=Pending`.
- [ ] QA-GCAL-012-002: simular registro existente com `Failed` e repetir fluxo com mesmo `AppointmentId` (teste unitario/mocado), validando transicao para `Pending`.
- [ ] QA-GCAL-012-003: validar cenario de falha de criacao de agendamento (`slot_unavailable`) sem insercao indevida em `ServiceAppointmentCalendarSyncs`.

## 10. Evolucao ST-012 (Task 3)

Fluxo de criacao de evento Google Calendar apos agendamento local:

- Apos persistir o agendamento e marcar `Pending`, a API executa `CreateEventAsync` no Google Calendar.
- A chave de idempotencia e sempre derivada de `AppointmentId` no formato `cpm-apt-{guid_sem_hifen}`.
- Em sucesso de create:
  - `SyncStatus` -> `Synced`
  - `GoogleEventId` preenchido
  - `Error` limpo
  - `LastSyncAtUtc` atualizado
- Em falha de create:
  - `SyncStatus` -> `Failed`
  - `GoogleEventId` nao e marcado como sincronizado indevidamente
  - `Error` recebe trilha (`errorCode:errorMessage`) para reprocessamento

Checklist operacional:

- [ ] QA-GCAL-012-101: agendar visita e validar chamada create no Google com `IdempotencyKey=cpm-apt-{appointmentId:N}`.
- [ ] QA-GCAL-012-102: repetir create para mesma chave idempotente e validar ausencia de duplicacao no Google.
- [ ] QA-GCAL-012-103: simular falha de create no Google e validar `ServiceAppointmentCalendarSyncs.SyncStatus=Failed` + `Error` preenchido.
- [ ] QA-GCAL-012-104: simular create com sucesso e validar `ServiceAppointmentCalendarSyncs.SyncStatus=Synced` + `GoogleEventId` preenchido.

## 11. Evolucao ST-012 (Task 4)

Sincronizacao de update de evento quando reagendamento e aceito:

- No `RespondRescheduleAsync` (quando `Accept=true`), apos aplicar nova janela no agendamento, a API sincroniza o evento do Google Calendar.
- Se existir `GoogleEventId`, o fluxo usa `UpdateEventAsync`.
- Se `UpdateEventAsync` retornar `google_calendar_event_not_found`, o fluxo tenta `CreateEventAsync` com a mesma chave idempotente (`cpm-apt-{appointmentId}`) para recompor o evento.
- O sync de calendario transita para:
  - `Synced` quando update/create conclui com sucesso.
  - `Failed` quando update/create falha, com erro persistido para retry.

Checklist operacional:

- [ ] QA-GCAL-012-201: aceitar reagendamento e validar chamada de `UpdateEventAsync` com nova janela.
- [ ] QA-GCAL-012-202: simular `google_calendar_event_not_found` no update e validar fallback de create idempotente.
- [ ] QA-GCAL-012-203: simular erro de update e validar `SyncStatus=Failed` com trilha em `Error`.
- [ ] QA-GCAL-012-204: validar transicao de sync `Pending -> Synced` apos update bem-sucedido.

## 12. Evolucao ST-012 (Task 5)

Sincronizacao de delete de evento quando o agendamento e cancelado:

- No `ServiceAppointmentService.CancelAsync`, apos persistir cancelamento local e cancelar lembretes, a API executa sync de delete no Google Calendar.
- Se houver `GoogleEventId`, o fluxo chama `DeleteEventAsync(eventId)`:
  - sucesso -> `SyncStatus=Deleted`, `Error=null`, `LastSyncAtUtc` atualizado.
  - falha -> `SyncStatus=Failed`, `Error` preenchido com trilha (`errorCode:errorMessage`), sem quebrar o cancelamento local.
- Se nao existir registro de sync para o `AppointmentId`, a API cria `ServiceAppointmentCalendarSync` com `Deleted` para manter rastreabilidade.
- Se existir sync sem `GoogleEventId`, o registro e normalizado para `Deleted` sem chamada externa.

Checklist operacional:

- [ ] QA-GCAL-012-301: cancelar agendamento com `GoogleEventId` existente e validar chamada de `DeleteEventAsync`.
- [ ] QA-GCAL-012-302: validar transicao do sync para `Deleted` apos delete bem-sucedido.
- [ ] QA-GCAL-012-303: simular falha de delete e validar `SyncStatus=Failed` + `Error` preenchido.
- [ ] QA-GCAL-012-304: cancelar agendamento sem registro previo de sync e validar criacao de `ServiceAppointmentCalendarSync` com `Deleted`.

## 13. Evolucao ST-012 (Task 6)

Compensacao no fluxo de create/fallback:

- Se create no Google falhar (create inicial ou fallback apos `google_calendar_event_not_found`), o sync deve ficar em `Failed`.
- Nao e permitido manter `GoogleEventId` residual de tentativa anterior nesse cenario de falha de create/fallback.
- O erro deve ser persistido em `Error` para reprocessamento posterior.

Checklist operacional:

- [ ] QA-GCAL-012-401: simular falha de create inicial e validar `SyncStatus=Failed`, `GoogleEventId=null`, `Error` preenchido.
- [ ] QA-GCAL-012-402: simular update `event_not_found` + falha no fallback create e validar limpeza de `GoogleEventId`.

## 14. Evolucao ST-012 (Task 7)

Descricao padronizada de negocio no evento Google Calendar:

- Campos minimos no `Description`:
  - `Protocolo`
  - `Pedido`
  - `Agendamento`
  - `Cliente`
  - `Prestador`
  - `Categoria`
  - `Endereco`
  - `Motivo`

Checklist operacional:

- [ ] QA-GCAL-012-501: validar descricao no evento criado via chatbot contendo todos os campos de negocio.
- [ ] QA-GCAL-012-502: validar descricao no evento atualizado por reagendamento mantendo o mesmo padrao.

## 15. Evolucao ST-012 (Task 8)

Cobertura unitaria adicional da sincronizacao:

- Idempotencia por `IdempotencyKey` em create.
- Falha de create com compensacao (`Failed` + limpeza de `GoogleEventId`).
- Payload com descricao de negocio validado via asserts de conteudo.

Checklist operacional:

- [ ] QA-GCAL-012-601: executar testes unitarios focados de sync e validar green para sucesso/falha/idempotencia.

## 16. Evolucao ST-012 (Task 9)

Cobertura de integracao com cliente fake Google Calendar:

- `create`: batch de agendamento via chatbot gerando sync `Synced`.
- `update`: aceite de reagendamento chamando update no fake.
- `delete`: cancelamento chamando delete no fake e finalizando em `Deleted`.

Checklist operacional:

- [ ] QA-GCAL-012-701: fluxo de create em integracao com fake calendar.
- [ ] QA-GCAL-012-702: fluxo de update em integracao com fake calendar.
- [ ] QA-GCAL-012-703: fluxo de delete em integracao com fake calendar.
