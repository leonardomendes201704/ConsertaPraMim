# EPIC-011 - Eventos operacionais, push e toasts no app admin mobile

Status: In Progress
Trilha: ADMIN_PORTAL

## Objetivo

Entregar sinalizacao operacional em tempo real no app admin mobile com push notifications, toasts in-app e feed de eventos recentes alinhado ao dashboard admin.

## Problema de negocio

- A operacao administrativa no mobile nao recebe alertas imediatos de eventos criticos.
- O admin depende de abrir telas manualmente para descobrir mudancas de operacao.
- Nao existe timeline mobile dedicada com os eventos mais importantes do fluxo cliente/prestador.

## Resultado esperado

- App admin recebe push para eventos operacionais chave.
- App admin mostra toast em foreground sem depender de refresh manual.
- App admin exibe tela "Eventos Recentes" com os mesmos eventos operacionais do dashboard.
- Backend consolida os eventos em fonte unificada para dashboard e app mobile.

## Metricas de sucesso

- 8 eventos operacionais publicados no backend e consumidos pelo app admin.
- Push/token admin registrado com sucesso no endpoint dedicado.
- Eventos recentes exibidos no app admin com atualizacao periodica e por push.

## Escopo

### Inclui

- Canal push dedicado para app admin (`/api/mobile/admin/push-devices`).
- Disparo de notificacao admin para eventos operacionais definidos.
- Evolucao do dashboard admin para incluir novos tipos de eventos recentes.
- Tela mobile "Eventos Recentes" com lista paginada/ordenada por data.
- Toast in-app no admin mobile quando evento chega em foreground.

### Nao inclui

- Centro de notificacoes completo com preferencias por usuario.
- Inbox persistente de notificacoes lidas/nao lidas no backend.
- Regras de escalonamento por severidade e on-call.

## Historias vinculadas

- ST-033 - Eventos operacionais recentes + push/toast no app admin mobile.
