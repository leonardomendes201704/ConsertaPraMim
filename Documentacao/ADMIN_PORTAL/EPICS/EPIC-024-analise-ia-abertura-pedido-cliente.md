# EPIC-024 - Analise IA no fluxo de abertura de pedido do cliente

Status: In Progress
Trilha: CLIENTE_WEB

## Objetivo

Adicionar uma etapa de analise assistida por IA no wizard de abertura de pedidos do portal cliente para melhorar a qualidade da descricao inicial e reduzir ambiguidade na etapa de matching.

## Problema de negocio

- Clientes descrevem problemas de forma incompleta, o que aumenta retrabalho e tempo para receber propostas aderentes.
- O prestador recebe pedidos com contexto insuficiente e tende a enviar proposta mais conservadora ou nao propor.
- O funil perde liquidez logo no inicio quando a descricao nao comunica claramente o escopo do servico.

## Resultado esperado

- O wizard passa de 3 para 4 etapas:
  - `1. O que precisa?`
  - `2. Analise do problema`
  - `3. Onde?`
  - `4. Revisar`
- A etapa 2 consome OpenAI no backend e devolve um resumo curto de entendimento para o cliente validar antes de seguir.
- Em indisponibilidade da OpenAI/configuracao, o fluxo continua com fallback controlado sem bloquear a abertura do pedido.

## Metricas de sucesso

- Reducao de pedidos com descricao vaga ou incoerente.
- Melhora de conversao para proposta nas primeiras horas apos criacao.
- Menor tempo ate primeira proposta em categorias com maior ambiguidade descritiva.

## Escopo

### Inclui

- Endpoint API dedicado para analise de problema no contexto de `service-requests`.
- Proxy no portal cliente para a etapa de analise.
- Atualizacao da UI wizard do cliente para 4 passos com feedback de carregamento/erro.
- Documentacao (story, changelog e QA manual).

### Nao inclui

- Persistencia da analise IA no pedido.
- Alteracao no app mobile cliente nesta entrega.
- Recomendacao automatica de prestador.

## Historias vinculadas

- ST-055 - Etapa "Analise do problema" no wizard de criacao de pedido do cliente.
