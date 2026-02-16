# ST-014 - Garantia do servico e fluxo de revisita

Status: In Progress  
Epic: EPIC-005

## Objetivo

Permitir acionamento de garantia dentro de prazo definido e tratamento estruturado de revisita, preservando historico e responsabilidade sobre o servico original.

## Criterios de aceite

- Cliente pode abrir garantia dentro de janela configurada por categoria/plano.
- Garantia gera revisita vinculada ao pedido original.
- Prestador recebe solicitacao e pode confirmar data de revisita.
- Rejeicao de garantia exige justificativa e dispara fila admin.
- SLA de resposta de garantia e monitorado no dashboard.
- Todo ciclo de garantia fica auditavel.

## Tasks

- [x] Criar entidade de garantia com status e prazos.
- [x] Criar endpoint para abrir solicitacao de garantia.
- [x] Integrar garantia com modulo de agenda para revisita.
- [x] Implementar regras de elegibilidade por prazo e estado do pedido.
- [x] Criar fluxo de aceite/rejeicao da garantia pelo prestador.
- [x] Criar escalonamento automatico para admin apos SLA.
- [x] Exibir historico de garantia no detalhe do pedido.
- [x] Notificar partes a cada transicao de garantia.
- [x] Criar testes unitarios de regras de prazo e SLA.
- [ ] Publicar runbook de atendimento de garantia.
