# Documentacao Tecnica - ST-003 (Realtime e performance do mapa)

## Escopo implementado

- Atualizacao do mapa orientada a eventos de notificacao/reconexao.
- Refresh incremental dos pins (diff por `requestId`).
- Paginacao geoespacial para carregamento progressivo de pins.
- Ajustes de performance de renderizacao para cenarios de alto volume.

## Backend

### Endpoint de dados de dashboard

- `ConsertaPraMim.Web.Provider/Controllers/HomeController.cs`
  - `GET /Home/RecentMatchesData`
  - novos parametros:
    - `pinPage` (default 1)
    - `pinPageSize` (default 120, clamp 20..200)
    - `category`
    - `maxDistanceKm`
  - resposta agora inclui metadados de paginacao no `coverageMap`:
    - `PinPage`
    - `PinPageSize`
    - `TotalPins`
    - `HasMorePins`

### Montagem de payload geoespacial

- `BuildCoverageMapPayloadAsync(...)`
  - aplica filtros de categoria e distancia;
  - normaliza categoria para comparacao sem acento;
  - calcula lote paginado com `skip/take`;
  - retorna apenas pins da pagina solicitada com total consolidado.

## Frontend (Portal Prestador)

- `ConsertaPraMim.Web.Provider/Views/Home/Index.cshtml`
  - refresh com debounce e abort de requisicao anterior;
  - atualizacao incremental de marcadores via mapa `coverageMarkersByRequestId`;
  - botao `Carregar mais pins` para avancar pagina;
  - metrica visual `Carregados X/Y`;
  - listener de `cpm:realtime-reconnected`.

- `ConsertaPraMim.Web.Provider/wwwroot/css/site.css`
  - estilo compacto de pins para alto volume.

## Realtime / reconexao SignalR

- `ConsertaPraMim.Web.Provider/Views/Shared/_Layout.cshtml`
  - conexao de notificacao com `withAutomaticReconnect()`;
  - dispatch de evento global `cpm:realtime-reconnected` no `notificationHub` e no `chatHub`.

## Estrategia de performance aplicada

- Diff de pins em vez de recriar camada completa.
- Tooltips permanentes desativadas em volume alto.
- Pins compactos em volume alto.
- Debounce de refresh em rajadas de notificacao.

## Validacao executada

- Build: `dotnet build src.sln -v minimal` (sucesso).
- Testes unitarios de servico: `ServiceRequestServiceTests` (11 aprovados).

## Arquivos principais alterados

- `Backend/src/ConsertaPraMim.Web.Provider/Controllers/HomeController.cs`
- `Backend/src/ConsertaPraMim.Web.Provider/Views/Home/Index.cshtml`
- `Backend/src/ConsertaPraMim.Web.Provider/Views/Shared/_Layout.cshtml`
- `Backend/src/ConsertaPraMim.Web.Provider/wwwroot/css/site.css`
