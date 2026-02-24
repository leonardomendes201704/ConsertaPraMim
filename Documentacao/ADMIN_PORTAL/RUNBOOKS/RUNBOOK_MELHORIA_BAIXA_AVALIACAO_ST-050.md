# RUNBOOK ST-050 - Melhoria Operacional por Baixa Avaliacao

## Objetivo

Padronizar a resposta operacional quando o ciclo pos-servico identificar sinais de baixa qualidade (NPS, score composto, concentracao de 1 estrela), reduzindo reincidencia e recuperando conversao/recompra.

## Escopo

- Canal: Portal Admin (`Dashboard`, `Eventos Recentes`, `Reviews`).
- Publico: Operacao Admin, CX/Atendimento e Gestao de Qualidade.
- Entradas principais:
  - `GET /api/admin/dashboard` (KPIs `operationalNpsScore`, `operationalQualityScore`, `reviewOutliers`, `repurchaseRatePercent`).
  - `GET /api/reviews/summary/provider/{providerId}`.
  - `GET /api/reviews/summary/client/{clientId}`.
  - `POST /api/reviews/admin/repurchase/run` (retencao para base positiva).

## Gatilhos de acionamento

Abrir acao de melhoria quando qualquer condicao ocorrer no recorte vigente:

1. `operationalNpsScore < 40`.
2. `operationalQualityScore < 70`.
3. Outlier com `oneStarRatePercent >= 40%` e `totalReviews >= 5`.
4. Queda de `repurchaseRatePercent` acima de 10 p.p. em relacao ao periodo anterior.

## Classificacao de severidade

- `SEV-1` (critico): NPS < 20 ou repeticao de reclamacao grave (seguranca/fraude).
- `SEV-2` (alto): NPS entre 20-39 ou score composto < 60.
- `SEV-3` (moderado): NPS entre 40-49 ou score entre 60-69.

## Fluxo operacional

1. Triage inicial (SLA 4h)
- Confirmar consistencia de dados no dashboard (filtro periodo, base e respondentes).
- Identificar ator principal: prestador, cliente ou categoria/regiao.
- Classificar severidade (`SEV-1/2/3`).

2. Diagnostico (SLA 1 dia util)
- Consultar summaries de review para distribuicao de nota e comentario dominante.
- Levantar historico de eventos relacionados (no-show, cancelamento, disputa, atraso de atendimento).
- Registrar causa provavel:
  - prazo/agendamento;
  - comunicacao;
  - qualidade tecnica;
  - custo-beneficio;
  - conduta/compliance.

3. Plano de acao (SLA 2 dias uteis)
- `Prestador`:
  - reforco de escopo e prazo em proposta;
  - checklist obrigatorio de atendimento;
  - acompanhamento assistido por 7 dias;
  - se recorrente: aplicar fila de confianca/restricao.
- `Cliente`:
  - orientacao de briefing mais preciso;
  - validacao de expectativas e janela de atendimento;
  - moderacao de abuso reincidente.
- `Categoria/Regiao`:
  - revisar liquidez e cobertura;
  - acionar campanha de reativacao;
  - ajustar priorizacao comercial.

4. Execucao e monitoramento (SLA semanal)
- Reavaliar KPIs em D+7 e D+14.
- Se sem melhora:
  - escalar `SEV-2 -> SEV-1`;
  - abrir plano corretivo ampliado com owner executivo.

## Matriz de decisao rapida

| Sinal principal | Acao imediata | Owner | SLA |
|---|---|---|---|
| NPS < 20 | Revisao completa + bloqueio preventivo de risco | Operacao + Trust | 4h |
| Score composto < 60 | Coaching tecnico e revisao de padrao de proposta | Qualidade | 1 dia util |
| 1 estrela >= 40% | Auditoria de casos recentes e contato ativo com base afetada | CX | 1 dia util |
| Recompra em queda > 10 p.p. | Rodar analise de funil + plano de reativacao | Growth | 2 dias uteis |

## Registro obrigatorio

Cada intervencao deve registrar:

- data/hora e owner;
- recorte analisado (de/ate + filtros);
- sinal que disparou o playbook;
- causa raiz documentada;
- acao aplicada;
- resultado em D+7 e D+14.

## KPIs de acompanhamento

- `Operational NPS`.
- `Operational Quality Score` (media do score composto).
- `% Outliers criticos` (1 estrela >= 40%).
- `Repurchase Rate`.
- volume de reviews coletados no periodo.

## Criterio de encerramento do incidente

Encerrar quando, por 2 ciclos consecutivos (D+7/D+14):

1. NPS >= 50.
2. Score composto >= 75.
3. Sem novos outliers criticos no grupo monitorado.

## Relacionamento com outros runbooks

- No-show/cancelamento: `RUNBOOK_NO_SHOW_CANCELAMENTO_ST-046.md`.
- Confianca de prestadores: `RUNBOOK_CONFIANCA_PRESTADORES_ST-045.md`.
- Termos legais e governanca: `RUNBOOK_TERMOS_LEGAIS_ST-035.md`.
