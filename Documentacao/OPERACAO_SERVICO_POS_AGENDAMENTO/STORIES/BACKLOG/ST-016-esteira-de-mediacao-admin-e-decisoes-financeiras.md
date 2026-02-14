# ST-016 - Esteira de mediacao admin e decisoes financeiras

Status: Backlog  
Epic: EPIC-006

## Objetivo

Disponibilizar no portal admin uma esteira completa de analise, decisao e fechamento de disputas com aplicacao controlada de impactos financeiros.

## Criterios de aceite

- Admin visualiza fila de disputas por prioridade/SLA.
- Caso possui checklist de analise e campos de fundamentacao.
- Admin pode decidir por `procedente`, `improcedente`, `parcial`.
- Decisao pode gerar reembolso, credito ou debito conforme regra.
- Partes recebem notificacao com resumo da decisao.
- Caso fechado permanece auditavel e imutavel.

## Tasks

- [ ] Criar telas admin de lista, detalhe e decisao de disputa.
- [ ] Implementar workflow de estados da disputa no backoffice.
- [ ] Implementar formulario de decisao com justificativa obrigatoria.
- [ ] Integrar decisao com engine financeira/ledger.
- [ ] Implementar notificacoes de decisao para as partes.
- [ ] Implementar bloqueio de edicao apos fechamento da disputa.
- [ ] Criar filtros por SLA, tipo, operador e status.
- [ ] Criar testes de permissao admin e segregacao de funcao.
- [ ] Criar exportacao de casos para auditoria externa.
- [ ] Atualizar manual admin com procedimento de mediacao.
