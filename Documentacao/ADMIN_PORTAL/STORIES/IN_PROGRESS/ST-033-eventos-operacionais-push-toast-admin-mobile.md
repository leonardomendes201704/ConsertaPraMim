# ST-033 - Eventos operacionais recentes + push/toast no app admin mobile

Status: In Progress
Epic: EPIC-011

## Objetivo

Permitir que o admin mobile acompanhe em tempo real eventos operacionais chave de cliente/prestador, com push notification, toast in-app e tela dedicada de "Eventos Recentes".

## Eventos cobertos

1. Cliente abriu um pedido.
2. Prestador enviou uma proposta.
3. Cliente novo.
4. Cliente fez login.
5. Prestador novo.
6. Prestador fez login.
7. Cliente aceitou a proposta.
8. Cliente agendou.

## Criterios de aceite

- Existe endpoint mobile admin para registrar/remover token push.
- Backend dispara push para admins nos 8 eventos acima.
- App admin exibe toast quando notificacao chega com app aberto.
- App admin possui aba/tela "Eventos Recentes" com lista dos eventos operacionais.
- Fonte dos eventos da aba mobile usa o mesmo contrato do dashboard admin.
- Eventos recentes no mobile atualizam por acao manual e por refresh periodico.

## Tasks

- [ ] Criar canal push admin (`/api/mobile/admin/push-devices`) e liberar `appKind=admin`.
- [ ] Implementar broadcaster de eventos operacionais para admins.
- [ ] Disparar eventos em `AuthService`, `ServiceRequestService`, `ProposalService` e `ServiceAppointmentService`.
- [ ] Expandir `AdminDashboardService` para incluir os 8 tipos de evento no `RecentEvents`.
- [ ] Adicionar push registration no app `conserta-pra-mim-admin app`.
- [ ] Implementar toast in-app para notificacao recebida em foreground.
- [ ] Criar aba/tela "Eventos Recentes" no app admin com lista e refresh.
- [ ] Validar build backend + build app admin e documentar resultado.
