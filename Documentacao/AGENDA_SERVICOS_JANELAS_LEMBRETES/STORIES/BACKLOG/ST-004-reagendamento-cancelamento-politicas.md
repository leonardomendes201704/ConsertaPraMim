# ST-004 - Reagendamento e cancelamento com politicas de prazo

Status: Backlog  
Epic: EPIC-001

## Objetivo

Permitir reagendamento e cancelamento com regras claras de prazo, aceite bilateral e trilha de auditoria.

## Criterios de aceite

- Cliente e prestador conseguem solicitar reagendamento.
- Toda proposta de reagendamento exige aceite da outra parte.
- Cancelamento exige motivo e respeita politica de prazo.
- Politicas de prazo ficam centralizadas em configuracao.
- Historico do agendamento registra todas as alteracoes.

## Tasks

- [ ] Definir matriz de regras:
- [ ] prazo minimo de cancelamento sem penalidade.
- [ ] prazo minimo para solicitar reagendamento.
- [ ] janela maxima para nova data.
- [ ] Implementar endpoints de solicitar reagendamento (cliente e prestador).
- [ ] Implementar endpoints de aceitar/rejeitar reagendamento.
- [ ] Implementar endpoint de cancelamento com motivo.
- [ ] Atualizar motor de estados para transicoes validas.
- [ ] Incluir validacoes de calendario e conflito no novo horario.
- [ ] Publicar notificacoes de cada evento de reagendamento/cancelamento.
- [ ] Criar testes unitarios e de integracao para fluxos positivos e de erro.
