# EPIC-017 - Documentacao extrema dos endpoints da API no Swagger

Status: Done
Trilha: BACKEND_API

## Objetivo

Elevar o Swagger da API para um padrao de documentacao completo, com linguagem de negocio e tecnica em **todos** os endpoints, reduzindo ambiguidade para QA, operacao, times mobile/web e integracoes externas.

## Problema de negocio

- O Swagger atual expõe contratos, mas nao explica claramente o objetivo de negocio de cada endpoint.
- Consumidores da API (internos e externos) nao conseguem inferir rapidamente quando, como e por que usar cada rota.
- Falta padrao consistente para seguranca, pre-condicoes, respostas e exemplos de uso.

## Resultado esperado

- Todo endpoint publicado no Swagger possui `summary` e `description` com contexto de negocio + tecnico.
- Descricoes seguem estrutura uniforme (objetivo, autenticacao, parametros, respostas, erros comuns, observabilidade).
- Tags/dominios da API possuem orientacoes de contexto para acelerar onboarding.
- Equipe de QA consegue validar semanticamente o contrato sem leitura de codigo-fonte.

## Metricas de sucesso

- 100% das operacoes no `swagger/v1/swagger.json` com resumo e descricao nao vazios.
- Reducao de duvidas recorrentes sobre uso de endpoints em suporte/dev/qa.
- Menor tempo medio de onboarding para novos desenvolvedores.

## Escopo

### Inclui

- Inventario de domínios/endpoints expostos pela API.
- Infraestrutura de documentacao automatica no pipeline de Swagger.
- Narrativas de negocio/tecnicas por domínio e por tipo de operacao.
- Exemplo de chamada (cURL) e orientacoes de seguranca por endpoint.
- Atualizacao de manual QA/Operacao com caso de teste de documentacao.

### Nao inclui

- Alteracao de contrato funcional da API (rotas/payloads/status codes).
- Mudanca de autorizacao/roles.
- Versionamento de API (`v2`) nesta fase.

## Historias vinculadas

- ST-039 - Documentacao extrema E2E dos endpoints no Swagger da API.
