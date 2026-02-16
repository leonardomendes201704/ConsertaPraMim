# ST-014 - Runbook operacional de garantia e revisita

Este runbook define como cliente, prestador e administracao devem operar o fluxo de garantia, desde a abertura ate o encerramento da revisita, com foco em SLA e rastreabilidade.

## 1. Objetivo

- Padronizar abertura, resposta e encerramento de garantias.
- Garantir resposta do prestador dentro do SLA configurado.
- Reduzir retrabalho e conflitos com trilha auditavel em todo o ciclo.

## 2. Escopo

- Garantias vinculadas a atendimentos concluídos.
- Fluxo de revisita de garantia associado ao pedido original.
- Tratativas de excecao: rejeicao, escalonamento admin e expiracao de SLA.

## 3. Regras de elegibilidade

- O agendamento original deve estar em `Completed`.
- O pedido deve estar em estado elegivel (`InProgress`, `PendingClientCompletionAcceptance`, `Completed` ou `Validated`).
- A abertura deve ocorrer dentro da janela de garantia configurada (`ServiceAppointments:Warranty:WindowDays`, default: `30`).
- Nao pode existir outra garantia ativa para o mesmo agendamento.

## 4. SLA e parametros operacionais

Configuracao principal:

- `ServiceAppointments:Warranty:WindowDays`
- `ServiceAppointments:Warranty:ProviderResponseSlaHours`
- `ServiceAppointments:Warranty:EnableSlaWorker`
- `ServiceAppointments:Warranty:SlaWorkerIntervalSeconds`
- `ServiceAppointments:Warranty:SlaEscalationBatchSize`

Defaults atuais:

- Janela de garantia: `30 dias`
- SLA de resposta do prestador: `48 horas`

## 5. Status e transicoes

Status da garantia (`ServiceWarrantyClaimStatus`):

1. `PendingProviderReview`
2. `AcceptedByProvider`
3. `EscalatedToAdmin`
4. `RevisitScheduled`
5. `Closed`

Transicoes operacionais:

- Abertura pelo cliente: `PendingProviderReview`
- Aceite do prestador: `AcceptedByProvider`
- Rejeicao do prestador: `EscalatedToAdmin`
- Escalonamento automatico por SLA: `EscalatedToAdmin`
- Agendamento de revisita: `RevisitScheduled`
- Conclusao da revisita (status operacional `Completed`): `Closed`

## 6. Endpoints operacionais (API)

Base: `ServiceAppointmentsController`

- `POST /api/service-appointments/{id}/warranty-claims`
  - Abre solicitacao de garantia.
- `POST /api/service-appointments/{id}/warranty-claims/{warrantyClaimId}/respond`
  - Prestador/admin aceita ou rejeita garantia.
- `POST /api/service-appointments/{id}/warranty-claims/{warrantyClaimId}/revisit`
  - Agenda revisita de garantia.

Principais codigos de erro:

- `warranty_not_eligible`
- `warranty_expired`
- `warranty_claim_already_open`
- `warranty_claim_invalid_state`
- `warranty_response_window_expired`
- `warranty_revisit_slot_unavailable`

## 7. Fluxo operacional (passo a passo)

### Etapa A - Cliente abre garantia

1. Cliente descreve o problema no atendimento concluido.
2. Sistema valida elegibilidade e janela.
3. Sistema grava garantia como `PendingProviderReview`.
4. Notifica cliente, prestador e administracao.

### Etapa B - Prestador responde

1. Prestador avalia a solicitacao.
2. Se aceitar: status vai para `AcceptedByProvider`.
3. Se rejeitar: status vai para `EscalatedToAdmin` com justificativa obrigatoria.
4. Notificacoes sao disparadas para as partes.

### Etapa C - Revisita

1. Prestador (ou admin) agenda revisita.
2. Sistema cria novo agendamento vinculado e seta garantia para `RevisitScheduled`.
3. Cliente e prestador recebem janela confirmada.

### Etapa D - Encerramento

1. Revisita chega em `Completed` via fluxo operacional.
2. Sistema encerra garantia em `Closed` automaticamente.
3. Cliente, prestador e administracao recebem notificacao de encerramento.

## 8. Escalonamento automatico por SLA

Worker: `ServiceWarrantyClaimSlaWorker`

- Executa em intervalo configuravel.
- Busca garantias em `PendingProviderReview` com prazo vencido.
- Transiciona para `EscalatedToAdmin`.
- Registra historico e envia notificacoes.

## 9. Visibilidade em tela

Portal cliente (`ServiceRequests/Details`):

- Secao `Historico de Garantia` com linha do tempo e status.
- Atualizacao automatica via `DetailsData`/`AppointmentData`.

Portal prestador (`ServiceRequests/Details`):

- Secao `Historico de garantia` com eventos e justificativas.

## 10. Checklist de atendimento admin

- [ ] Verificar garantias escaladas por rejeicao do prestador.
- [ ] Verificar garantias escaladas por expiracao de SLA.
- [ ] Validar evidencias (problema reportado, historico do atendimento, agendamento de revisita).
- [ ] Direcionar resolucao: mediação, novo agendamento ou decisao final.
- [ ] Garantir notificacao de desfecho para cliente e prestador.

## 11. Evidencias e auditoria

Fontes de auditoria:

- `ServiceWarrantyClaims` (status, prazos, motivos, datas)
- `ServiceAppointmentHistory` (metadados de transicao)
- notificacoes in-app por transicao

Eventos historicos chave:

- `WarrantyClaimCreated`
- `WarrantyClaimAccepted`
- `WarrantyClaimRejectedEscalated`
- `WarrantyClaimEscalatedBySla`
- `WarrantyRevisitScheduled`
- `WarrantyRevisitLinked`
- `WarrantyClaimClosed`

## 12. Cenarios de teste rapido (QA)

1. Abrir garantia dentro da janela: deve criar em `PendingProviderReview`.
2. Abrir garantia fora da janela: deve retornar `warranty_expired`.
3. Aceitar garantia como prestador: deve ir para `AcceptedByProvider`.
4. Rejeitar garantia como prestador: deve ir para `EscalatedToAdmin`.
5. Deixar SLA expirar: worker deve escalar automaticamente.
6. Agendar revisita com garantia valida: deve gerar agendamento e `RevisitScheduled`.
7. Concluir revisita: garantia deve ser encerrada em `Closed`.

## 13. Operacao assistida (incidentes)

Se houver divergencia de estado:

1. Conferir `ServiceWarrantyClaims.Status` e datas UTC.
2. Conferir historico do agendamento original e da revisita.
3. Reprocessar notificacoes se necessario.
4. Registrar incidente com:
   - `WarrantyClaimId`
   - `ServiceRequestId`
   - `ServiceAppointmentId` (original + revisita)
   - estado esperado x estado atual

