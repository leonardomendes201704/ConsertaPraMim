# ST-001 - Check-in de chegada e inicio de atendimento

Status: In Progress  
Epic: EPIC-001

## Objetivo

Registrar de forma confiavel quando o prestador chegou ao local e quando iniciou efetivamente a execucao, criando marco operacional e evidencias temporais para cliente e administracao.

## Criterios de aceite

- Prestador visualiza acao de check-in apenas em agendamentos `Confirmed` ou `RescheduleConfirmed`.
- Check-in grava `timestamp`, `latitude`, `longitude` e `accuracy` quando GPS estiver disponivel.
- Quando GPS estiver indisponivel, sistema exige motivo e grava check-in manual auditavel.
- Cliente recebe notificacao de "prestador chegou" em tempo real.
- Timeline do agendamento exibe eventos `Arrived` e `InProgress` com data/hora.
- API impede check-in duplicado para o mesmo agendamento.

## Tasks

- [x] Modelar novos campos em `ServiceAppointment` para chegada e inicio (`ArrivedAtUtc`, `StartedAtUtc`, coordenadas e metadados).
- [x] Criar migration e atualizar mapeamento EF para os novos campos.
- [x] Adicionar endpoint/API de check-in de chegada com validacao de estado.
- [x] Adicionar endpoint/API de inicio de atendimento com validacao de transicao.
- [x] Registrar eventos no historico do agendamento (`ServiceAppointmentHistory`) com metadata.
- [x] Publicar notificacao in-app e SignalR para cliente ao chegar/iniciar.
- [x] Implementar botao e feedback visual na agenda do prestador.
- [x] Exibir marco de chegada/inicio na tela de detalhes do cliente.
- [x] Cobrir regras com testes unitarios de transicao de status.
- [ ] Cobrir integracao da API com teste de concorrencia e idempotencia.
