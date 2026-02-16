# ST-003 - Atualizacao em tempo real e performance do mapa

Status: Done  
Epic: EPIC-001

## Objetivo

Evoluir a atualizacao dos pins para tempo real e otimizar performance de renderizacao conforme volume de pedidos.

## Criterios de aceite

- Novo pedido relevante atualiza mapa sem refresh completo.
- Refresh periodico/pontual nao degrada experiencia de uso.
- Dashboard mantem tempo de resposta adequado com alto volume local.

## Tasks

- [x] Integrar atualizacao de mapa aos eventos existentes de notificacao.
- [x] Implementar estrategia de refresh incremental dos pins.
- [x] Definir limites de volume e paginacao geoespacial.
- [x] Otimizar renderizacao frontend para muitos marcadores.
- [x] Validar comportamento em cenarios de reconexao SignalR.

## Atualizacao de implementacao (2026-02-16)

- Integracao de eventos realtime:
- dashboard reage a `cpm:notification` e tambem a `cpm:realtime-reconnected`.
- reconexao de `notificationHub` e `chatHub` agora publica evento de reconexao para reidratar mapa/KPIs.
- Refresh incremental no frontend:
- marcadores passam a ser sincronizados por `requestId` com diff (upsert/remove), sem limpar e recriar toda a camada a cada atualizacao.
- popup/tooltip/icon sao atualizados somente quando o pin muda.
- Limites e paginacao geoespacial:
- endpoint `GET /Home/RecentMatchesData` recebeu `pinPage` e `pinPageSize`;
- payload de mapa inclui `PinPage`, `PinPageSize`, `TotalPins`, `HasMorePins`;
- backend aplica clamp de pagina/tamanho e limite maximo de busca.
- Otimizacao para alto volume:
- modo compacto de pin quando volume total e alto;
- tooltips permanentes desativados em volume elevado para reduzir custo de renderizacao;
- debounce de refresh para evitar rajada de render em notificacoes sequenciais.
- UX de volume:
- indicador `Carregados X/Y` no card do mapa;
- botao `Carregar mais pins` com paginacao incremental no cliente.
- Validacoes executadas:
- `dotnet build src.sln -v minimal` (sucesso);
- `dotnet test ..\tests\ConsertaPraMim.Tests.Unit\ConsertaPraMim.Tests.Unit.csproj --filter "FullyQualifiedName~ServiceRequestServiceTests"` (11/11 aprovados).
- Observacoes tecnicas registradas em:
- `Documentacao/PROVIDER_DASHBOARD_MAPA_COBERTURA/DOCUMENTACAO_TECNICA_ST-003.md`

## Diagramas

- Fluxo: `Documentacao/DIAGRAMAS/PROVIDER_DASHBOARD_MAPA_COBERTURA/ST-003-realtime-performance-mapa/fluxo-realtime-incremental-paginacao.mmd`
- Sequencia: `Documentacao/DIAGRAMAS/PROVIDER_DASHBOARD_MAPA_COBERTURA/ST-003-realtime-performance-mapa/sequencia-realtime-incremental-paginacao.mmd`
