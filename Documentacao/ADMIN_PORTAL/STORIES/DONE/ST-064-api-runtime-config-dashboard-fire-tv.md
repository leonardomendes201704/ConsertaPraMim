# ST-064 - API e runtime config do dashboard Fire TV

## Como
operacao/growth/admin

## Eu quero
uma API enxuta e uma configuracao runtime propria para o dashboard Fire TV da landing

## Para
alimentar uma tela executiva em TV sem depender do portal web desktop.

## Criterios de aceite

1. Existe endpoint autenticado `GET /api/admin/fire-tv/landing-dashboard` protegido por `AdminOnly`.
2. O payload retorna exatamente os 8 KPIs configurados, calorimetria agregada, top listas e sessoes recentes.
3. Toda configuracao funcional do app TV fica em `SystemSettings` pela secao runtime `FireTvDashboard`.
4. A secao `FireTvDashboard` aparece na tela de `Configuracoes` do Admin com defaults seguros e sem restart obrigatorio.
5. O endpoint respeita as janelas permitidas configuradas (`AllowedRangeDays`) e bloqueia valores fora da politica.

## Tasks

- [x] criar DTOs do snapshot Fire TV;
- [x] criar servico de agregacao `AdminFireTvDashboardService`;
- [x] criar runtime settings `FireTvDashboardRuntimeSettings`;
- [x] registrar dependencias e defaults em `RuntimeConfigSections`/`appsettings.json`;
- [x] criar controller autenticado na API;
- [x] documentar endpoint no catalogo Swagger;
- [x] cobrir servico/controller com testes unitarios.
