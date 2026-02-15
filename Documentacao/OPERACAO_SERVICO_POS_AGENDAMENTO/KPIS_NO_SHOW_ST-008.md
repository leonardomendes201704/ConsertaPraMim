# ST-008 - KPIs oficiais de no-show (baseline de negocio)

Este documento define os indicadores oficiais para monitoramento operacional de no-show no portal admin.

## Escopo e premissas

- Granularidade base: `ServiceAppointment`.
- Periodo de analise: por `WindowStartUtc` dentro do intervalo filtrado.
- Filtros de segmentacao: periodo, categoria, cidade/regiao, nivel de risco.
- Fontes de dados atuais:
  - `ServiceAppointments`
  - `ServiceAppointmentHistory`
  - `ServiceAppointmentNoShowQueueItems`
  - `AppointmentReminderDispatches`

## Universo de agendamentos analisaveis

`BaseAppointments = agendamentos com WindowStartUtc no periodo`

Observacao: agendamentos `PendingProviderConfirmation` dentro do periodo entram na base para previsao, mas podem ser excluidos de KPI de efetividade final via filtro operacional.

## KPI-001 - Taxa de no-show geral

**Objetivo:** medir percentual de agendamentos com falha de comparecimento/atendimento.

`NoShowAppointments = status in (ExpiredWithoutProviderAction, CancelledByClient, CancelledByProvider) apos janela`

`NoShowRate = NoShowAppointments / BaseAppointments`

Regra inicial:
- `ExpiredWithoutProviderAction` conta como no-show.
- `CancelledByClient` e `CancelledByProvider` contam como no-show quando ocorrerem em janela critica (ate `X` horas da visita; default operacional: 24h).

## KPI-002 - Taxa de comparecimento efetivo

**Objetivo:** medir agendamentos com visita em campo iniciada.

`AttendanceAppointments = status in (Arrived, InProgress, Completed)`

`AttendanceRate = AttendanceAppointments / BaseAppointments`

## KPI-003 - Taxa de confirmacao de presenca (dupla)

**Objetivo:** medir aderencia preventiva antes da visita.

`DualPresenceConfirmed = appointments com ClientPresenceConfirmed = true e ProviderPresenceConfirmed = true`

`DualPresenceConfirmationRate = DualPresenceConfirmed / BaseAppointments`

## KPI-004 - Conversao de risco alto para comparecimento

**Objetivo:** medir efetividade operacional sobre casos criticos.

`HighRiskAppointments = appointments com NoShowRiskLevel = High no momento mais recente`

`HighRiskConverted = HighRiskAppointments com desfecho AttendanceAppointments`

`HighRiskConversionRate = HighRiskConverted / HighRiskAppointments`

## KPI-005 - Backlog operacional de risco

**Objetivo:** medir carga da operacao manual.

- `OpenRiskQueue = itens em ServiceAppointmentNoShowQueueItems com Status in (Open, InProgress)`
- `HighRiskQueue = itens abertos com RiskLevel = High`
- `AvgQueueAgeMinutes = media(now - FirstDetectedAtUtc) dos itens abertos`

## KPI-006 - SLA de tratativa da fila de risco

**Objetivo:** medir tempo de resolucao operacional.

`ResolvedItems = fila com Status in (Resolved, Dismissed) no periodo`

`AvgResolutionTimeMinutes = media(ResolvedAtUtc - FirstDetectedAtUtc)`

`P95ResolutionTimeMinutes = percentil 95 do mesmo intervalo`

## KPI-007 - Entrega de lembretes

**Objetivo:** monitorar confiabilidade do canal preventivo.

`ReminderSendSuccessRate = dispatches com Status = Sent / dispatches agendados`

`ReminderDeliveryRate = dispatches com DeliveredAtUtc preenchido / dispatches com Status = Sent`

## KPI-008 - Impacto dos lembretes na reducao de no-show

**Objetivo:** correlacionar lembrete com desfecho.

Definicoes:
- `RemindedAppointments`: agendamentos com pelo menos 1 `AppointmentReminderDispatch` `Sent` antes da janela.
- `NotRemindedAppointments`: agendamentos sem lembrete `Sent`.

Formulas:
- `NoShowRate_Reminded = no-show em RemindedAppointments / RemindedAppointments`
- `NoShowRate_NotReminded = no-show em NotRemindedAppointments / NotRemindedAppointments`
- `ReminderImpactDelta = NoShowRate_NotReminded - NoShowRate_Reminded`

Interpretacao: valor positivo indica reducao de no-show no grupo lembrado.

## KPI-009 - Acuracia operacional do score de risco

**Objetivo:** validar utilidade pratica da heuristica ST-007.

`PrecisionHighRisk = no-show real dentro do grupo classificado como High / total High`

`LiftHighRisk = NoShowRate_High / NoShowRate_Geral`

## KPI-010 - Taxa de falso positivo de risco alto

**Objetivo:** controlar desgaste operacional com alertas excessivos.

`FalsePositiveHighRisk = HighRiskConverted / HighRiskAppointments`

Observacao: esse KPI deve ser lido junto de `HighRiskConversionRate` para calibracao dos pesos.

## Convenções de publicação no dashboard

- Percentuais em `%` com 1 casa decimal.
- Tempos em minutos inteiros.
- Sempre exibir `N` absoluto ao lado de percentual para contexto.
- Ordenacao default da tabela de risco: `RiskLevel desc`, depois `WindowStartUtc asc`.

## Baseline inicial de alertas proativos

- `NoShowRate` > `20%` no periodo filtrado: alerta amarelo.
- `NoShowRate` > `30%`: alerta vermelho e escalonamento.
- `OpenRiskQueue` com `HighRiskQueue >= 10`: alerta de capacidade.
- `ReminderSendSuccessRate < 95%`: alerta tecnico de canal.

Esses limites sao iniciais e devem virar configuracao administrativa na task de thresholds da ST-008.
