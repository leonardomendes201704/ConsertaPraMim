# RUNBOOK OPERACIONAL - ADITIVOS DE ESCOPO (ST-009)

## 1. Objetivo

Padronizar a operacao e validacao do fluxo de aditivo de escopo/valor durante atendimento em andamento, com aprovacao do cliente e trilha de auditoria.

## 2. Escopo

Cobertura desta versao:

- Prestador abre solicitacao de aditivo durante atendimento ativo.
- Solicitacao exige motivo, descricao de escopo adicional e valor incremental.
- Prestador pode anexar evidencias (foto/video) na solicitacao pendente.
- Cliente recebe notificacao com CTA para aprovar/rejeitar.
- Cliente visualiza timeline de aditivos no detalhe do pedido e responde o aditivo.
- Conclusao operacional do atendimento e bloqueada enquanto existir aditivo pendente.
- Auditoria de aditivo e registrada no historico do agendamento.

## 3. Perfis e responsabilidades

- Prestador: cria aditivo e anexa evidencias.
- Cliente: aprova/rejeita aditivo pendente.
- Admin: pode consultar e operar via endpoints protegidos quando necessario.

## 4. Regras de negocio

- Apenas `Provider` (ou `Admin`) pode solicitar aditivo.
- Apenas `Client` dono do agendamento (ou `Admin`) pode aprovar/rejeitar.
- So e permitido 1 aditivo pendente por agendamento.
- Limites de valor por plano do prestador:
  - Trial: max `R$ 120,00` e `30%` sobre proposta aceita.
  - Bronze: max `R$ 500,00` e `60%`.
  - Silver: max `R$ 1.500,00` e `100%`.
  - Gold: max `R$ 5.000,00` e `200%`.
- Finalizacao (`OperationalStatus = Completed`) bloqueada se houver aditivo com status `PendingClientApproval`.

## 5. Fluxo E2E

1. Prestador acessa `ServiceRequests/Details/{id}` (portal prestador).
2. Prestador envia aditivo (motivo, escopo adicional, valor incremental).
3. Opcionalmente envia anexos para o aditivo pendente.
4. Cliente recebe notificacao e acessa `ServiceRequests/Details/{id}` (portal cliente).
5. Cliente ve timeline de aditivos e escolhe:
   - Aprovar: status muda para `ApprovedByClient`.
   - Rejeitar: status muda para `RejectedByClient` (motivo obrigatorio).
6. Sistema registra evento de auditoria no historico do agendamento.
7. Prestador so consegue concluir atendimento apos nao existir aditivo pendente.

## 6. Endpoints principais

Base: `/api/service-appointments`

- `POST /{appointmentId}/scope-changes`
- `POST /{appointmentId}/scope-changes/{scopeChangeRequestId}/attachments/upload`
- `POST /{appointmentId}/scope-changes/{scopeChangeRequestId}/approve`
- `POST /{appointmentId}/scope-changes/{scopeChangeRequestId}/reject`

No portal cliente (acoes web):

- `POST /ServiceRequests/ApproveScopeChange`
- `POST /ServiceRequests/RejectScopeChange`

## 7. Cenarios de validacao QA

### 7.1 Happy path

1. Criar pedido, enviar proposta e aceitar proposta.
2. Confirmar agendamento e iniciar atendimento.
3. Criar aditivo com valor valido.
4. Anexar 1 evidencia.
5. No cliente, aprovar aditivo.
6. Validar na timeline:
   - versao
   - status final
   - timestamps
   - anexos
7. Concluir atendimento com sucesso.

Resultado esperado: fluxo completo sem erro e com historico auditavel.

### 7.2 Rejeicao do aditivo

1. Repetir passos 1-4.
2. No cliente, rejeitar com motivo.
3. Validar status `RejectedByClient` e motivo exibido.

Resultado esperado: aditivo encerrado como rejeitado e sem pendencia.

### 7.3 Bloqueio de conclusao

1. Criar aditivo e nao responder no cliente.
2. No prestador, tentar `Completed` no status operacional.

Resultado esperado: erro `scope_change_pending` e atendimento nao e concluido.

### 7.4 Autorizacao

- Cliente nao dono nao pode aprovar/rejeitar.
- Prestador de outro agendamento nao pode anexar evidencia.
- Perfil invalido nao lista aditivos por pedido.

Resultado esperado: `forbidden` ou vazio conforme regra.

## 8. Evidencia de auditoria

Eventos gravados no `ServiceAppointmentHistory` com `Metadata.type = scope_change_audit`:

- `created`
- `attachment_added`
- `approved`
- `rejected`

Campos auditados:

- `scopeChangeRequestId`
- `scopeChangeVersion`
- `scopeChangeStatus`
- `serviceRequestId`
- `serviceAppointmentId`
- `providerId`
- `incrementalValue`
- `reason`

## 9. Troubleshooting rapido

- Erro `policy_violation`: revisar plano do prestador e valor incremental.
- Erro `scope_change_pending`: responder aditivo pendente antes de concluir atendimento.
- Erro `invalid_state`: verificar se aditivo ja foi respondido ou se status do agendamento nao permite a acao.
- Anexo nao sobe: validar limite (25MB), content-type e extensao permitida.

## 10. Observacoes operacionais

- A timeline de aditivos atualiza sem refresh completo no portal cliente.
- No portal prestador, o detalhe do pedido mostra historico de aditivos e atualiza ao receber notificacao relacionada.
- Em caso de disputa comercial, iniciar com o historico de auditoria e anexos do aditivo.
