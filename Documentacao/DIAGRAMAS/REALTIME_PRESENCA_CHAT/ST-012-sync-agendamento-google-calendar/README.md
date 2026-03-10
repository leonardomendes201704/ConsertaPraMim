# ST-012 - Sync automatica de agendamento Google Calendar

Diagramas Mermaid da story ST-012.

- `fluxo-sync-agendamento-google-calendar.mmd`: fluxo da Task 2 para marcacao `Pending` apos criacao bem-sucedida do agendamento no chatbot.
- `sequencia-sync-agendamento-google-calendar.mmd`: sequencia de create no agendamento inicial (Task 3), update/fallback create no reagendamento aceito (Task 4) e delete no cancelamento (Task 5), com transicoes `Synced/Failed/Deleted`.

Atualizacoes recentes da ST-012:

- Task 6: compensacao para falha de create/fallback com limpeza de `GoogleEventId` residual.
- Task 7: descricao de evento padronizada com contexto de negocio (cliente, prestador, protocolo, endereco).
- Task 8: cobertura unitaria ampliada para sucesso/falha/idempotencia.
- Task 9: cobertura de integracao com fake Google client para create/update/delete.
