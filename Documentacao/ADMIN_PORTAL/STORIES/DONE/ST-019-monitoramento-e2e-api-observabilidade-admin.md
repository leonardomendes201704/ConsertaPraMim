# ST-019 - Monitoramento E2E da API com telemetria, agregacao e dashboard admin

Status: Done
Epic: EPIC-007

## Objetivo

Entregar monitoramento completo de requests da API, com camada de observabilidade no backend e visualizacao analitica no portal admin.

## Criterios de aceite

- Middleware captura request/response de todos os endpoints da API com correlationId e route template.
- Dados sensiveis nao sao persistidos e erros sao normalizados/mascarados.
- Persistencia separa eventos brutos e agregados (hora/dia), com catalogo de erros.
- Job de agregacao e retencao roda em background sem derrubar a API em falhas de storage.
- Endpoints admin de monitoramento entregam overview, top endpoints, latencia, erros, lista paginada e detalhe por correlationId.
- Portal admin possui area Monitoramento com cards, graficos, tabelas, filtros e drilldown.
- Portal admin possui area de Testes de Carga com lista de runs, filtros e detalhe tecnico por execucao.
- Cobertura minima de testes (unit/integracao) para fluxo critico.
- Documentacao operacional e diagramas (fluxo + sequencia) atualizados.

## Tasks

- [x] Definir arquitetura de telemetria (evento bruto, agregados, catalogo de erro) e configuracoes.
- [x] Implementar middleware global de request telemetry com correlationId, severidade e warning collector.
- [x] Implementar buffer assíncrono (canal) + worker de flush para persistencia nao bloqueante.
- [x] Criar entidades/tabelas de monitoramento + indices + migracao EF Core.
- [x] Implementar worker de agregacao/retencao (hora/dia, erros, limpeza por janela).
- [x] Criar servico de consultas analiticas com filtros (range, endpoint, status, userId, tenantId, severidade).
- [x] Expor endpoints admin protegidos em `/api/admin/monitoring/*` com Swagger detalhado.
- [x] Integrar opcionalmente medidores nativos (`System.Diagnostics.Metrics`) para exportadores externos.
- [x] Implementar area "Monitoramento" no portal admin com cards, graficos, tabelas, paginacao e drilldown.
- [x] Adicionar estados de loading/erro/vazio e controles de filtro no frontend.
- [x] Adicionar seeds de telemetria para facilitar validacao local da UI.
- [x] Atualizar documentacao de operacao/configuracao e criar diagramas mermaid.
- [x] Integrar import/list/detail de runs de carga (`/api/admin/loadtests/*`) para analise no admin.
- [x] Adicionar tela "Testes de Carga" no portal admin consumindo endpoints dedicados.
- [x] Atualizar runner Python para publicacao opcional do report no admin ao final da execucao.

## Plano curto (arquitetura + passos)

1. Telemetria de entrada:
- Middleware captura metadados da requisicao e resposta;
- Evento vai para `Channel<T>` em memoria (nao bloqueante);
- Worker persiste em lote em `ApiRequestLogs`.

2. Camada analitica:
- Worker periodico recomputa agregados por hora/dia (`ApiEndpointMetricsHourly/Daily`);
- Cataloga erros normalizados em `ApiErrorCatalog` + ocorrencias em `ApiErrorOccurrencesHourly`;
- Aplica retencao de bruto e agregados por configuracao.

3. APIs admin:
- Controller `AdminMonitoringController` com endpoints de overview/top/latency/errors/requests/detail;
- Filtros por janela temporal e dimensoes operacionais.

4. Portal admin:
- Nova secao de menu "Monitoramento";
- Pagina com KPI cards, series temporais, distribuicoes, top tabelas e drilldown;
- Consulta via `IAdminOperationsApiClient`.

5. Governanca:
- Configuracoes de monitoramento no `appsettings`;
- Sem body/raw sensivel persistido;
- Diagramas e docs de execucao/validacao local.


