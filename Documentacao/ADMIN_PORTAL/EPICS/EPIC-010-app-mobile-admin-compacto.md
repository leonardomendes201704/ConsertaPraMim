# EPIC-010 - App mobile Admin compacto

Status: In Progress
Trilha: ADMIN_PORTAL

## Objetivo

Entregar um app mobile para operacao administrativa rapida, com foco em login seguro, visao executiva compacta, monitoramento da API e atendimento de chamados.

## Problema de negocio

- Operacoes administrativas dependem apenas do portal web.
- Em cenarios de rua/plantao, o tempo de resposta operacional aumenta.
- Nao existe canal mobile dedicado para triagem de incidentes e chamados.

## Resultado esperado

- Admin autentica no mobile com role Admin.
- App exibe KPIs compactos do dashboard e sinais de saude da plataforma.
- App permite monitorar API (overview e top endpoints) em poucos toques.
- App permite atuar em chamados de suporte (fila, detalhe, resposta e status).
- Pipeline gera APK do app Admin junto dos demais apps mobile.

## Metricas de sucesso

- 100% dos endpoints usados no app protegidos por `AdminOnly`.
- Tempo de abertura do app ate dashboard menor que 5 segundos em rede estavel.
- Build CI gera APK Admin e publica no fileserver sem passos manuais.

## Escopo

### Inclui

- Novo app mobile `conserta-pra-mim-admin app` no mesmo stack (React + Vite + Capacitor).
- Fluxo de login admin e sessao local.
- Dashboard compacto admin.
- Modulo de monitoramento operacional.
- Modulo de suporte (fila e detalhe de chamados).
- Atualizacao da automacao de build/upload de APK.

### Nao inclui

- Cobertura completa de todos os modulos web do portal admin.
- CRUD administrativo complexo em mobile (usuarios, catalogos completos, etc.).
- Notificacoes push dedicadas para admin (fica para evolucao posterior).

## Historias vinculadas

- ST-028 - Bootstrap do app mobile admin compacto.
- ST-029 - Autenticacao admin e sessao segura no mobile.
- ST-030 - Dashboard executivo e monitoramento compacto.
- ST-031 - Central de atendimento no app admin mobile.
- ST-032 - Pipeline CI para APK admin e rollout operacional.
