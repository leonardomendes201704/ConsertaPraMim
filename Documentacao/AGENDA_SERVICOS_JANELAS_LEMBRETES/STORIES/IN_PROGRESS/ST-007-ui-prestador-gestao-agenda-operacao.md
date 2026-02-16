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
- [ ] Exibir historico e motivo de alteracoes do agendamento.
- [ ] Integrar notificacoes em tempo real para novas solicitacoes.
- [ ] Cobrir cenarios com testes de interface e testes manuais guiados.

## Diagramas

- Fluxo: `Documentacao/DIAGRAMAS/AGENDA_SERVICOS_JANELAS_LEMBRETES/ST-007-ui-prestador-gestao-agenda-operacao/fluxo-inbox-sla-acoes-rapidas.mmd`
- Sequencia: `Documentacao/DIAGRAMAS/AGENDA_SERVICOS_JANELAS_LEMBRETES/ST-007-ui-prestador-gestao-agenda-operacao/sequencia-inbox-sla-acoes-rapidas.mmd`
