# ST-008 - Painel operacional de no-show e runbook de atuacao

Status: Done  
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
- [x] Atualizar manual QA com casos de risco/no-show.

## Roteiro QA - Risco e No-show

### Pre-condicoes

1. Banco com seed aplicado e usuarios admin/client/prestador ativos.
2. Agendamentos em diferentes status (`ExpiredWithoutProviderAction`, `CancelledByClient`, `CancelledByProvider`, `Arrived`, `InProgress`, `Completed`).
3. Worker de risco/no-show habilitado para alimentar fila operacional.
4. Portal Admin autenticado com usuario `Admin`.

### Cenario 1 - Carregamento do painel no-show

1. Abrir dashboard admin e acessar bloco de no-show.
2. Informar periodo valido (ex.: ultimos 30 dias) e aplicar filtros.
3. Resultado esperado:
   - KPIs exibidos sem erro;
   - lista por categoria e por cidade preenchidas;
   - fila de risco visivel com itens `Open`/`InProgress`.

### Cenario 2 - Consistencia de KPI de no-show

1. Selecionar periodo com dados conhecidos.
2. Validar formula: `NoShowRatePercent = NoShowAppointments / BaseAppointments * 100`.
3. Resultado esperado:
   - taxa exibida com 1 casa decimal;
   - valores de base e no-show coerentes com os agendamentos do periodo.

### Cenario 3 - Regra de no-show por cancelamento tardio

1. Criar/usar agendamento cancelado dentro da janela configurada (default 24h).
2. Atualizar painel para o periodo do agendamento.
3. Resultado esperado:
   - agendamento conta como no-show.

### Cenario 4 - Cancelamento fora da janela

1. Criar/usar agendamento cancelado fora da janela (ex.: 36h antes).
2. Atualizar painel.
3. Resultado esperado:
   - agendamento nao conta como no-show.

### Cenario 5 - Fila de risco e ordenacao

1. Gerar itens de risco `High` e `Medium` com scores distintos.
2. Abrir tabela de fila operacional.
3. Resultado esperado:
   - ordenacao por `RiskLevel desc`, depois `Score desc`, depois horario da visita;
   - itens limitados ao `queueTake` aplicado.

### Cenario 6 - Thresholds e alerta proativo

1. Ajustar thresholds de warning/critical no admin.
2. Forcar condicao acima do limite no periodo.
3. Resultado esperado:
   - configuracao persiste;
   - worker dispara alerta para canal operacional conforme configuracao.

### Cenario 7 - Exportacao CSV

1. Executar exportacao com os mesmos filtros do painel.
2. Abrir arquivo gerado.
3. Resultado esperado:
   - arquivo `.csv` baixado com nome `admin-no-show-dashboard-YYYYMMDD-HHmmss.csv`;
   - secoes presentes: `Kpi`, `BreakdownCategory`, `BreakdownCity`, `OpenRiskQueue`;
   - colunas com datas em UTC no formato ISO-8601.

### Cenario 8 - Runbook operacional

1. Seguir o fluxo do runbook para item `High` em janela proxima.
2. Registrar tentativa de contato, escalonamento e desfecho.
3. Resultado esperado:
   - item atualizado com status e nota final auditavel;
   - acao aderente ao SLA definido no runbook.
