# ST-041 - Funil E2E com SLA operacional por etapa

Status: In Progress
Epic: EPIC-018

## Objetivo

Instrumentar o funil operacional completo (pedido -> proposta -> aceite -> agendamento -> conclusao) com SLA por etapa e alertas para gargalos.

## Criterios de aceite

- KPIs de funil por etapa disponiveis para operacao/admin.
- SLA por etapa configuravel com semaforo (ok, risco, violado).
- Alertas operacionais em atraso de primeira proposta e aceite.
- Dados com recorte por categoria e regiao.

## Tasks

- [x] Definir dicionario de eventos e estados do funil.
- [x] Criar agregacoes por etapa com janela temporal.
- [x] Expor endpoints admin para funil e SLA.
- [ ] Implementar visualizacao no dashboard admin.
- [x] Criar alertas operacionais para violacao de SLA.
