# ST-012 - Sincronizacao automatica de agendamento com Google Calendar

Status: Backlog  
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

- [ ] Criar entidade de mapeamento `ServiceAppointmentCalendarSync` (`AppointmentId`, `GoogleEventId`, `SyncStatus`, `LastSyncAtUtc`, `Error`).
- [ ] Integrar sincronizacao na orquestracao de agendamento apos persistencia local bem-sucedida.
- [ ] Implementar fluxo `create event` com chave idempotente por `appointmentId`.
- [ ] Implementar fluxo `update event` para mudancas de data/horario/prestador.
- [ ] Implementar fluxo `delete event` no cancelamento do agendamento.
- [ ] Garantir compensacao: se create no Google falhar, nao marcar agendamento como sincronizado.
- [ ] Padronizar descricao do evento com dados de negocio (cliente, prestador, protocolo, endereco resumido).
- [ ] Criar testes unitarios para service de sincronizacao (success/failure/idempotencia).
- [ ] Criar testes de integracao com client fake para cenarios de create/update/delete.

