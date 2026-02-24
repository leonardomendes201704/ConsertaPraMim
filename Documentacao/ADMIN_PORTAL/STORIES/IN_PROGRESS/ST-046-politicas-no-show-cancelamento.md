# ST-046 - Politicas de no-show/cancelamento com governanca operacional

Status: In Progress
Epic: EPIC-018

## Objetivo

Padronizar tratamento de no-show e cancelamento para reduzir conflito entre cliente e prestador e aumentar previsibilidade operacional.

## Criterios de aceite

- Politicas parametrizaveis por janela de antecedencia e reincidencia.
- Regras de impacto (advertencia, restricao, estorno/credito) auditaveis.
- Notificacao automatica das partes sobre decisao aplicada.
- Painel admin com reincidencia e tendencia.

## Regras consolidadas por perfil (v1)

### Janela de antecedencia

- `>= 24h`: cancelamento antecipado (sem penalidade financeira).
- `6h a 24h`: cancelamento tardio moderado (warning/compensacao parcial).
- `< 6h` ou ausencia confirmada: evento critico de no-show/cancelamento tardio.

### Perfil Cliente

- 1o evento critico em 90 dias: warning + registro de reincidencia.
- 2o evento critico em 90 dias: compensacao financeira ao prestador + alerta operacional.
- 3o+ evento critico em 90 dias: prioridade baixa em matching e analise manual em disputa.

### Perfil Prestador

- 1o evento critico em 90 dias: warning + perda de destaque em ranking.
- 2o evento critico em 90 dias: debito/compensacao ao cliente + flag de risco medio.
- 3o+ evento critico em 90 dias: restricao operacional temporaria (`Restricted`) com revisao admin.

### Evidencias obrigatorias

- Status do agendamento, horario da janela e horario do cancelamento.
- Confirmacao de presenca cliente/prestador (quando existir).
- Log de notificacoes disparadas e comprovante de entrega.
- Snapshot da regra aplicada e motivo de override (quando houver).

## Tasks

- [x] Consolidar regras de no-show/cancelamento por perfil.
- [x] Implementar motor de decisao e trilha de auditoria.
- [x] Integrar notificacoes e eventos operacionais.
- [x] Criar painel de reincidencia no admin.
- [ ] Publicar runbook de operacao e contestacao.
