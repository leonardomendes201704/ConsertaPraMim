# ST-036 - FCM multi-dispositivo e rotacao de token E2E (Backend + 3 apps mobile)

Status: In Progress
Epic: EPIC-014

## Objetivo

Implementar um fluxo E2E de push notification resiliente para cliente, prestador e admin, com registro por instalacao e suporte real a multiplos dispositivos por usuario.

## Criterios de aceite

- Registro de push usa `installationId` estavel por instalacao e aceita rotacao de token.
- Backend faz `upsert` por `installationId` e/ou token, mantendo unicidade de token.
- Endpoints inferem `userId` pelo JWT (nao confiam em body).
- Um usuario pode ter varios devices ativos simultaneamente.
- Logout desativa apenas o device/instalacao alvo, sem derrubar todos os devices do usuario.
- Envio para usuario tenta todos os tokens ativos recentes e trata erro por token.
- Tokens invalidos/desregistrados sao desativados automaticamente.
- Job de limpeza desativa tokens stale e remove inativos antigos por politica.
- 3 apps mobile registram push no boot/login e no refresh de token.
- Observabilidade inclui logs de registro, rotacao, desativacao e taxa de falha.

## Tasks

- [x] Criar/atualizar documentacao da historia e arquitetura FCM multi-device.
- [x] Backend: evoluir entidade/tabela `MobilePushDevices` com `InstallationId`, `LastSeenAtUtc`, `RevokedAtUtc`.
- [x] Backend: refatorar repositorio/servico de register/unregister para `upsert` por instalacao/token.
- [x] Backend: adicionar endpoint admin/debug para auditoria de devices push.
- [x] Backend: adicionar job de limpeza de tokens stale/inativos com configuracao por janela.
- [x] Backend: reforcar observabilidade e motivos normalizados de falha por token.
- [x] Mobile cliente: enviar `installationId`, `appVersion`, `deviceModel`, `deviceId` no register/unregister.
- [x] Mobile prestador: enviar `installationId`, `appVersion`, `deviceModel`, `deviceId` no register/unregister.
- [x] Mobile admin: enviar `installationId`, `appVersion`, `deviceModel`, `deviceId` no register/unregister.
- [x] Adicionar/atualizar testes unitarios/integracao para cenarios de multi-dispositivo e rotacao.
- [x] Validar build/testes e registrar evidencias tecnicas da entrega.

## Validacao tecnica

Data: 22/02/2026

- Backend solution:
  - `dotnet build Backend/src/src.sln -v minimal`
  - Resultado: sucesso (0 erros, warnings pre-existentes mantidos).
- Testes unitarios:
  - `dotnet test Backend/src/src.sln -v minimal`
  - Resultado: sucesso (`426 passed`, `0 failed`).
- Mobile apps:
  - `npm.cmd run build` em `conserta-pra-mim app`
  - `npm.cmd run build` em `conserta-pra-mim-provider app`
  - `npm.cmd run build` em `conserta-pra-mim-admin app`
  - Resultado: sucesso nos 3 builds.
