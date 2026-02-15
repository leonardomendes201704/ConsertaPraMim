# RUNBOOK DE SUPORTE - CONTESTACOES FINANCEIRAS (ST-012)

## 1. Objetivo

Padronizar a triagem, diagnostico e resolucao de contestacoes financeiras relacionadas a cancelamento e no-show, garantindo:

- consistencia de calculo;
- rastreabilidade (historico + auditoria);
- resposta rapida para cliente e prestador.

## 2. Escopo

Este runbook cobre:

- calculo de politica financeira por evento (`ClientCancellation`, `ProviderCancellation`, `ClientNoShow`, `ProviderNoShow`);
- aplicacao de impacto financeiro no ledger do prestador;
- memoria de calculo exibida nas telas de detalhes;
- override administrativo com justificativa obrigatoria.

## 3. Sinais de incidente

Abrir tratativa quando houver relato de:

1. "A multa/compensacao foi calculada errada."
2. "Recebi mensagem de ajuste financeiro, mas o valor nao bate."
3. "Nao houve lancamento no ledger apos cancelamento/no-show."
4. "O valor parece diferente entre telas."
5. "O admin reprocessou, mas nao entendi o motivo/resultado."

## 4. Fontes de verdade

- API simulacao:
  - `POST /api/service-appointments/financial-policy/simulate`
- API override admin:
  - `POST /api/service-appointments/{id}/financial-policy/override`
- Historico tecnico:
  - `ServiceAppointmentHistory.Metadata.type = financial_policy_application`
  - `ServiceAppointmentHistory.Metadata.type = financial_policy_calculation_failed`
  - `ServiceAppointmentHistory.Metadata.type = financial_policy_override_requested`
- Auditoria/BI:
  - `AdminAuditLog.Action = ServiceFinancialPolicyEventGenerated`
  - `AdminAuditLog.TargetType = ServiceAppointmentFinancialPolicy`
- Ledger:
  - `ProviderCreditLedger` com `Source` prefixado por `FinancialPolicy:`

## 5. Regras que devem ser verdade

1. Todos os valores monetarios sao arredondados para 2 casas com `AwayFromZero`.
2. Memoria de calculo deve estar em locale `pt-BR` (ex.: `R$ 1.234,56`).
3. Se `compensacao + retencao > multa`, o ajuste deve manter a consistencia:
   - reduz retencao primeiro;
   - se necessario, reduz compensacao.
4. Se nao houver regra ativa para o evento/janela:
   - deve registrar `financial_policy_calculation_failed`.
5. Override admin exige justificativa valida e gera trilha:
   - historico tecnico;
   - evento de auditoria/BI.

## 6. Fluxo de triagem (L1 -> L2)

### Etapa A - Coleta inicial

1. Coletar `ServiceRequestId` e `ServiceAppointmentId`.
2. Identificar ator do relato (cliente, prestador, admin).
3. Registrar horario UTC aproximado do evento.
4. Capturar print da tela com memoria financeira (quando houver).

### Etapa B - Validar evento e regra aplicada

1. Confirmar tipo do evento financeiro (cancelamento ou no-show).
2. Conferir no historico o bloco `financial_policy_application` ou `financial_policy_calculation_failed`.
3. Verificar:
   - `eventType`
   - `serviceValue`
   - `breakdown.penaltyPercent/Amount`
   - `breakdown.counterpartyCompensationPercent/Amount`
   - `breakdown.platformRetainedPercent/Amount`
   - `breakdown.remainingAmount`

### Etapa C - Reproducao por simulacao

1. Rodar `POST /api/service-appointments/financial-policy/simulate` com os mesmos dados:
   - `eventType`
   - `serviceValue`
   - `windowStartUtc`
   - `eventOccurredAtUtc`
2. Comparar resultado da simulacao com o `breakdown` persistido.
3. Se divergir, abrir incidente tecnico L2 com evidencias.

### Etapa D - Validar impacto de ledger

1. No metadata de historico, verificar bloco `ledger.requested` e `ledger.result`.
2. Confirmar se houve:
   - `Grant` (compensacao ao prestador), ou
   - `Debit` (penalidade ao prestador).
3. Em caso de falha de ledger, classificar como incidente tecnico (sem recalc manual em banco).

### Etapa E - Validar auditoria/BI

1. Confirmar `AdminAuditLog` com:
   - `Action = ServiceFinancialPolicyEventGenerated`
   - `TargetType = ServiceAppointmentFinancialPolicy`
2. Validar payload:
   - `outcome` (`ledger_applied`, `ledger_failed`, `no_ledger_impact`, `calculation_failed`, `skipped_zero_service_value`)
   - ids de appointment/request/provider/client
   - dados de breakdown e ledger.

## 7. Procedimento de override admin

Usar somente quando houver excecao operacional justificada.

1. Confirmar motivo de negocio (evidencia objetiva).
2. Executar `POST /api/service-appointments/{id}/financial-policy/override`.
3. Preencher justificativa clara e auditavel.
4. Validar retorno de sucesso.
5. Validar pos-acao:
   - historico com `financial_policy_override_requested`;
   - novo registro de aplicacao financeira;
   - evento de auditoria/BI.

## 8. Acoes permitidas e proibidas

Permitido:

- simular calculo para reproduzir divergencia;
- aplicar override admin com justificativa;
- escalar para engenharia com pacote de evidencias.

Proibido:

- editar valor financeiro manualmente em banco;
- alterar ledger sem trilha oficial;
- fechar chamado sem validar historico e auditoria.

## 9. Checklist de evidencias para escalacao

- [ ] `ServiceRequestId`
- [ ] `ServiceAppointmentId`
- [ ] Evento financeiro (`eventType`)
- [ ] Janela e horario do evento (`windowStartUtc` / `eventOccurredAtUtc`)
- [ ] Payload da simulacao
- [ ] Historico `financial_policy_*`
- [ ] Dados de ledger (`requested`/`result`)
- [ ] Registro de `AdminAuditLog` correspondente
- [ ] Prints das telas cliente/prestador/admin

## 10. SLA recomendado

- Triagem L1: ate 15 min.
- Diagnostico inicial com simulacao: ate 30 min.
- Escalacao L2 (quando necessario): ate 60 min.
- Atualizacao ao solicitante: a cada 60 min enquanto aberto.

## 11. Casos operacionais de QA

1. Cancelamento cliente com compensacao ao prestador (ledger `Grant`).
2. No-show prestador com penalidade ao prestador (ledger `Debit`).
3. Regra ausente para evento (falha de calculo registrada).
4. Overflow de alocacao (compensacao + retencao > multa) com ajuste correto.
5. Override admin com justificativa e trilha completa.
6. Validacao de locale na memoria (`pt-BR`, moeda e percentual).

## 12. Resultado esperado

Toda contestacao financeira deve terminar em um desfecho auditavel:

- comportamento esperado confirmado com evidencia, ou
- incidente tecnico escalado com pacote de diagnostico completo.
