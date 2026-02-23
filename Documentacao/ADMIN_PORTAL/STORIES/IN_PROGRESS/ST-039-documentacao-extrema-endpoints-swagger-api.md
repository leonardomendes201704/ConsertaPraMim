# ST-039 - Documentacao extrema E2E dos endpoints no Swagger da API

Status: Done
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
- [x] Implementar motor global de documentacao Swagger para cobertura de todas as operacoes.
- [x] Adicionar catalogo de contexto de negocio/tecnico por dominio da API.
- [x] Cobrir exemplos de chamada e orientacoes de autenticacao por endpoint no Swagger.
- [x] Atualizar manual QA/Operacao + changelog e validar build final.
- [x] Refinar narrativas para contexto de negocio por rota/acao (evitando frases genericas).

## Validacao tecnica

Data: 23/02/2026

- `dotnet build Backend/src/ConsertaPraMim.API/ConsertaPraMim.API.csproj`
  - Resultado: sucesso (0 erros) apos refinamento de narrativas por rota/acao no catalogo.
- Inventario tecnico:
  - `rg -n "^\s*\[Http(Get|Post|Put|Patch|Delete)" Backend/src/ConsertaPraMim.API/Controllers -S | Measure-Object`
  - Resultado: 240 operacoes mapeadas no conjunto de controllers da API, todas cobertas pelo filtro global de documentacao.
- Validacao de qualidade:
  - Inspecao manual no Swagger confirmou remoção de frase generica de objetivo (`Disponibilizar visao de ...`) em favor de narrativas por cenario operacional da rota.
