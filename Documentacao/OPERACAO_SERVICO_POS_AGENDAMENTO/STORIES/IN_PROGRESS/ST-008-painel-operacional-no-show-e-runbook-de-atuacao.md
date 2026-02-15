# ST-008 - Painel operacional de no-show e runbook de atuacao

Status: In Progress  
Epic: EPIC-002

## Objetivo

Dar visibilidade executiva e operacional sobre comparecimento, faltas e efetividade das acoes preventivas, com roteiro padrao de resposta a incidentes.

## Criterios de aceite

- Dashboard admin exibe taxa de no-show por periodo, regiao e categoria.
- Painel lista agendamentos em risco com prioridade de atendimento.
- Runbook define passos para contato, remarcacao e escalonamento.
- Indicadores mostram impacto dos lembretes na reducao de faltas.
- Exportacao CSV dos indicadores para analise externa.
- Alertas automaticos sao enviados quando KPI ultrapassa limite.

## Tasks

- [x] Definir KPIs de no-show e formulas oficiais do negocio.
- [x] Criar consultas agregadas para dashboard e filtros.
- [x] Implementar widgets e tabela de risco no portal admin.
- [x] Criar configuracao de thresholds para alertas proativos.
- [x] Integrar envio de alerta para canal interno de operacao.
- [x] Criar endpoint de exportacao CSV dos dados do painel.
- [x] Publicar runbook em `Documentacao` com passo a passo operacional.
- [x] Criar suite de testes de consistencia de metricas.
- [x] Validar performance das consultas em base maior.
- [ ] Atualizar manual QA com casos de risco/no-show.
