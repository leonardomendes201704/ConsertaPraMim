# ST-001 - Dashboard com mapa, raio e pins dentro/fora do raio

Status: Done  
Epic: EPIC-001

## Objetivo

Adicionar na dashboard do prestador um mapa com visualizacao do raio de interesse e pins de pedidos proximos, incluindo pedidos fora do raio.

## Criterios de aceite

- Dashboard do prestador exibe mapa quando houver base geocodificada.
- Mapa mostra marcador do prestador e circulo do raio de interesse.
- Pins exibem pedidos dentro e fora do raio com diferenciacao visual.
- Popup do pin exibe informacoes basicas e atalho para detalhes do pedido.
- Ausencia de base geocodificada exibe estado vazio com orientacao para configurar perfil.

## Tasks

- [x] Definir DTO de pin de mapa para pedidos do prestador.
- [x] Implementar servico para recuperar pedidos proximos para mapa com distancia calculada.
- [x] Expor dados de mapa no `HomeController` (load inicial e refresh).
- [x] Implementar card/mapa na `Views/Home/Index.cshtml` com Leaflet.
- [x] Diferenciar visualmente pin dentro do raio e fora do raio.
- [x] Validar build completo do projeto e documentar observacoes.

## Atualizacao de implementacao (2026-02-16)

- DTO `ProviderServiceMapPinDto` publicado para payload de pins e metadados de distancia/radius/categoria.
- Regra de negocio implementada no `ServiceRequestService` para:
- buscar pedidos abertos em raio ampliado de mapa;
- calcular distancia em km para cada pedido;
- marcar `IsWithinInterestRadius` e `IsCategoryMatch`;
- ordenar por proximidade e limitar volume retornado.
- `HomeController` passou a montar payload de cobertura para carga inicial e refresh parcial (`RecentMatchesData`).
- Dashboard do prestador implementada com Leaflet em `Views/Home/Index.cshtml`:
- marcador da base do prestador com avatar;
- circulo de raio de interesse;
- pins de categorias com diferenciacao visual dentro/fora do raio e categoria fora do filtro;
- popup com resumo e atalho para detalhes do pedido.
- Estado vazio com orientacao para configurar base geocodificada no perfil foi aplicado.
- Estilo do mapa e pins consolidado em `ConsertaPraMim.Web.Provider/wwwroot/css/site.css`.
- Testes unitarios adicionados para cobertura das regras de pins em:
- `Backend/tests/ConsertaPraMim.Tests.Unit/Services/ServiceRequestServiceTests.cs`.
- Validacoes executadas:
- `dotnet build src.sln -v minimal` (sucesso);
- `dotnet test ..\tests\ConsertaPraMim.Tests.Unit\ConsertaPraMim.Tests.Unit.csproj --filter "FullyQualifiedName~ServiceRequestServiceTests.GetMapPinsForProviderAsync"` (3/3 aprovados).
- Observacoes tecnicas registradas em:
- `Documentacao/PROVIDER_DASHBOARD_MAPA_COBERTURA/DOCUMENTACAO_TECNICA_ST-001.md`

## Diagramas

- Fluxo: `Documentacao/DIAGRAMAS/PROVIDER_DASHBOARD_MAPA_COBERTURA/ST-001-dashboard-mapa-raio-pins/fluxo-dashboard-mapa-cobertura.mmd`
- Sequencia: `Documentacao/DIAGRAMAS/PROVIDER_DASHBOARD_MAPA_COBERTURA/ST-001-dashboard-mapa-raio-pins/sequencia-dashboard-mapa-cobertura.mmd`
