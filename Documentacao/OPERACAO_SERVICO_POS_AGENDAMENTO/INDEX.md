# INDEX - Operacao de Servico Pos Agendamento

## Epicos

- `EPIC-001` - Execucao de campo, status operacional, checklist e evidencias.
- `EPIC-002` - Comunicacao proativa e prevencao de no-show.
- `EPIC-003` - Aditivos de escopo e valor durante atendimento.
- `EPIC-004` - Pagamentos, cancelamento e no-show financeiro.
- `EPIC-005` - Qualidade, reputacao e garantia pos-servico.
- `EPIC-006` - Disputas, mediacao e governanca administrativa.

## Stories

### In Progress

- Nenhuma story em andamento.

### Backlog

- Nenhuma story em backlog.

### Done

- `ST-001` - Check-in de chegada e inicio de atendimento.
- `ST-002` - Status operacional do atendimento em tempo real.
- `ST-003` - Checklist tecnico por categoria de servico.
- `ST-004` - Evidencias de execucao (antes/depois) vinculadas ao pedido.
- `ST-005` - Finalizacao formal com resumo e assinatura digital/PIN.
- `ST-006` - Lembretes automaticos e confirmacao de presenca.
- `ST-007` - Deteccao preventiva de risco de no-show.
- `ST-008` - Painel operacional de no-show e runbook de atuacao.
- `ST-009` - Solicitacao de aditivo de escopo e valor pelo prestador.
- `ST-010` - Aprovacao do cliente e versionamento comercial do pedido.
- `ST-011` - Pagamento integrado (PIX/cartao) e comprovantes.
- `ST-012` - Politica financeira de cancelamento e no-show.
- `ST-013` - Avaliacao dupla (cliente/prestador) e reputacao.
- `ST-014` - Garantia do servico e fluxo de revisita.
- `ST-015` - Abertura de disputa com evidencias.
- `ST-016` - Esteira de mediacao admin e decisoes financeiras.
- `ST-017` - Observabilidade, compliance e antifraude de disputas.

## Guias tecnicos

- `CONTRATO_INTEGRACAO_PAGAMENTO_ST-011.md` - definicao do provider inicial e contrato tecnico de integracao de pagamentos.
- `KPIS_NO_SHOW_ST-008.md` - formulas oficiais de monitoramento.
- `RUNBOOK_OPERACIONAL_NO_SHOW_ST-008.md` - procedimento operacional para tratamento de risco/no-show.
- `RUNBOOK_OPERACIONAL_ADITIVOS_ST-009.md` - procedimento operacional e QA para fluxo de aditivos comerciais.
- `RUNBOOK_OPERACIONAL_GARANTIA_ST-014.md` - procedimento operacional de atendimento de garantia e fluxo de revisita.
- `RUNBOOK_SUPORTE_DIVERGENCIA_VALOR_ST-010.md` - procedimento de suporte para diagnostico e tratativa de divergencias de valor comercial.
- `RUNBOOK_SUPORTE_CONTESTACOES_FINANCEIRAS_ST-012.md` - procedimento de suporte para triagem, simulacao, override e auditoria de contestacoes financeiras.
- `RUNBOOK_QA_AVALIACAO_BILATERAL_ST-013.md` - roteiro QA de avaliacao bilateral, moderacao e reputacao.
- `RUNBOOK_FRAUDE_COMPLIANCE_DISPUTAS_ST-017.md` - procedimento operacional de antifraude, trilha auditavel e compliance no modulo de disputas.
- `VALIDACAO_PERFORMANCE_NO_SHOW_ST-008.md` - validacao de performance das consultas do painel em base maior.
