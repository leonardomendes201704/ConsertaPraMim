# Documentacao Tecnica - ST-002 (Filtros de categoria e distancia no mapa)

## Escopo implementado

- Filtro por categoria no mapa da dashboard do prestador.
- Filtro por distancia maxima (km) no mapa da dashboard.
- Sincronizacao entre pins exibidos no mapa e tabela "Pedidos Recentes".
- Atualizacao sem refresh completo usando endpoint de dados parciais.

## Backend

### Controller atualizado

- `ConsertaPraMim.Web.Provider/Controllers/HomeController.cs`
  - `RecentMatchesData(string? category = null, double? maxDistanceKm = null)`
    - recebe filtros;
    - retorna `coverageMap` e `recentMatches` do mesmo conjunto filtrado;
    - atualiza KPIs com base no resultado filtrado.
  - `BuildCoverageMapPayloadAsync(...)`
    - aceita `categoryFilter` e `maxDistanceKm`;
    - aplica normalizacao de categoria (sem acento, lower-case);
    - aplica limite de distancia solicitado dentro do raio de busca efetivo.
  - `NormalizeFilterValue(...)`
    - remove acentos e normaliza string para comparacao estavel.

## Frontend (Portal Prestador)

- `ConsertaPraMim.Web.Provider/Views/Home/Index.cshtml`
  - controles novos:
    - `#coverage-filter-category`;
    - `#coverage-filter-distance`.
  - funcoes novas:
    - registro/catalogo de categorias vindas dos pins;
    - montagem de URL com query string de filtros;
    - render de tabela baseada em pins filtrados;
    - sincronizacao de controles com payload aplicado.
  - comportamento:
    - ao alterar filtro, dispara `refreshDashboard()`;
    - resposta atualiza KPIs, mapa e lista em conjunto.

## Cenarios de fallback

- Sem base geocodificada: mapa entra em estado vazio com orientacao ao perfil.
- Filtro sem resultado: tabela e contador refletem zero resultados.
- Se endpoint retornar sem `recentMatches`, tabela usa fallback derivado dos pins.

## Validacao executada

- Build: `dotnet build src.sln -v minimal` (sucesso).
- Testes unitarios de servico: `ServiceRequestServiceTests` (11 aprovados).

## Arquivos principais alterados

- `Backend/src/ConsertaPraMim.Web.Provider/Controllers/HomeController.cs`
- `Backend/src/ConsertaPraMim.Web.Provider/Views/Home/Index.cshtml`
