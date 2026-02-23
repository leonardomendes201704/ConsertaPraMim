# EPIC-018 - Liquidez e conversao do marketplace (pedido -> proposta -> aceite)

Status: Backlog
Trilha: GROWTH_MARKETPLACE

## Objetivo

Aumentar a taxa de fechamento de pedidos no ConsertaPraMim, reduzindo tempo de primeira proposta e melhorando a decisao do cliente no comparativo entre prestadores.

## Problema de negocio

- Em parte dos pedidos, o cliente nao recebe proposta qualificada no tempo esperado.
- A etapa proposta -> aceite ainda possui friccao por falta de comparacao objetiva.
- Sem SLA por etapa, a operacao perde previsibilidade e escalabilidade.

## Resultado esperado

- Mais pedidos com proposta em janela curta (SLA operacional).
- Maior conversao de proposta para aceite.
- Menor tempo medio do ciclo pedido -> agendamento -> conclusao.

## Metricas de sucesso

- % de pedidos com primeira proposta em ate 30 min.
- Conversao proposta -> aceite.
- Tempo medio do ciclo completo do pedido.

## Escopo

### Inclui

- Instrumentacao do funil E2E com SLA por etapa.
- Score de liquidez por regiao/categoria.
- Comparador de propostas e ranking por qualidade.
- Politicas operacionais de no-show/cancelamento.

### Nao inclui

- Mudanca de modelo de receita principal (tratar em epic dedicado).
- Expansao geografica para novas cidades sem base de liquidez.

## Historias vinculadas

- ST-041 - Funil E2E com SLA operacional por etapa.
- ST-042 - Score de liquidez por regiao/categoria e alertas de deficit.
- ST-043 - Comparador de propostas para decisao do cliente.
- ST-044 - Qualidade e ranking de propostas por completude e historico.
- ST-045 - Camadas de confianca e verificacao de prestadores.
- ST-046 - Politicas de no-show/cancelamento com governanca operacional.
