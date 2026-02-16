# Runbook de Suporte e Rollback - ST-008 (Agenda)

## Objetivo

Padronizar resposta operacional para incidentes do modulo de agenda e definir passos seguros de rollback controlado.

## Escopo

- API de agendamentos (`/api/service-appointments/*`).
- Lembretes e workers de agenda.
- Dashboards operacionais de agenda no admin.
- Correlation id e logs estruturados do fluxo.

## Sinais de alerta

- aumento de `5xx` em endpoints de agenda;
- queda abrupta de taxa de confirmacao no SLA;
- aumento de falhas de lembrete (`ReminderFailureRatePercent`);
- divergencia entre status de agendamento no cliente e no prestador;
- erros de conflito fora do padrao esperado (`slot_unavailable` em massa).

## Diagnostico rapido (primeiros 15 minutos)

1. Confirmar janela temporal do incidente (inicio, severidade, alcance).
2. Coletar `X-Correlation-ID` de casos reportados.
3. Validar saude:
   - API (`/health` se aplicavel);
   - banco de dados;
   - workers de lembrete;
   - hub/notificacao (quando impactar atualizacao em tempo real).
4. Consultar logs estruturados filtrando por:
   - `Operation`;
   - `ActorRole`;
   - `AppointmentId`/`ServiceRequestId`;
   - `ErrorCode`;
   - `CorrelationId`.

## Tabela de incidentes e acao imediata

| Codigo | Sintoma | Acao imediata | Time owner |
|---|---|---|---|
| AGD-INC-001 | Criacao de agendamento falhando em lote | Verificar conflitos reais x regressao de validacao de slot; se regressao, aplicar feature flag/rollback parcial | Backend |
| AGD-INC-002 | Lembretes nao enviados | Confirmar worker ativo, filas e logs de dispatch; reprocessar janela impactada | Backend/Operacao |
| AGD-INC-003 | Confirmacao nao reflete no portal | Validar persistencia e notificacao realtime; forcar refresh controlado no front | Backend/Web |
| AGD-INC-004 | KPI admin incoerente | Conferir periodo/filtro e consistencia de dados base; recalcular cache se houver | Backend/Admin |
| AGD-INC-005 | Erro de autorizacao indevido | Revisar claims/role no token e politicas de permissao | Backend/SecOps |

## Procedimento de mitigacao

1. Classificar severidade:
   - `SEV-1`: indisponibilidade ampla ou perda de operacao critica;
   - `SEV-2`: degradacao relevante com workaround;
   - `SEV-3`: impacto pontual/localizado.
2. Aplicar mitigacao de menor risco:
   - reduzir impacto via feature flag;
   - pausar caminho opcional (ex.: lembrete secundario) mantendo operacao principal;
   - limitar novas escritas em fluxo degradado, mantendo leitura.
3. Comunicar status em canal operacional:
   - escopo afetado;
   - workaround ativo;
   - ETA de normalizacao.

## Playbook de rollback (controlado)

### Pre-check

1. Confirmar versao candidata a rollback (tag/commit estavel).
2. Garantir backup/snapshot recente do banco.
3. Validar impacto de schema:
   - se nao houve migration nova, rollback direto de app;
   - se houve migration nao retrocompativel, executar plano de contingencia de dados antes.

### Execucao

1. Colocar deployment em modo controlado (canario/percentual reduzido).
2. Publicar versao anterior da API.
3. Validar smoke critico:
   - `GetSlots`;
   - `Create`;
   - `Confirm`;
   - `GetMine`.
4. Validar KPI minimo no admin e erros `5xx`.
5. Expandir rollout apenas apos estabilidade.

### Pos-rollback

1. Registrar causa raiz preliminar.
2. Abrir tarefa de correcao definitiva.
3. Atualizar este runbook com aprendizado.

## Checklist de encerramento do incidente

- [ ] impacto ao usuario final estimado e comunicado;
- [ ] timeline registrada (inicio, deteccao, mitigacao, normalizacao);
- [ ] correlation ids de referencia anexados;
- [ ] acao corretiva definitiva planejada;
- [ ] monitoramento reforcado por 24h apos normalizacao.

## Evidencias minimas

- logs com `CorrelationId` de casos reais;
- comparativo de metricas antes/durante/depois;
- evidencias de smoke test pos-mitigacao/rollback;
- link do ticket de incidente e post-mortem.
