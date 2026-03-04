# ST-012 - Sync automatica de agendamento Google Calendar

Diagramas Mermaid da story ST-012.

- `fluxo-sync-agendamento-google-calendar.mmd`: fluxo da Task 2 para marcacao `Pending` apos criacao bem-sucedida do agendamento no chatbot.
- `sequencia-sync-agendamento-google-calendar.mmd`: sequencia de `CreateAsync` + upsert (`Add/Update`) com `CreateEventAsync` no Google e transicao `Synced/Failed`.
