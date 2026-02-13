# ST-015 - Carteira de creditos e ledger transacional do prestador

Status: Backlog  
Epic: EPIC-006

## Objetivo

Como sistema, quero manter uma carteira de creditos por prestador com extrato imutavel para garantir saldo correto, rastreabilidade e auditoria de todos os movimentos.

## Criterios de aceite

- Cada prestador possui carteira/saldo de credito.
- Todo movimento de credito gera lancamento no ledger (imutavel).
- Tipos de lancamento previstos:
  - `Grant` (concessao);
  - `Debit` (consumo em mensalidade);
  - `Expire` (expiracao);
  - `Reversal` (estorno/ajuste administrativo).
- Cada lancamento registra:
  - valor;
  - saldo anterior e saldo posterior;
  - motivo;
  - referencia/origem;
  - data de vigencia/expiracao;
  - usuario admin responsavel (quando aplicavel).
- Nao e permitido consumir credito acima do saldo disponivel.
- API permite consulta de extrato por periodo e saldo atual por prestador.
- Todas as operacoes ficam auditadas no trilho administrativo.

## Tasks

- [ ] Modelar entidades de carteira e ledger de credito do prestador.
- [ ] Criar migration para novas tabelas de saldo/lancamentos.
- [ ] Implementar repositorio para leitura e escrita transacional do ledger.
- [ ] Implementar servico de dominio para calcular saldo consistente.
- [ ] Implementar validacoes de integridade (saldo, expiracao, valor > 0).
- [ ] Expor endpoints de consulta de saldo/extrato para admin e prestador.
- [ ] Integrar eventos de auditoria para cada lancamento.
- [ ] Criar testes unitarios do motor de saldo/ledger.
- [ ] Criar testes de integracao de persistencia e consistencia transacional.
