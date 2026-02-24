# ST-051 - North Star Metric e metas trimestrais

Status: Released
Epic: EPIC-020
Owner: Growth + Operacao Admin

## North Star oficial

- Nome: `Resolucao Qualificada em ate 72h`
- Formula: `RQ72 = pedidos que chegaram em ScheduledOrBeyond em ate 72h / pedidos abertos no periodo`
- Objetivo de negocio: reduzir tempo ate valor percebido pelo cliente sem sacrificar qualidade de conversao.
- Leitura executiva: quanto maior o `RQ72`, maior a liquidez efetiva e menor friccao entre abertura, proposta e aceite.

## Guardrails obrigatorios

- `Taxa de pedidos sem proposta <= 30%`
- `SLA da 1a proposta >= 75%`
- `SLA de aceite >= 70%`
- `Sem crescimento de no-show critico na janela`

## Metas por trimestre (baseline 2026)

| Trimestre | Meta RQ72 | Dono primario | Apoio |
|---|---:|---|---|
| 2026-Q1 | 58% | Growth Operacional | Time Comercial |
| 2026-Q2 | 62% | Growth Operacional | Produto + Comercial |
| 2026-Q3 | 66% | Growth Operacional | Produto + CX |
| 2026-Q4 | 70% | Growth Operacional | Produto + Operacao |

## Cadencia de revisao

- Semanal: acompanhamento no cockpit executivo (tendencia semanal + alertas).
- Mensal: revisao de estrategia com decisoes de investimento por categoria/regiao.
- Trimestral: recalibracao de metas e guardrails com base em capacidade real.

## Criterios de sucesso da ST-051 (Task 1)

- North Star formalizada e versionada.
- Metas trimestrais publicadas com ownership.
- Guardrails definidos para evitar ganho artificial de conversao.
