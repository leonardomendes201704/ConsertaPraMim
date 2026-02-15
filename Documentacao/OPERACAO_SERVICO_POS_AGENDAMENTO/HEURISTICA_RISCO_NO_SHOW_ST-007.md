# ST-007 - Heuristica inicial de risco de no-show

Este documento define a heuristica base para classificar o risco de no-show dos agendamentos em `baixo`, `medio` e `alto`.

## Objetivo operacional

Antecipar faltas e cancelamentos tardios para permitir acao proativa de cliente, prestador e operacao admin.

## Formula de score (0..100)

`score = confirmacao + proximidade + historico`

O resultado final e truncado para o intervalo `0..100`.

## Bloco 1 - Confirmacao de presenca

- Cliente nao confirmou presenca: `+25`
- Prestador nao confirmou presenca: `+25`
- Ambos nao confirmaram: `+10` adicional

Faixa maxima do bloco: `60`.

## Bloco 2 - Proximidade da janela da visita

- Visita em ate 24h: `+10`
- Visita em ate 6h: `+15` (substitui o de 24h)
- Visita em ate 2h: `+20` (substitui os anteriores)

Faixa maxima do bloco: `20`.

## Bloco 3 - Historico recente de cancelamento/ausencia

Janela de analise: ultimos `90` dias, ate `20` eventos por ator.

- Cliente com historico relevante (cancelamento tardio/ausencia): `+10`
- Prestador com historico relevante (cancelamento tardio/ausencia): `+10`

Faixa maxima do bloco: `20`.

## Thresholds de classificacao

- `baixo`: `0..39`
- `medio`: `40..69`
- `alto`: `70..100`

## Motivos auditaveis padrao

Cada score deve registrar motivos legiveis para painel e historico, por exemplo:

- `client_presence_not_confirmed`
- `provider_presence_not_confirmed`
- `both_presence_not_confirmed`
- `window_within_24h`
- `window_within_6h`
- `window_within_2h`
- `client_history_risk`
- `provider_history_risk`

## Observacoes

- Esta e a versao heuristica inicial (nao-ML).
- Os pesos e thresholds devem ser persistidos em tabela para ajuste sem deploy.
- Alteracoes futuras de peso devem ser auditadas.
