# ST-007 - Deteccao preventiva de risco de no-show

Status: Done  
Epic: EPIC-002

## Objetivo

Identificar agendamentos com alta chance de ausencia antes do horario da visita e acionar medidas preventivas de forma automatica.

## Criterios de aceite

- Sistema classifica risco do agendamento em `baixo`, `medio` ou `alto`.
- Regra inicial considera falta de confirmacao, historico de cancelamentos e proximidade da visita.
- Risco `alto` dispara alerta para cliente/prestador e fila operacional admin.
- Painel mostra motivos que levaram ao score de risco.
- Score e motivos ficam auditaveis no historico do agendamento.
- Regras de score sao configuraveis sem alterar codigo-fonte.

## Tasks

- [x] Definir heuristica de score de risco e pesos iniciais.
- [x] Criar tabela de configuracao de regras e thresholds.
- [x] Implementar job periodico de avaliacao de risco de no-show.
- [x] Persistir ultimo score e justificativas por agendamento.
- [x] Disparar notificacoes preventivas para risco medio/alto.
- [x] Criar fila operacional para intervencao manual do admin.
- [x] Exibir badge de risco nas telas de agenda e detalhe.
- [x] Criar endpoint admin para ajustar pesos/thresholds.
- [x] Criar testes com cenarios de score e regressao.
- [x] Documentar politica de acao por nivel de risco.




