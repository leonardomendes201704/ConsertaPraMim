# RUNBOOK DE SUPORTE - DIVERGENCIA DE VALOR COMERCIAL (ST-010)

## 1. Objetivo

Padronizar a investigacao e resolucao de divergencias de valor no fluxo de aditivos, garantindo rastreabilidade comercial do pedido e resposta rapida ao cliente/prestador.

## 2. Escopo

Este runbook cobre:

- Comparativo `valor anterior` x `novo valor` por aditivo.
- Versao comercial (`CommercialVersion`) e estado comercial (`CommercialState`) do pedido.
- Respostas de aditivo (`ApprovedByClient`, `RejectedByClient`, `Expired`).
- Timeout de aditivo pendente e seus efeitos operacionais.

## 3. Sinais de incidente

Abrir tratativa quando houver relato de:

1. "Aprovei e o valor nao mudou."
2. "Rejeitei e o valor mudou."
3. "Nao consigo responder aditivo / apareceu expirado."
4. "Pedido mostra versao comercial inesperada."
5. "Valor no cliente difere do admin/prestador."

## 4. Fontes de verdade

- Portal Cliente: `ServiceRequests/Details/{id}`.
- Portal Prestador: `ServiceRequests/Details/{id}`.
- Portal Admin: `AdminServiceRequests/Details/{id}`.
- API de aditivos:
  - `POST /api/service-appointments/{appointmentId}/scope-changes/{scopeChangeRequestId}/approve`
  - `POST /api/service-appointments/{appointmentId}/scope-changes/{scopeChangeRequestId}/reject`
- Historico tecnico:
  - `ServiceAppointmentHistory` com `Metadata.type = scope_change_audit`.

## 5. Regras funcionais que devem ser verdade

1. Aditivo aprovado:
   - Status do aditivo: `ApprovedByClient`.
   - `CommercialVersion` incrementa +1.
   - `CommercialState` volta para `Stable`.
2. Aditivo rejeitado:
   - Status do aditivo: `RejectedByClient`.
   - `CommercialVersion` nao incrementa.
   - `CommercialState` volta para `Stable`.
3. Aditivo expirado por timeout:
   - Status do aditivo: `Expired`.
   - `CommercialVersion` nao incrementa.
   - `CommercialState` volta para `Stable`.
   - Nova resposta deve retornar `scope_change_expired`.
4. Nao pode existir mais de um aditivo ativo pendente por agendamento.

## 6. Fluxo de triagem (L1 -> L2)

## Etapa A - Confirmar contexto

1. Coletar `ServiceRequestId`, `AppointmentId`, `ScopeChangeId` (quando existir).
2. Coletar horario do relato e usuario afetado (cliente/prestador).
3. Confirmar em qual tela a divergencia foi observada.

## Etapa B - Validar timeline comercial

1. Abrir detalhe do pedido no Admin e listar aditivos por versao.
2. Conferir ultimo aditivo e status final.
3. Conferir `CommercialVersion`, `CommercialBaseValue`, `CommercialCurrentValue`, `CommercialState`.
4. Conferir eventos `scope_change_audit`:
   - `created`
   - `approved` ou `rejected` ou `expired`

## Etapa C - Classificar tipo de divergencia

Classificar em um dos cenarios:

- C1: erro de expectativa do usuario (valor foi atualizado corretamente).
- C2: aditivo expirado por timeout (usuario tentou responder fora da janela).
- C3: aditivo rejeitado e usuario esperava aplicacao do incremento.
- C4: inconsistência tecnica real (dados e eventos nao fecham).

## 7. Matriz de diagnostico rapido

## Cenario C1 - Valor correto, percepcao incorreta

- Evidencia: timeline e versao comercial coerentes.
- Acao: explicar comparativo de valores e versao aplicada.
- Resultado esperado: incidente encerrado sem alteracao de dados.

## Cenario C2 - Timeout de resposta

- Evidencia:
  - status `Expired`;
  - erro `scope_change_expired` ao responder;
  - auditoria com evento `expired`.
- Acao:
  1. orientar prestador a criar novo aditivo, se necessario;
  2. orientar cliente sobre janela de resposta.
- Resultado esperado: novo fluxo comercial iniciado de forma limpa.

## Cenario C3 - Rejeicao com expectativa de aumento

- Evidencia: status `RejectedByClient` com motivo registrado.
- Acao: informar que rejeicao nao altera valor; negociar novo aditivo.
- Resultado esperado: pedido mantem valor anterior.

## Cenario C4 - Inconsistencia tecnica real

- Evidencia: estado comercial e historico divergentes.
- Acao:
  1. abrir incidente L2;
  2. anexar evidencias completas (logs + snapshots de tela + payloads);
  3. bloquear alteracoes manuais em banco ate analise.
- Resultado esperado: correcao por engenharia com rastreabilidade.

## 8. Acoes permitidas e proibidas

Permitido:

- Reprocessar orientacao operacional (novo aditivo quando aplicavel).
- Revalidar estado via APIs oficiais e telas de admin.
- Escalar para engenharia com evidencias completas.

Proibido:

- Alterar valores comerciais manualmente em banco sem change formal.
- Forcar status de aditivo sem trilha de auditoria.
- "Corrigir" incidente apenas por UI sem validar historico tecnico.

## 9. Checklist de evidencias para escalacao

- [ ] `ServiceRequestId`
- [ ] `AppointmentId`
- [ ] `ScopeChangeId` (quando aplicavel)
- [ ] Status do aditivo e versao
- [ ] Snapshot de `CommercialVersion`, `CommercialState`, `CommercialBaseValue`, `CommercialCurrentValue`
- [ ] Evento(s) `scope_change_audit` relacionados
- [ ] Horario UTC do problema
- [ ] Perfil impactado (cliente/prestador/admin)

## 10. SLA recomendado de suporte

- Triagem L1: ate 15 min.
- Classificacao e retorno inicial ao usuario: ate 30 min.
- Escalacao L2 (se C4): ate 60 min.
- Atualizacao de status para o solicitante: a cada 60 min enquanto aberto.

## 11. Casos de teste operacionais (QA)

1. Aprovar aditivo e validar incremento + versao.
2. Rejeitar aditivo e validar manutencao do valor.
3. Deixar aditivo pendente ate timeout e validar status `Expired`.
4. Tentar aprovar aditivo expirado e validar erro `scope_change_expired`.
5. Criar novo aditivo apos timeout e validar nova versao comercial.

## 12. Resultado esperado da operacao

- Toda divergencia de valor deve terminar em um desfecho auditavel:
  - comportamento esperado confirmado, ou
  - incidente tecnico escalado com pacote de evidencias completo.
