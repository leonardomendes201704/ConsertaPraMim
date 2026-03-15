# ST-098 - Observabilidade de negocio do canal Telegram

Status: Done
Epic: EPIC-TELEGRAM-002

## Objetivo

Dar visibilidade gerencial e operacional ao canal Telegram com indicadores de conversao, handoff, tempo e gargalos.

## Criterios de aceite

- O CPM Full exibe indicadores de negocio relevantes do canal Telegram.
- A operacao consegue acompanhar volume, tempos e conversao da trilha.
- O diagnostico deixa de ser apenas tecnico.
- O manual operacional define rotina de acompanhamento e thresholds.

## Tasks

- [x] Definir os KPIs principais do canal Telegram.
- [x] Implementar agregacoes/consultas para volume, tempo e conversao.
- [x] Expor a leitura operacional e gerencial no CPM Full.
- [x] Atualizar runbook com rotina de acompanhamento.
- [x] Cobrir QA da leitura operacional.

## Entrega realizada

1. O CPM Full passou a expor a nova view administrativa `/admin/telegram/painel`, com cards executivos, tabelas operacionais e drawer `Filtros`.
2. O painel consolida volume diario, qualificacao minima, bootstrap no Chatwoot, handoff humano, top categorias, top cidades e gargalos por etapa.
3. As agregacoes passaram a sair do `SqlAdminKanbanService`, respeitando board opcional e periodo de criacao do lead em UTC com leitura no fuso de negocio.
4. O dashboard admin e o topo do Kanban passaram a oferecer atalho explicito para o novo painel Telegram.
5. O manual operacional passou a orientar como interpretar o painel e quais sinais devem virar acao da operacao.
