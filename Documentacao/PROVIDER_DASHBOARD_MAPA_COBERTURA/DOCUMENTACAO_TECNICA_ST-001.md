# Documentacao Tecnica - ST-001 (Mapa de Cobertura na Dashboard)

## Escopo implementado

- Exibicao de mapa na dashboard do prestador com base geocodificada.
- Marcador de base do prestador com avatar.
- Circulo do raio de interesse (`RadiusKm`).
- Pins de pedidos abertos dentro e fora do raio.
- Popup com resumo do pedido e link para detalhes.
- Refresh parcial do dashboard sem reload completo da pagina.

## Backend

### Contrato de dados

- `ConsertaPraMim.Application/DTOs/ProviderDashboardMapDTOs.cs`
  - `ProviderServiceMapPinDto`
  - campos de distancia (`DistanceKm`), cobertura (`IsWithinInterestRadius`) e aderencia de categoria (`IsCategoryMatch`).

### Servico de dominio/aplicacao

- `ConsertaPraMim.Application/Services/ServiceRequestService.cs`
  - `GetMapPinsForProviderAsync(Guid providerId, double? maxDistanceKm = null, int take = 200)`
  - regras:
    - exige base geocodificada do prestador;
    - usa `interestRadiusKm` do perfil (default 5 km);
    - define raio maximo de mapa com clamp (`interestRadiusKm * 4`, min 40, max 250);
    - calcula distancia haversine por pedido;
    - ordena por distancia;
    - limita quantidade retornada.

### Controller

- `ConsertaPraMim.Web.Provider/Controllers/HomeController.cs`
  - `BuildCoverageMapPayloadAsync(...)` para carga inicial e refresh.
  - `RecentMatchesData()` retorna `coverageMap` junto com KPIs e lista resumida.

## Frontend (Portal Prestador)

- `ConsertaPraMim.Web.Provider/Views/Home/Index.cshtml`
  - card de mapa com legenda dentro/fora do raio;
  - estado vazio quando base nao esta configurada;
  - Leaflet:
    - tile layer OSM;
    - marcador de base com avatar;
    - circulo de cobertura;
    - pins de categoria;
    - tooltip de tempo relativo (`Ha X minutos/horas/dias`);
    - popup com CTA `Ver detalhes`.

- `ConsertaPraMim.Web.Provider/wwwroot/css/site.css`
  - estilo do mapa, popup, tooltip e icones de pins.

## Testes automatizados adicionados

- `Backend/tests/ConsertaPraMim.Tests.Unit/Services/ServiceRequestServiceTests.cs`
  - `GetMapPinsForProviderAsync_ShouldReturnOrderedPins_WithInsideOutsideAndCategoryFlags`
  - `GetMapPinsForProviderAsync_ShouldReturnEmpty_WhenProviderHasNoBaseCoordinates`
  - `GetMapPinsForProviderAsync_ShouldRespectMaxDistanceAndTake`

## Validacao executada

- Build: `dotnet build src.sln -v minimal` (sucesso).
- Testes: filtro `ServiceRequestServiceTests.GetMapPinsForProviderAsync` (3 aprovados).

## Limites conhecidos

- Sem clusterizacao de pins (planejado em historias futuras).
- Sem roteamento/tempo de deslocamento em tempo real.
- Sem cache dedicado para payload geoespacial.
