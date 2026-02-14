# EPIC-001 - Agenda de servicos com janelas de horario, confirmacao, reagendamento e lembretes automaticos

## Objetivo

Permitir que cliente e prestador combinem atendimento com horario estruturado, com previsibilidade operacional e comunicacao proativa, reduzindo faltas e retrabalho.

## Problema atual

- O fluxo de pedidos/propostas existe, mas nao ha agenda formal de execucao.
- Cliente e prestador combinam horario de forma dispersa no chat.
- Nao existe processo padrao de confirmacao, reagendamento e cancelamento.
- Nao ha lembretes automaticos para reduzir no-show.

## Resultado esperado

- Cliente escolhe janela de horario valida do prestador no momento do agendamento.
- Prestador confirma ou recusa solicitacoes dentro de SLA configurado.
- Reagendamento ocorre por fluxo estruturado, com aceite de ambas as partes.
- Lembretes automaticos sao enviados em momentos chave antes do atendimento.
- Todo o historico fica auditavel para suporte, QA e operacao.

## Personas principais

- Cliente: quer agendar rapidamente e ter previsibilidade.
- Prestador: quer controlar agenda, evitar conflitos e manter taxa de comparecimento.
- Admin/Operacao: quer monitorar saude do fluxo e agir em falhas.

## Fluxo ponta a ponta (alto nivel)

1. Prestador configura disponibilidade recorrente e bloqueios excepcionais.
2. Cliente abre pedido aceito e solicita agendamento em slot disponivel.
3. Sistema cria agendamento em estado `PendingProviderConfirmation`.
4. Prestador confirma ou recusa.
5. Se confirmado, lembretes automaticos sao programados.
6. Se houver mudanca, fluxo de reagendamento/cancelamento aplica politicas.
7. Ao concluir atendimento, status finaliza e historico permanece rastreavel.

## Regras de negocio (base)

### Estados do agendamento

- `PendingProviderConfirmation`
- `Confirmed`
- `RejectedByProvider`
- `ExpiredWithoutProviderAction`
- `RescheduleRequestedByClient`
- `RescheduleRequestedByProvider`
- `RescheduleConfirmed`
- `CancelledByClient`
- `CancelledByProvider`
- `Completed`

### Janelas e disponibilidade

- Agenda do prestador por dia da semana com hora inicio/fim.
- Duracao minima de slot configuravel por plano (ex.: 30 min).
- Bloqueios manuais de agenda (ferias, indisponibilidade pontual, evento externo).
- Prevenir overlap de agendamentos do mesmo prestador.
- Timezone unico do sistema (inicialmente America/Sao_Paulo) com campo pronto para evolucao multi-timezone.

### Confirmacao

- Toda solicitacao inicia como pendente de confirmacao do prestador.
- SLA de resposta configuravel (ex.: 12h).
- Sem resposta dentro do SLA: expirar automaticamente e notificar cliente.

### Reagendamento

- Pode ser solicitado por cliente ou prestador.
- Reagendamento exige aceite da outra parte.
- Manter historico completo de versoes de horario e motivo.

### Cancelamento

- Cancelamento exige motivo.
- Politica de prazo minima para cancelamento sem penalidade (configuravel).
- Agendamentos cancelados nao podem voltar a `Confirmed`.

### Lembretes automaticos

- Disparos padrao: `T-24h`, `T-2h`, `T-30min`.
- Canais iniciais: notificacao in-app e email.
- Regra de idempotencia para evitar duplicidade de envio.
- Registro de tentativa, sucesso e falha por lembrete.

## Escopo

### Inclui

- Modelagem de dominio da agenda.
- APIs para disponibilidade, slots, agendamento e alteracoes de status.
- UI Cliente e UI Prestador para operacao de agenda.
- Motor de lembretes com retries e observabilidade.
- Testes unitarios e integracao dos fluxos criticos.

### Nao inclui

- Integracao com Google Calendar/Outlook.
- Cobranca ou multa automatica por no-show.
- Otimizacao de rotas multi-atendimento no mesmo dia.
- Push mobile nativo (fica para evolucao futura).

## Metricas de sucesso

- Taxa de confirmacao em ate 12h >= 85%.
- Queda de no-show >= 30% apos lembretes.
- Tempo medio de reagendamento < 10 min (entre solicitacao e resposta).
- Falha de disparo de lembretes < 1% com retry.
- Zero conflito de sobreposicao de horario confirmado por prestador.

## Dependencias tecnicas

- Entidades novas no dominio e migrations no banco.
- Jobs em background para expiracao e lembretes.
- Extensao das notificacoes ja existentes (SignalR + email service).
- Permissoes por papel (Cliente, Prestador, Admin).

## Riscos e mitigacoes

- Concorrencia em reserva de slot.
  - Mitigar com transacao + validacao otimista/pessimista no create.
- Falha de job de lembrete.
  - Mitigar com retries, dead-letter logico e painel de reprocessamento.
- Ambiguidade de regras de cancelamento.
  - Mitigar com configuracao central e auditoria de alteracoes.
- Sobrecarga de notificacoes.
  - Mitigar com preferencias e deduplicacao por evento.

## Historias vinculadas

- ST-001 - Modelagem de agenda, disponibilidade e janelas de horario.
- ST-002 - API de consulta de slots e criacao de agendamentos.
- ST-003 - Confirmacao do prestador, recusa e expiracao automatica.
- ST-004 - Reagendamento e cancelamento com politicas de prazo.
- ST-005 - Lembretes automaticos, retries e rastreabilidade de envio.
- ST-006 - UI do cliente para agendar, acompanhar e reagendar servicos.
- ST-007 - UI do prestador para gerir agenda e responder solicitacoes.
- ST-008 - Observabilidade, QA, runbook e operacao assistida do fluxo.
