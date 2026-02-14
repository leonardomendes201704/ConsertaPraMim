# EPIC-004 - Pagamentos, cancelamento e no-show financeiro

## Objetivo

Fechar o ciclo comercial do servico com pagamento integrado, comprovantes e regras financeiras claras para cancelamento/no-show.

## Problema atual

- Nao existe fechamento financeiro integrado no fluxo operacional.
- Regras de multa/compensacao por cancelamento tardio sao manuais.
- Prestador e cliente nao tem visibilidade padronizada dos impactos.

## Resultado esperado

- Pedido finalizado pode ser pago via PIX/cartao no proprio sistema.
- Comprovante de pagamento fica vinculado ao pedido.
- Regras de cancelamento/no-show aplicam creditos/debitos automaticamente.
- Ledger financeiro deixa trilha confiavel para suporte e conciliacao.

## Escopo

### Inclui

- Integracao de checkout.
- Estados de pagamento e comprovantes.
- Politica financeira parametrizavel.
- Lancamentos de credito/debito auditaveis.

### Nao inclui

- Emissao fiscal oficial (NF-e/NFS-e) em primeira versao.
- Marketplace escrow com split multi-partes complexo.

## Metricas de sucesso

- >= 85% dos servicos concluidos com pagamento registrado na plataforma.
- Reducao de inadimplencia em >= 30%.
- 100% dos casos de no-show com regra financeira aplicada e rastreavel.

## Historias vinculadas

- ST-011 - Pagamento integrado (PIX/cartao) e comprovantes.
- ST-012 - Politica financeira de cancelamento e no-show.
