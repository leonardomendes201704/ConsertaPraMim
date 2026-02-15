# ST-007 - Deteccao preventiva de risco de no-show

Status: In Progress  
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
- [ ] Implementar job periodico de avaliacao de risco de no-show.
- [ ] Persistir ultimo score e justificativas por agendamento.
- [ ] Disparar notificacoes preventivas para risco medio/alto.
- [ ] Criar fila operacional para intervencao manual do admin.
- [ ] Exibir badge de risco nas telas de agenda e detalhe.
- [ ] Criar endpoint admin para ajustar pesos/thresholds.
- [ ] Criar testes com cenarios de score e regressao.
- [ ] Documentar politica de acao por nivel de risco.

