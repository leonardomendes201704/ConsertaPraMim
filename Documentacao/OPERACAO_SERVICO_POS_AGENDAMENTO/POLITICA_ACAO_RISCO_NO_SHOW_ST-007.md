# ST-007 - Politica de acao por nivel de risco de no-show

Este documento define como o sistema e a operacao devem agir para cada nivel de risco calculado no agendamento.

## Niveis

- `baixo` (`0..39`)
- `medio` (`40..69`)
- `alto` (`70..100`)

## Acao automatica por nivel

### Risco baixo

- Atualiza score, nivel e motivos no agendamento.
- Mantem registro no historico tecnico do agendamento.
- Nao abre fila operacional admin.
- Nao envia alerta preventivo ativo.

### Risco medio

- Atualiza score, nivel e motivos no agendamento.
- Registra evento no historico do agendamento.
- Envia notificacao preventiva para cliente e prestador com link para detalhes.
- Cria (ou atualiza) item na fila operacional admin com status `Open`.

### Risco alto

- Executa tudo do risco medio.
- Prioriza intervencao operacional no item de fila (SLA menor).
- Mantem rastreabilidade completa do motivo e do score aplicado.

## Regras de fila operacional admin

- Se risco subir para `medio`/`alto`: cria ou reabre item na fila.
- Se risco permanecer `medio`/`alto`: atualiza score, motivos e `LastDetectedAtUtc`.
- Se risco cair para `baixo` e item estiver `Open`/`InProgress`: resolve automaticamente com nota
  `Fechado automaticamente: risco normalizado para baixo.`

## Notificacoes preventivas

- Destinatarios: cliente e prestador do agendamento.
- Assunto:
  - `Alerta preventivo: risco medio de no-show`
  - `Alerta preventivo: risco alto de no-show`
- Conteudo inclui:
  - horario da visita
  - score atual
  - motivos traduzidos
  - atalho para `ServiceRequests/Details/{serviceRequestId}?appointmentId={appointmentId}`

## Rastreabilidade e auditoria

- Campos persistidos no agendamento:
  - `NoShowRiskScore`
  - `NoShowRiskLevel`
  - `NoShowRiskReasons`
  - `NoShowRiskCalculatedAtUtc`
- Historico do agendamento recebe evento `no_show_risk_assessment` quando houver mudanca.
- Alteracoes da politica de pesos/thresholds via endpoint admin devem gerar `AdminAuditLog`.

## Operacao recomendada (runbook)

1. Monitorar itens `Open` e `InProgress` da fila de no-show.
2. Priorizar risco `alto` por proximidade da visita.
3. Acionar cliente e prestador para confirmacao ativa de presenca.
4. Registrar desfecho no item da fila (quando manual).
5. Revisar semanalmente pesos e thresholds com base em conversao real e falso positivo.
