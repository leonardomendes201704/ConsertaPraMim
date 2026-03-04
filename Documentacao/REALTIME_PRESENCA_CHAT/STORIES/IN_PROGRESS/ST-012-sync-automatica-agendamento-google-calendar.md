# ST-012 - Sincronizacao automatica de agendamento com Google Calendar

Status: In Progress  
Epic: EPIC-003

## Objetivo

Garantir que agendamentos confirmados, atualizados e cancelados no sistema sejam refletidos automaticamente no Google Calendar, com idempotencia e consistencia.

## Criterios de aceite

- Criacao de agendamento confirmado gera evento no Google Calendar.
- Reagendamento atualiza o mesmo evento no Google Calendar.
- Cancelamento remove evento correspondente do Google Calendar.
- Nao ocorre duplicacao para o mesmo `appointmentId`.
- Em falha de sincronizacao, sistema mantem rastreabilidade e retry sem perder evento.

## Tasks

- [x] Criar entidade de mapeamento `ServiceAppointmentCalendarSync` (`AppointmentId`, `GoogleEventId`, `SyncStatus`, `LastSyncAtUtc`, `Error`).
- [x] Integrar sincronizacao na orquestracao de agendamento apos persistencia local bem-sucedida.
- [x] Implementar fluxo `create event` com chave idempotente por `appointmentId`.
- [x] Implementar fluxo `update event` para mudancas de data/horario/prestador.
- [ ] Implementar fluxo `delete event` no cancelamento do agendamento.
- [ ] Garantir compensacao: se create no Google falhar, nao marcar agendamento como sincronizado.
- [ ] Padronizar descricao do evento com dados de negocio (cliente, prestador, protocolo, endereco resumido).
- [ ] Criar testes unitarios para service de sincronizacao (success/failure/idempotencia).
- [ ] Criar testes de integracao com client fake para cenarios de create/update/delete.

## Progresso atual

- Entidade `ServiceAppointmentCalendarSync` criada no dominio com status de sincronizacao e trilha de erro.
- Repositorio dedicado criado para leitura/escrita do mapeamento por `AppointmentId` e `GoogleEventId`.
- `DbContext` atualizado com `DbSet`, relacionamentos, indices e constraints de consistencia.
- Migration `AddServiceAppointmentCalendarSync` gerada para provisionar a tabela no banco.
- `TelegramChatbotSchedulingService` passou a criar/atualizar registro de sync como `Pending` imediatamente apos `CreateAsync` de agendamento concluido com sucesso.
- Cobertura unitaria adicionada para garantir `AddAsync` (novo registro) e `UpdateAsync` (registro existente) no repositorio de sincronizacao.
- Fluxo de `create event` integrado ao agendamento: apos `Pending`, o servico chama `IGoogleCalendarService.CreateEventAsync` com `IdempotencyKey` derivada de `appointmentId` (`cpm-apt-{guid}`).
- Em sucesso, sync passa para `Synced` com `GoogleEventId`; em falha de create, sync passa para `Failed` com trilha de erro para reprocessamento.
- `ServiceAppointmentService.RespondRescheduleAsync` passou a sincronizar atualizacao de evento no Google Calendar ao aceitar reagendamento (nova data/horario).
- Quando existe `GoogleEventId`, a integracao executa `UpdateEventAsync`; se o evento nao for encontrado, o fluxo reconstroi com `CreateEventAsync` usando a mesma chave idempotente.
- Em update/create bem-sucedido apos reagendamento, sync fica `Synced`; em falha, sync fica `Failed` com erro persistido para reprocessamento.
