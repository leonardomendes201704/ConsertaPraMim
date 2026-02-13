# ST-003 - API Admin Dashboard e metricas

Status: Done  
Epic: EPIC-001

## Objetivo

Disponibilizar endpoint consolidado de metricas globais para o dashboard admin.

## Criterios de aceite

- Endpoint admin de dashboard retorna KPIs principais.
- Dados de usuarios, pedidos, propostas e chats retornam agregados por periodo.
- Endpoint paginado e preparado para filtros basicos.
- Endpoint protegido por autorizacao Admin.

## Tasks

- [x] Definir DTOs de metricas administrativas na camada Application.
- [x] Criar `IAdminDashboardService` e implementacao com consultas agregadas.
- [x] Adicionar controller `AdminDashboardController` na API.
- [x] Implementar endpoint `GET /api/admin/dashboard`.
- [x] Incluir indicadores minimos:
- [x] Total de usuarios por role e por status.
- [x] Pedidos por status.
- [x] Propostas enviadas/aceitas.
- [x] Conversas ativas nas ultimas 24h.
- [x] Criar testes de integracao para autorizacao e contrato de resposta.
