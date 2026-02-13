# ST-001 - Hardening de seguranca para Admin

Status: Done  
Epic: EPIC-001

## Objetivo

Eliminar brechas de privilegio e garantir que apenas usuarios Admin autenticados acessem funcionalidades administrativas.

## Criterios de aceite

- Nao e possivel registrar `UserRole.Admin` via endpoint publico de cadastro.
- Todas as rotas admin exigem role/policy de Admin.
- Seed de usuario Admin fica restrito a ambiente controlado.
- Existe teste cobrindo tentativa de escalacao de privilegio.

## Tasks

- [x] Revisar `RegisterRequest` e `AuthService.RegisterAsync` para impedir role Admin em fluxo publico.
- [x] Criar fluxo seguro para criacao de admin inicial (seed controlado por ambiente/config).
- [x] Revisar controllers administrativos para uso consistente de `[Authorize(Roles = "Admin")]`.
- [x] Adicionar policy `AdminOnly` para padronizar autorizacao nas novas rotas.
- [x] Criar testes de integracao para:
- [x] Cadastro publico tentando criar Admin.
- [x] Usuario nao-admin tentando acessar endpoint admin.
- [x] Documentar regras de seguranca no changelog tecnico.
