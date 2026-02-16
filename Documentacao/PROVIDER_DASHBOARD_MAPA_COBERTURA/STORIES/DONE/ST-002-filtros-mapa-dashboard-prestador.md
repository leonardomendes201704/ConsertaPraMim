# ST-002 - Filtros e experiencia de navegacao geoespacial

Status: Done  
Epic: EPIC-001

## Objetivo

Adicionar interacoes de filtro no mapa para permitir foco rapido em oportunidades por categoria e distancia.

## Criterios de aceite

- Prestador consegue filtrar pins por categoria.
- Prestador consegue limitar visualizacao por faixa de distancia.
- Mapa e lista de oportunidades mantem consistencia de dados.

## Tasks

- [x] Definir modelo de filtros para mapa e lista.
- [x] Implementar endpoint/acao para aplicar filtros em pins.
- [x] Adicionar controles de filtro na UI da dashboard.
- [x] Garantir sincronizacao entre tabela e pins exibidos.
- [x] Cobrir cenarios de vazio e fallback.

## Atualizacao de implementacao (2026-02-16)

- Modelo de filtros aplicado no dashboard:
- `category` (categoria normalizada);
- `maxDistanceKm` (limite de distancia em km).
- Acao `GET /Home/RecentMatchesData` atualizada para receber filtros e retornar:
- `coverageMap` filtrado;
- `recentMatches` derivado do mesmo conjunto de pins (sincronia mapa/lista).
- `BuildCoverageMapPayloadAsync` passou a aceitar filtros e aplicar:
- limite de distancia solicitado (com clamp no raio de busca do mapa);
- filtro de categoria com normalizacao sem acento.
- UI da dashboard (`Views/Home/Index.cshtml`) recebeu:
- combo de categoria;
- combo de distancia;
- refresh automatico ao alterar filtros.
- Sincronizacao consolidada:
- tabela de "Pedidos Recentes" renderiza os mesmos pedidos filtrados dos pins;
- contador de oportunidades acompanha o volume filtrado.
- Fallbacks implementados:
- sem base geocodificada => estado vazio no mapa;
- sem resultados no filtro => tabela em estado vazio.
- Validacao executada:
- `dotnet build src.sln -v minimal` (sucesso);
- `dotnet test ..\tests\ConsertaPraMim.Tests.Unit\ConsertaPraMim.Tests.Unit.csproj --filter "FullyQualifiedName~ServiceRequestServiceTests"` (11/11 aprovados).
- Observacoes tecnicas registradas em:
- `Documentacao/PROVIDER_DASHBOARD_MAPA_COBERTURA/DOCUMENTACAO_TECNICA_ST-002.md`

## Diagramas

- Fluxo: `Documentacao/DIAGRAMAS/PROVIDER_DASHBOARD_MAPA_COBERTURA/ST-002-filtros-mapa-dashboard-prestador/fluxo-filtros-mapa-lista.mmd`
- Sequencia: `Documentacao/DIAGRAMAS/PROVIDER_DASHBOARD_MAPA_COBERTURA/ST-002-filtros-mapa-dashboard-prestador/sequencia-filtros-mapa-lista.mmd`
