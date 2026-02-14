# ST-008 - Observabilidade, QA, runbook e operacao assistida do fluxo

Status: Backlog  
Epic: EPIC-001

## Objetivo

Garantir governanca operacional do modulo de agenda com metricas, alertas, roteiro de testes e procedimentos de suporte.

## Criterios de aceite

- Dashboard operacional exibe metricas chave da agenda.
- Logs de eventos de agenda possuem correlacao por `AppointmentId`.
- Existe runbook para incidentes de expirar sem notificar e falha de lembrete.
- Suite de testes cobre cenarios criticos ponta a ponta.
- Manual funcional para QA e operacao esta atualizado.

## Tasks

- [ ] Definir metricas operacionais:
- [ ] taxa de confirmacao no SLA.
- [ ] volume de reagendamento.
- [ ] taxa de cancelamento.
- [ ] taxa de falha de lembretes.
- [ ] Adicionar logs estruturados e correlation id no fluxo.
- [ ] Criar painel/consulta administrativa para diagnostico de agendamentos.
- [ ] Escrever plano de testes E2E para cliente e prestador.
- [ ] Criar runbook de suporte e rollback do modulo.
- [ ] Atualizar manual administrativo/QA com novos fluxos.
- [ ] Definir checklist de prontidao para rollout gradual.
