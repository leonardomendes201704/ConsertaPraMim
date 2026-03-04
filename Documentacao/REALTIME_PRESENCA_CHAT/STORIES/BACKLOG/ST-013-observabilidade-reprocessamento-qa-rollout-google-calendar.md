# ST-013 - Observabilidade, reprocessamento, QA e rollout da sincronizacao Google Calendar

Status: Backlog  
Epic: EPIC-003

## Objetivo

Fechar a operacao da sincronizacao com monitoramento, retentativas controladas, ferramentas de reprocessamento e plano de rollout seguro.

## Criterios de aceite

- Dashboard operacional mostra sucesso/erro/latencia da sincronizacao.
- Falhas transientes entram em retry com backoff e limite configuravel.
- Falhas permanentes ficam em fila de reprocessamento manual.
- Runbook cobre diagnostico, correcoes comuns e rollback.
- Feature flag permite ativar/desativar sync por ambiente.

## Tasks

- [ ] Instrumentar metricas de sincronizacao (`created`, `updated`, `deleted`, `failed`, `retry_count`, `latency_ms`).
- [ ] Implementar politicas de retry com backoff exponencial e jitter.
- [ ] Implementar dead-letter logica para falhas permanentes.
- [ ] Criar endpoint/admin job para reprocessar sincronizacao pendente por intervalo ou `appointmentId`.
- [ ] Adicionar logs estruturados com `appointmentId`, `googleEventId`, `providerId`, `traceId`.
- [ ] Implementar guardrail para nao confirmar sincronizacao quando API Google retornar erro.
- [ ] Criar plano QA com cenarios happy path, falhas de autenticacao, timeout e cancelamento.
- [ ] Criar runbook operacional de incidentes e recuperacao da integracao.
- [ ] Definir rollout progressivo por ambiente (dev -> homolog -> prod) com criterio de go/no-go.

