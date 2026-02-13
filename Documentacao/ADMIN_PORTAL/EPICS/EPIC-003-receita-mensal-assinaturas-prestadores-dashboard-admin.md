# EPIC-003 - Receita mensal de assinaturas dos prestadores no dashboard admin

Status: Done

## Objetivo

Dar visibilidade financeira para o administrador sobre a renda mensal gerada pelas assinaturas dos prestadores (planos Bronze, Silver e Gold).

## Problema atual

- O dashboard admin nao mostra nenhuma metrica de receita de assinaturas.
- Nao existe consolidacao mensal de valor por plano no portal administrativo.
- Seed pode deixar prestadores em `Trial`, o que distorce a leitura inicial da receita.

## Resultado esperado

- Dashboard admin exibindo KPI de `Renda Mensal de Assinaturas`.
- Breakdown por plano (Bronze/Silver/Gold) com quantidade de prestadores e valor total por plano.
- Total de prestadores assinantes pagantes no periodo.
- Seed inicial garantindo prestadores com plano de assinatura para permitir contabilizacao imediata.

## Metricas de sucesso

- KPI financeiro carregando junto com dashboard admin em ate 2 segundos.
- 100% dos prestadores seedados com plano pagante (`Bronze`, `Silver` ou `Gold`).
- Divergencia zero entre valor exibido no dashboard e regra de calculo definida para cada plano.

## Escopo

### Inclui

- Definicao da regra de valor mensal por plano.
- Agregacao de receita mensal no backend do dashboard admin.
- Exibicao visual do KPI de receita e breakdown por plano no portal admin.
- Ajuste do seed para garantir todos os prestadores com assinatura/plano.

### Nao inclui

- Integracao com gateway de pagamento.
- Gestao de inadimplencia, chargeback ou ciclo de faturamento real.
- Emissao fiscal/financeira.

## Historias vinculadas

- ST-012 - KPI de renda mensal por assinaturas dos prestadores.
