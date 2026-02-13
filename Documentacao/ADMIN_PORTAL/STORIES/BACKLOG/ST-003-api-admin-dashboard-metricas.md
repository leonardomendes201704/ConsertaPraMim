# ST-003 - API Admin Dashboard e metricas

Status: Backlog  
Epic: EPIC-001

## Objetivo

Disponibilizar endpoint consolidado de metricas globais para o dashboard admin.

## Criterios de aceite

- Endpoint admin de dashboard retorna KPIs principais.
- Dados de usuarios, pedidos, propostas e chats retornam agregados por periodo.
- Endpoint paginado e preparado para filtros basicos.
- Endpoint protegido por autorizacao Admin.

## Tasks

- [ ] Definir DTOs de metricas administrativas na camada Application.
- [ ] Criar `IAdminDashboardService` e implementacao com consultas agregadas.
- [ ] Adicionar controller `AdminDashboardController` na API.
- [ ] Implementar endpoint `GET /api/admin/dashboard`.
- [ ] Incluir indicadores minimos:
- [ ] Total de usuarios por role e por status.
- [ ] Pedidos por status.
- [ ] Propostas enviadas/aceitas.
- [ ] Conversas ativas nas ultimas 24h.
- [ ] Criar testes de integracao para autorizacao e contrato de resposta.

