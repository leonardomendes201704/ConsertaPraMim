# Checklist de Prontidao para Rollout Gradual - ST-008 (Agenda)

## Objetivo

Garantir publicacao gradual e segura das evolucoes de observabilidade da agenda, com gates claros de qualidade.

## Fase 0 - Pre-release (obrigatorio)

- [ ] Build e testes automatizados verdes.
- [ ] Migrations aplicadas no ambiente alvo (quando houver).
- [ ] Plano E2E ST-008 executado para cenarios criticos.
- [ ] Runbook de suporte/rollback revisado pelo time on-call.
- [ ] Dashboard admin validado com dados de teste recentes.
- [ ] Logs estruturados e `X-Correlation-ID` validados em ambiente homolog.

## Fase 1 - Canary (5% a 10% do trafego)

- [ ] Habilitar release para percentual reduzido.
- [ ] Monitorar por no minimo 30 minutos:
  - [ ] erro 5xx de agenda;
  - [ ] latencia p95 de endpoints de agenda;
  - [ ] taxa de falha de lembretes.
- [ ] Sem alerta critico durante janela de monitoracao.

Gate para avancar:
- [ ] Nenhuma regressao bloqueante detectada em canario.

## Fase 2 - Ramp-up controlado (25% -> 50%)

- [ ] Expandir para 25% e monitorar 30 minutos.
- [ ] Expandir para 50% e monitorar 30 minutos.
- [ ] Revalidar KPI de operacao da agenda no admin.
- [ ] Revalidar trilha de logs por correlation id em ao menos 3 fluxos reais.

Gate para avancar:
- [ ] Indicadores dentro de tolerancia definida.

## Fase 3 - Full rollout (100%)

- [ ] Liberar para 100% do trafego.
- [ ] Monitorar por 60 minutos apos full rollout.
- [ ] Registrar status final da release.
- [ ] Comunicar encerramento de deploy para stakeholders.

## Criticos de rollback imediato

- [ ] erro 5xx acima do limite acordado por 10 minutos consecutivos.
- [ ] falha sistemica de criacao/confirmacao de agendamento.
- [ ] falha de lembretes em massa sem recuperacao.
- [ ] inconsistencias graves entre estado de agendamento e resposta de API.

## Pos-rollout (D+1)

- [ ] Revisao de metricas de 24h.
- [ ] Revisao de incidentes (se houver).
- [ ] Atualizacao de backlog tecnico com melhorias observadas.
- [ ] Atualizacao de documentacao/diagramas em caso de ajuste de fluxo.
