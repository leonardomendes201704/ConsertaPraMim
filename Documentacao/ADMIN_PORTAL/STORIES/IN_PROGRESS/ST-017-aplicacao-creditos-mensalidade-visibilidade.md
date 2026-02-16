# ST-017 - Aplicacao de creditos na mensalidade e visibilidade operacional

Status: In Progress  
Epic: EPIC-006

## Objetivo

Como sistema/prestador, quero que os creditos sejam aplicados automaticamente na mensalidade e que o saldo/extrato fique visivel para transparencia operacional.

## Criterios de aceite

- Regra de calculo de mensalidade considera ordem:
  - preco base do plano;
  - promocao vigente;
  - cupom valido;
  - credito disponivel.
- Preco final da mensalidade nunca fica negativo (piso `R$ 0,00`).
- Credito nao utilizado permanece no saldo (se dentro da vigencia).
- Credito expirado nao e aplicado e gera lancamento de expiracao.
- Portal do prestador exibe:
  - saldo atual de creditos;
  - previsao de abatimento da proxima mensalidade;
  - extrato de movimentos (concessao, consumo, expiracao, estorno).
- Portal admin/dashboard exibe KPIs:
  - creditos concedidos no periodo;
  - creditos consumidos no periodo;
  - saldo total em aberto;
  - creditos a expirar.
- Manual do portal admin e atualizado com o novo modulo/fluxo.

## Tasks

- [x] Integrar motor de credito ao calculo de mensalidade/assinatura.
- [x] Implementar consumo de credito na cobranca mensal simulada.
- [ ] Implementar tratamento de expiracao automatica.
- [x] Expor endpoint de simulacao de mensalidade com credito aplicado.
- [ ] Implementar UI no portal prestador para saldo e extrato de creditos.
- [ ] Implementar widgets/KPIs no dashboard admin para creditos.
- [ ] Incluir filtros e relatorio administrativo de uso de creditos.
- [ ] Atualizar manual HTML do admin com novos fluxos e testes QA.
- [x] Criar testes unitarios do calculo final com credito.
- [ ] Criar testes de regressao E2E funcional (admin concede -> prestador recebe -> mensalidade abate).
