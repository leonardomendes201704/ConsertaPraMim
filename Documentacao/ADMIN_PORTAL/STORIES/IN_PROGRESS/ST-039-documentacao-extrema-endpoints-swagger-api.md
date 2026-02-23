# ST-039 - Documentacao extrema E2E dos endpoints no Swagger da API

Status: In Progress
Epic: EPIC-017

## Objetivo

Garantir que todos os endpoints da API tenham documentacao clara e detalhada no Swagger, cobrindo contexto de negocio e detalhes tecnicos de consumo.

## Criterios de aceite

- Todo endpoint visivel no Swagger possui `summary` e `description`.
- A descricao inclui, no minimo:
  - objetivo de negocio;
  - contexto tecnico de uso;
  - autenticacao/autorizacao;
  - parametros de entrada (path/query/body);
  - respostas esperadas e erros comuns;
  - observabilidade/rastreabilidade;
  - exemplo de chamada.
- O padrao é aplicado globalmente, sem depender de documentacao manual endpoint a endpoint.
- Build do `ConsertaPraMim.API` passa sem erros.
- Manual QA/Operacao atualizado com caso de validacao da documentacao da API.

## Tasks

- [x] Criar EPIC/ST e atualizar board da trilha.
- [ ] Implementar motor global de documentacao Swagger para cobertura de todas as operacoes.
- [ ] Adicionar catalogo de contexto de negocio/tecnico por dominio da API.
- [ ] Cobrir exemplos de chamada e orientacoes de autenticacao por endpoint no Swagger.
- [ ] Atualizar manual QA/Operacao + changelog e validar build final.

## Validacao tecnica

Data: 23/02/2026

- Em andamento.
