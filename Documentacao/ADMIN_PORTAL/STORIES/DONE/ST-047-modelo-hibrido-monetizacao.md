# ST-047 - Modelo hibrido de monetizacao (assinatura + creditos orientados a resultado)

Status: Done
Epic: EPIC-019

## Objetivo

Evoluir monetizacao para modelo hibrido que combine previsibilidade de assinatura com incentivo por resultado (creditos/performance).

## Criterios de aceite

- Estrategia comercial hibrida definida e parametrizavel.
- Simulacao de impacto financeiro por perfil de prestador.
- KPIs de receita por componente (assinatura vs creditos).
- Regras de migracao de plano sem perda de historico.

## Modelo comercial hibrido v1

### Componentes de receita

1. Receita fixa (assinatura mensal por prestador):
- `Plano Essencial`: valor base menor, com limite de leads/operacoes.
- `Plano Crescimento`: valor intermediario, com maior faixa de operacoes.
- `Plano Escala`: valor premium, com maior visibilidade e cobertura operacional.

2. Receita variavel (creditos orientados a resultado):
- Consumo de creditos por eventos que geram resultado ao prestador:
  - proposta aceita;
  - agendamento confirmado;
  - servico concluido (quando aplicavel por categoria/plano).
- Politica anti dupla cobranca por evento e por referencia (`requestId`/`appointmentId`).

### Regras de combinacao

- Assinatura garante baseline de capacidade e previsibilidade de receita.
- Creditos capturam monetizacao incremental por performance real.
- Limites operacionais:
  - debito de creditos nunca pode gerar saldo negativo invalido;
  - prestador sem saldo segue com operacao limitada conforme plano/politica.
- Governanca:
  - toda mutacao financeira deve registrar tipo de componente (`subscription` ou `credits`) e referencia de negocio.

### Simulacao financeira operacional

- Entradas minimas:
  - plano atual;
  - preco mensal;
  - volume de propostas aceitas/agendamentos/conclusoes;
  - creditos concedidos, consumidos e expirados.
- Saidas:
  - MRR estimado da assinatura;
  - receita variavel por creditos;
  - receita total;
  - participacao percentual de cada componente;
  - margem operacional estimada por cohort.

### Migracao de planos e historico

- Troca de plano preserva historico financeiro (ledger imutavel).
- Mudanca de plano vale para novo ciclo de cobranca.
- Operacoes em curso permanecem com regra da data do evento para evitar reprocessamento inconsistente.

## Tasks

- [x] Modelar regras comerciais do modelo hibrido.
- [x] Implementar simulador financeiro para operacao/admin.
- [x] Ajustar ledger para separar componentes de receita.
- [x] Criar dashboard de receita por componente.
- [x] Definir estrategia de rollout por cohort.
