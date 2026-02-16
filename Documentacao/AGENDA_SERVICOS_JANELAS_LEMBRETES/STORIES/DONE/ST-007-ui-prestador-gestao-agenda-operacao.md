# ST-007 - UI do prestador para gerir agenda e responder solicitacoes

Status: In Progress  
Epic: EPIC-001

## Objetivo

Dar ao prestador controle operacional da agenda: disponibilidade, bloqueios, confirmacao de solicitacoes e acao rapida de reagendamento/cancelamento.

## Criterios de aceite

- Prestador configura disponibilidade recorrente por dia da semana.
- Prestador cria bloqueios pontuais de agenda.
- Prestador visualiza solicitacoes pendentes com SLA restante.
- Prestador confirma/recusa agendamento e responde propostas de reagendamento.
- Prestador visualiza calendario consolidado dos atendimentos.

## Tasks

- [x] Implementar tela de configuracao de disponibilidade recorrente.
- [x] Implementar tela/modal de bloqueios pontuais.
- [x] Implementar inbox de solicitacoes pendentes com prioridade por SLA.
- [x] Implementar acoes rapidas: confirmar, recusar, propor novo horario.
- [x] Implementar calendario semanal/mensal de atendimentos.
- [x] Exibir historico e motivo de alteracoes do agendamento.
- [x] Integrar notificacoes em tempo real para novas solicitacoes.
- [x] Cobrir cenarios com testes de interface e testes manuais guiados.

## Evidencias de validacao

- Teste automatizado de integracao real-time: `ServiceAppointmentRealtimeIntegrationTests.CreateAsync_ShouldBroadcastRealtimeNotificationForPendingProviderConfirmation`.
- Cobertura tecnica de regressao: fluxo de status operacional real-time mantido em `ServiceAppointmentRealtimeIntegrationTests.UpdateOperationalStatusAsync_ShouldPersistAndBroadcastRealtimeNotification`.
- Teste manual guiado (prestador):
  1. Abrir `Minha Agenda` no portal do prestador e manter a tela ativa.
  2. Em outra sessao (cliente), criar solicitacao de agendamento para o mesmo prestador.
  3. Validar toast real-time global + banner da agenda indicando novo pendente.
  4. Confirmar recarregamento automatico da agenda em ate 3 segundos.
  5. Verificar que o card entra em `Aguardando confirmacao` com SLA e acoes rapidas.

## Diagramas

- Fluxo: `Documentacao/DIAGRAMAS/AGENDA_SERVICOS_JANELAS_LEMBRETES/ST-007-ui-prestador-gestao-agenda-operacao/fluxo-inbox-sla-acoes-rapidas.mmd`
- Sequencia: `Documentacao/DIAGRAMAS/AGENDA_SERVICOS_JANELAS_LEMBRETES/ST-007-ui-prestador-gestao-agenda-operacao/sequencia-inbox-sla-acoes-rapidas.mmd`
