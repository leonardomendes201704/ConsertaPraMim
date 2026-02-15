# ST-012 - Politica financeira de cancelamento e no-show

Status: In Progress  
Epic: EPIC-004

## Objetivo

Aplicar regras claras de multa, credito e abatimento em cancelamentos tardios e no-show, de forma automatica e auditavel.

## Criterios de aceite

- Politicas sao parametrizaveis por janela de antecedencia.
- Cancelamento/no-show gera calculo automatico de valor devido.
- Sistema cria lancamentos de credito/debito para cliente/prestador quando aplicavel.
- Cliente e prestador recebem notificacao com memoria de calculo.
- Admin consegue reprocessar calculo em casos excepcionais com trilha.
- Relatorio financeiro exibe impacto consolidado por periodo.

## Tasks

- [x] Definir tabela de politicas por antecedencia e tipo de evento.
- [x] Implementar engine de calculo financeiro de no-show/cancelamento.
- [x] Integrar calculo com ledger de creditos/debitos existente.
- [x] Criar endpoint de simulacao para transparenica de regra.
- [x] Exibir memoria de calculo nas telas de detalhe.
- [x] Implementar override admin com justificativa obrigatoria.
- [x] Gerar evento financeiro para auditoria e BI.
- [ ] Criar testes de regressao para formulas monetarias.
- [ ] Validar arredondamento e locale monetario pt-BR.
- [ ] Atualizar manual de suporte para contestacoes financeiras.
