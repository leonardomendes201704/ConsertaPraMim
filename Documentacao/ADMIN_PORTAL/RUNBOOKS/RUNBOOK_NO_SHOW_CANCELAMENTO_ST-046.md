# Runbook ST-046 - Operacao e Contestacao de No-show/Cancelamento

## Objetivo

Padronizar a resposta operacional para eventos de cancelamento tardio/no-show, com decisao auditavel, comunicacao das partes e trilha de contestacao.

## Escopo

- Cliente e prestador em agendamentos da plataforma ConsertaPraMim.
- Eventos cobertos:
  - cancelamento do cliente em janela critica;
  - no-show do cliente;
  - cancelamento do prestador em janela critica;
  - no-show do prestador.

## Fontes de evidencia obrigatorias

1. Status do agendamento (`ServiceAppointmentStatus`) e timeline (janela, cancelamento, expiracao).
2. Evidencias de presenca (confirmacao cliente/prestador).
3. Registros de notificacao e comunicacao no periodo.
4. Trilha financeira da politica aplicada (`ServiceFinancialPolicyEventGenerated`).
5. Log operacional/auditoria admin (`AdminAuditLog`).

## Fluxo operacional padrao

1. Identificacao do caso
- Origem: dashboard no-show, evento operacional admin, chamado de disputa, ou fila de revisao.
- Abrir registro de analise com `serviceAppointmentId` e `serviceRequestId`.

2. Validacao de contexto
- Confirmar janela operacional do atendimento.
- Confirmar ator principal do evento (cliente ou prestador).
- Validar se o evento esta dentro da janela critica configurada.

3. Aplicacao da regra
- Executar motor de politica financeira.
- Confirmar outcome:
  - `ledger_applied`
  - `ledger_failed`
  - `no_ledger_impact`
  - `calculation_failed`
  - `skipped_zero_service_value`

4. Comunicacao das partes
- Enviar notificacao contextual para admin/partes impactadas.
- Publicar resumo no historico operacional.

5. Encerramento
- Registrar decisao final e observacoes.
- Se necessario, abrir contestacao com owner e prazo.

## SLA operacional sugerido

- Triagem inicial: ate 30 minutos apos deteccao.
- Decisao de politica: ate 2 horas para casos padrao.
- Contestacao de baixa complexidade: ate 1 dia util.
- Contestacao de media/alta complexidade: ate 3 dias uteis.

## Fluxo de contestacao

1. Recebimento
- Origem: cliente, prestador ou time interno.
- Registrar motivo objetivo (erro de classificacao, evidencia adicional, indisponibilidade sistamica, etc.).

2. Reabertura controlada
- Congelar alteracoes manuais ate consolidacao das evidencias.
- Garantir trilha de auditoria com actor e timestamp.

3. Revisao da evidencia
- Revalidar timeline do agendamento.
- Revalidar evidencias de presenca e comunicacao.
- Revalidar payload financeiro e ledger.

4. Decisao de contestacao
- Manter decisao original.
- Ajustar decisao financeira.
- Escalar para analise juridica/operacional (quando envolver risco regulatorio ou repeticao sistemica).

5. Comunicacao final
- Informar cliente e prestador com linguagem objetiva.
- Atualizar feed admin e historico do caso.

## Checklist rapido de operacao

- [ ] Caso possui `serviceAppointmentId` e `serviceRequestId`.
- [ ] Janela critica validada.
- [ ] Ator principal identificado.
- [ ] Outcome financeiro registrado.
- [ ] Notificacao operacional enviada.
- [ ] Evidencias anexadas/confirmadas.
- [ ] Contestacao (se existir) com owner e prazo.

## Indicadores para acompanhamento semanal

- Taxa de no-show cliente e prestador.
- Percentual de casos com `ledger_failed`.
- Tempo medio de resolucao de contestacao.
- Reincidencia por ator (clientes/prestadores com >= 2 eventos em 90 dias).
- Volume de casos escalados para revisao manual.
