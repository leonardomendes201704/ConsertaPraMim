# EPIC-004 - Governanca comercial e operacional de planos de assinatura

Status: In Progress

## Objetivo

Permitir que o administrador governe os planos de assinatura dos prestadores em uma unica experiencia, incluindo regras comerciais (preco, promocao e cupom) e regras operacionais (raio e categorias permitidas).

## Problema atual

- Valores mensais dos planos estao fixos no codigo.
- Nao existe cadastro administrativo de promocoes por periodo.
- Nao existe mecanismo de cupons de desconto para assinatura.
- Limites operacionais por plano (raio maximo e categorias permitidas) nao sao governados de forma centralizada por admin.

## Resultado esperado

- Admin consegue ajustar preco mensal de cada plano.
- Admin consegue criar promocoes com data de inicio/fim para planos.
- Admin consegue criar cupons de desconto com validade e regras de uso.
- Admin consegue ajustar limite de raio de atuacao por plano.
- Admin consegue definir categorias disponiveis por plano e quantidade maxima de categorias permitidas.
- Sistema aplica e valida as regras no fluxo de onboarding/perfil do prestador.

## Metricas de sucesso

- 100% dos ajustes de preco/promocao/cupom feitos sem deploy.
- Tempo medio para atualizar oferta comercial de plano < 10 minutos.
- 0 inconsistencias de regra operacional entre plano e perfil do prestador.
- Auditoria completa de alteracoes de plano/comercial no portal admin.

## Escopo

### Inclui

- Catalogo administrativo de planos com configuracoes comerciais e operacionais.
- Regras de promocao por periodo (inicio/fim) e aplicacao por plano.
- Regras de cupom (codigo, validade, tipo de desconto, limites de uso).
- Validacao de limite de raio e categorias por plano no fluxo do prestador.
- Exposicao no admin dashboard/operacao para consulta dos parametros vigentes.

### Nao inclui

- Integracao com gateway de pagamento real.
- Split financeiro, inadimplencia e cobranca recorrente real.
- Motor de precificacao dinamica por IA.

## Historias vinculadas

- ST-013 - Gestao administrativa de precos, promocoes, cupons e limites por plano.
