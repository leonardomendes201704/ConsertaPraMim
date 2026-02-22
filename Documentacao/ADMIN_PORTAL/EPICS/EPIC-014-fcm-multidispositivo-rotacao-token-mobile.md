# EPIC-014 - FCM multi-dispositivo com rotacao de token (Cliente, Prestador e Admin)

Status: In Progress
Trilha: ADMIN_PORTAL

## Objetivo

Garantir notificacoes push robustas no ecossistema mobile (cliente, prestador e admin), suportando multiplos dispositivos por usuario, rotacao de token FCM e limpeza automatica de registros invalidos.

## Problema de negocio

- Push pode falhar quando token muda e o backend nao atualiza corretamente o vinculo da instalacao.
- O mesmo usuario pode usar mais de um dispositivo ao mesmo tempo.
- Um mesmo dispositivo pode alternar entre usuarios em logins diferentes.
- Nao ha governanca completa de ciclo de vida de token (last seen, revogacao e retencao).

## Resultado esperado

- Registro de device push por instalacao (`installationId`) com `upsert` seguro.
- Envio para todos os devices ativos do usuario, sem perder notificacao em cenarios multi-device.
- Tokens invalidos desativados automaticamente no retorno do Firebase.
- Rotina de limpeza periodica para dispositivos stale/inativos.
- Contrato unificado para os 3 apps mobile.

## Metricas de sucesso

- 100% dos apps mobile registram `installationId` no backend.
- Registro de push atualizado no boot, login e refresh de token.
- Queda de falhas por token invalido com auto-desativacao no primeiro erro definitivo.
- Reducao de incidentes de "usuario nao recebeu push" em cenarios com mais de um dispositivo.

## Escopo

### Inclui

- Evolucao do modelo `MobilePushDevices` para `installationId` + telemetria de last seen.
- Endpoints de register/unregister autenticados e idempotentes.
- Envio para multiplos tokens ativos com tratamento por token.
- Cleanup job para stale tokens.
- Atualizacao dos 3 apps (`conserta-pra-mim`, `conserta-pra-mim-provider`, `conserta-pra-mim-admin`).
- Historias e runbook de validacao operacional.

### Nao inclui

- Segmentacao avancada de campanhas push por perfil/comportamento.
- Centro de preferencias de push por tipo de evento.
- iOS APNs tuning especifico (entitlement/certificados).

## Historias vinculadas

- ST-036 - FCM multi-dispositivo e rotacao de token E2E (Backend + 3 Apps Mobile).
