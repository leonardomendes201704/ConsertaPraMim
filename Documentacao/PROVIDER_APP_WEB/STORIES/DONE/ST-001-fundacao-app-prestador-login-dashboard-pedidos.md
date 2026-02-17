# ST-001 - Fundacao do app do prestador (login, dashboard e pedidos proximos)

Status: Done
Epic: EPIC-001

## Objetivo

Entregar o primeiro slice funcional do app do prestador com autenticacao, dashboard operacional e listagem de oportunidades de atendimento via contratos mobile dedicados.

## Criterios de aceite

- App dedicado do prestador em projeto separado do app cliente.
- Login por email/senha integrado ao endpoint de autenticacao existente, validando role `Provider`.
- Endpoints mobile dedicados:
  - `GET /api/mobile/provider/dashboard`
  - `GET /api/mobile/provider/requests`
- Dashboard deve exibir KPIs minimos:
  - oportunidades proximas;
  - propostas em aberto;
  - propostas aceitas;
  - agendamentos pendentes;
  - proximas visitas confirmadas.
- Lista de pedidos deve retornar dados de card para app (categoria, descricao, distancia, status, data, indicador de proposta enviada).
- Endpoint e app documentados sem impacto em contratos dos portais.

## Tasks

- [x] Criar DTOs mobile provider para dashboard e cards de pedido.
- [x] Criar interface e servico de aplicacao para orquestrar dados do dashboard do prestador.
- [x] Criar controller API dedicado `/api/mobile/provider/*` com Swagger detalhado.
- [x] Registrar dependencias no `DependencyInjection`.
- [x] Criar novo projeto app `conserta-pra-mim-provider app` com base Vite + React + TS.
- [x] Implementar autenticacao do app do prestador com tratamento de indisponibilidade da API.
- [x] Implementar dashboard com KPIs e cards de pedidos proximos.
- [x] Integrar listagem de pedidos proximos com endpoint dedicado.
- [x] Validar build backend e app.
- [x] Gerar diagramas de fluxo e sequencia e atualizar indice de diagramas.

## Arquivos impactados

### Backend

- `Backend/src/ConsertaPraMim.Application/DTOs/MobileProviderDTOs.cs`
- `Backend/src/ConsertaPraMim.Application/Interfaces/IMobileProviderService.cs`
- `Backend/src/ConsertaPraMim.Application/Services/MobileProviderService.cs`
- `Backend/src/ConsertaPraMim.Application/DependencyInjection.cs`
- `Backend/src/ConsertaPraMim.API/Controllers/MobileProviderController.cs`

### App do prestador

- `conserta-pra-mim-provider app/package.json`
- `conserta-pra-mim-provider app/index.html`
- `conserta-pra-mim-provider app/index.tsx`
- `conserta-pra-mim-provider app/App.tsx`
- `conserta-pra-mim-provider app/types.ts`
- `conserta-pra-mim-provider app/services/auth.ts`
- `conserta-pra-mim-provider app/services/mobileProvider.ts`
- `conserta-pra-mim-provider app/components/SplashScreen.tsx`
- `conserta-pra-mim-provider app/components/Auth.tsx`
- `conserta-pra-mim-provider app/components/Dashboard.tsx`
- `conserta-pra-mim-provider app/components/RequestDetails.tsx`
- `conserta-pra-mim-provider app/components/Proposals.tsx`
- `conserta-pra-mim-provider app/components/Profile.tsx`

### Documentacao

- `Documentacao/PROVIDER_APP_WEB/INDEX.md`
- `Documentacao/PROVIDER_APP_WEB/EPICS/EPIC-001-app-prestador-endpoints-mobile-dedicados.md`
- `Documentacao/PROVIDER_APP_WEB/STORIES/DONE/ST-001-fundacao-app-prestador-login-dashboard-pedidos.md`
- `Documentacao/DIAGRAMAS/PROVIDER_APP_WEB/ST-001-fundacao-app-prestador-login-dashboard-pedidos/fluxo-app-prestador-login-dashboard-propostas.mmd`
- `Documentacao/DIAGRAMAS/PROVIDER_APP_WEB/ST-001-fundacao-app-prestador-login-dashboard-pedidos/sequencia-app-prestador-login-dashboard-propostas.mmd`
- `Documentacao/DIAGRAMAS/INDEX.md`

## Diagramas

- Fluxo:
  - `Documentacao/DIAGRAMAS/PROVIDER_APP_WEB/ST-001-fundacao-app-prestador-login-dashboard-pedidos/fluxo-app-prestador-login-dashboard-propostas.mmd`
- Sequencia:
  - `Documentacao/DIAGRAMAS/PROVIDER_APP_WEB/ST-001-fundacao-app-prestador-login-dashboard-pedidos/sequencia-app-prestador-login-dashboard-propostas.mmd`
