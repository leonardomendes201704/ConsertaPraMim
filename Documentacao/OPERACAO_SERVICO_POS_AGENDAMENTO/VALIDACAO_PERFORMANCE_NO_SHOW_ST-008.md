# ST-008 - Validacao de performance das consultas de no-show

## Objetivo

Validar que as consultas do painel operacional de no-show suportam volume maior de dados com tempo de resposta dentro de budget operacional.

## Escopo validado

- Repositorio: `AdminNoShowDashboardRepository`
- Metodos:
  - `GetKpisAsync`
  - `GetBreakdownByCategoryAsync`
  - `GetBreakdownByCityAsync`
  - `GetOpenRiskQueueAsync`

## Base de teste utilizada

- `6000` agendamentos (`ServiceAppointments`)
- `6000` pedidos (`ServiceRequests`)
- `~1500` itens de fila de risco (`ServiceAppointmentNoShowQueueItems`)
- Distribuicao de:
  - cidades
  - categorias
  - status de agendamento
  - niveis de risco

## Cenarios de performance

1. Consulta sem filtros
- KPI + breakdown por categoria + breakdown por cidade + fila (take 100)
- Budget definido: `< 8s`

2. Consulta com filtros operacionais
- Filtros: cidade + categoria + risco alto
- KPI + breakdown por categoria + breakdown por cidade + fila (take 50)
- Budget definido: `< 6s`

## Automacao criada

- Teste de integracao:
  - `Backend/tests/ConsertaPraMim.Tests.Unit/Integration/Repositories/AdminNoShowDashboardRepositorySqlitePerformanceIntegrationTests.cs`
- Comando:
  - `dotnet test ..\tests\ConsertaPraMim.Tests.Unit\ConsertaPraMim.Tests.Unit.csproj --filter "AdminNoShowDashboardRepositorySqlitePerformanceIntegrationTests"`

## Resultado da validacao

- Status: `Aprovado`
- Total de testes: `1`
- Falhas: `0`
- Criterio de budget: `atendido`

## Ajustes tecnicos aplicados durante validacao

- Foi adicionado fallback para agregacao em memoria quando o provider nao for SQL Server.
  - Motivo: manter compatibilidade de execucao em SQLite (suite de integracao), sem alterar regra de negocio.
- Filtro de categoria legado foi normalizado para aceitar termos em ingles e equivalentes em pt-BR.
- Consulta de idade media da fila passou a calcular media por lista de datas de deteccao, preservando resultado operacional.

## Risco residual

- O benchmark automatizado usa SQLite em memoria para repetibilidade local/CI.
- Em producao (SQL Server), recomenda-se monitorar tempo real de resposta por endpoint para confirmar comportamento em volumes superiores.

