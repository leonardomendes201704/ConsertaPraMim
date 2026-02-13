# ST-012 - KPI de renda mensal por assinaturas dos prestadores

Status: Done  
Epic: EPIC-003

## Objetivo

Como administrador, quero visualizar no dashboard a renda mensal de assinaturas dos prestadores para acompanhar a performance financeira da base ativa.

## Criterios de aceite

- Dashboard admin exibe card `Renda Mensal de Assinaturas` com valor total do mes.
- Dashboard admin exibe breakdown por plano (`Bronze`, `Silver`, `Gold`) com:
  - quantidade de prestadores no plano;
  - valor mensal consolidado por plano.
- Prestadores em `Trial` nao entram no calculo da renda mensal.
- API do dashboard retorna os campos necessarios para exibir o KPI e o breakdown.
- Seed de prestadores garante plano/assinatura para todos os prestadores seedados.
- Valor exibido usa moeda BRL e formato monetario consistente no portal admin.

## Tasks

- [x] Definir tabela de precos mensais por plano (`Bronze`, `Silver`, `Gold`) em ponto unico de configuracao.
- [x] Definir regra de competencia mensal (inicio/fim do mes) para o calculo da receita.
- [x] Estender DTOs/contratos do dashboard admin para incluir:
  - receita mensal total;
  - breakdown por plano;
  - total de assinantes pagantes.
- [x] Implementar agregacao no `AdminDashboardService` para calcular receita mensal a partir dos planos dos prestadores.
- [x] Atualizar endpoint de dashboard admin para retornar os novos campos financeiros.
- [x] Atualizar UI do portal admin para renderizar o novo KPI financeiro e o breakdown por plano.
- [x] Ajustar seed para que todos os prestadores venham com plano pagante (sem `Trial`) e possam ser contabilizados.
- [x] Garantir comportamento resiliente para dados legados (prestador sem perfil/plano nao quebra o dashboard).
- [x] Criar testes unitarios para validacao do calculo da receita mensal e breakdown por plano.
- [x] Criar testes de regressao para validar consistencia do KPI quando houver mudanca de plano.
- [x] Atualizar INDEX/changelog do board administrativo.

## Regra adotada

- A `renda mensal` e exibida como run-rate mensal estimado, considerando prestadores ativos com plano pagante (`Bronze`, `Silver`, `Gold`) no momento da consulta.
- `Trial` nao entra no calculo.
