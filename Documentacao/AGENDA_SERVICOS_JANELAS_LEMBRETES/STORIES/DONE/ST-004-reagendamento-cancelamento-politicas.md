# ST-004 - Reagendamento e cancelamento com politicas de prazo

Status: Done  
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

- [x] Definir matriz de regras:
- [x] prazo minimo de cancelamento sem penalidade.
- [x] prazo minimo para solicitar reagendamento.
- [x] janela maxima para nova data.
- [x] Implementar endpoints de solicitar reagendamento (cliente e prestador).
- [x] Implementar endpoints de aceitar/rejeitar reagendamento.
- [x] Implementar endpoint de cancelamento com motivo.
- [x] Atualizar motor de estados para transicoes validas.
- [x] Incluir validacoes de calendario e conflito no novo horario.
- [x] Publicar notificacoes de cada evento de reagendamento/cancelamento.
- [x] Criar testes unitarios e de integracao para fluxos positivos e de erro.
