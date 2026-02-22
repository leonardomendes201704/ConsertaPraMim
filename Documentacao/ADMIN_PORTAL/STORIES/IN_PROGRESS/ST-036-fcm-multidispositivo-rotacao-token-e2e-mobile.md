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

- [ ] Criar/atualizar documentacao da historia e arquitetura FCM multi-device.
- [ ] Backend: evoluir entidade/tabela `MobilePushDevices` com `InstallationId`, `LastSeenAtUtc`, `RevokedAtUtc`.
- [ ] Backend: refatorar repositorio/servico de register/unregister para `upsert` por instalacao/token.
- [ ] Backend: adicionar endpoint admin/debug para auditoria de devices push.
- [ ] Backend: adicionar job de limpeza de tokens stale/inativos com configuracao por janela.
- [ ] Backend: reforcar observabilidade e motivos normalizados de falha por token.
- [ ] Mobile cliente: enviar `installationId`, `appVersion`, `deviceModel`, `deviceId` no register/unregister.
- [ ] Mobile prestador: enviar `installationId`, `appVersion`, `deviceModel`, `deviceId` no register/unregister.
- [ ] Mobile admin: enviar `installationId`, `appVersion`, `deviceModel`, `deviceId` no register/unregister.
- [ ] Adicionar/atualizar testes unitarios/integracao para cenarios de multi-dispositivo e rotacao.
- [ ] Validar build/testes e registrar evidencias tecnicas da entrega.
