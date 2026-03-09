# EPIC-030 - Dashboard Fire TV da landing no ecossistema Admin

## Objetivo

Disponibilizar um app read-only para Fire TV / Android TV que exiba continuamente os principais indicadores da landing publica, com autenticacao admin, auto refresh e operacao simples por controle remoto.

## Resultado esperado

- app proprio para Fire TV com login admin e foco em leitura 10-foot UI;
- oito KPIs principais da landing exibidos em tempo real com refresh automatico;
- heatmap basico, top origens, top localidades e sessoes recentes;
- configuracao runtime persistida em banco e editavel via `Configuracoes` no Portal Admin;
- processo padrao para build do APK e instalacao no Fire Stick via ADB.

## KPIs iniciais da fase 1

1. `totalSessions`
2. `uniqueVisitors`
3. `leadSubmissions`
4. `leadSubmissionRatePercent`
5. `leadModalOpens`
6. `totalClicks`
7. `averageActiveSecondsPerSession`
8. `averageMaxScrollPercent`

## Escopo

- endpoint administrativo dedicado `GET /api/admin/fire-tv/landing-dashboard`;
- runtime config `FireTvDashboard` persistida em `SystemSettings` e exposta na UI generica de configuracoes;
- app React + Capacitor + Android TV / Fire TV;
- build automatizado no script `scripts/build_apks.py`;
- documentacao operacional de instalacao, login, troubleshooting e rollout inicial.

## Fora de escopo

- reproduzir todas as telas do Portal Admin no Fire TV;
- operacao de edicao/cadastro pelo app de TV;
- push notifications no dispositivo Fire TV;
- distribuicao pela Amazon Appstore nesta fase.

## Historias relacionadas

- ST-064 - API e runtime config do dashboard Fire TV
- ST-065 - App Fire TV para KPIs da landing
- ST-066 - Build APK e instalacao no Fire Stick
