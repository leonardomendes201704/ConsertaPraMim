# EPIC-002 - Comunicacao proativa e prevencao de no-show

## Objetivo

Reduzir faltas e cancelamentos tardios com lembretes inteligentes, confirmacao de presenca e deteccao antecipada de risco operacional.

## Problema atual

- Mesmo com agendamento confirmado, ha risco de ausencia de uma das partes.
- Nao existe mecanismo de confirmacao de presenca proximo da visita.
- Operacao/admin nao tem visao consolidada de risco de no-show.

## Resultado esperado

- Cliente e prestador recebem lembretes nos momentos criticos.
- Ambas as partes podem confirmar presenca sem friccao.
- Sistema identifica risco de no-show e dispara acoes preventivas.
- Admin acompanha taxa de comparecimento por periodo/regiao/categoria.

## Escopo

### Inclui

- Lembretes automaticos multi-canal.
- Confirmacao ativa de presenca.
- Regras de risco e alerta operacional.
- Painel e runbook de tratamento de excecoes.

### Nao inclui

- Motor preditivo de ML em primeira versao.
- Rebooking totalmente automatico sem consentimento.

## Metricas de sucesso

- Reducao de no-show em >= 25%.
- Taxa de confirmacao de presenca >= 70% em T-2h.
- Tempo medio de acao em alertas de risco < 15 min.

## Historias vinculadas

- ST-006 - Lembretes automaticos e confirmacao de presenca.
- ST-007 - Deteccao preventiva de risco de no-show.
- ST-008 - Painel operacional de no-show e runbook de atuacao.
