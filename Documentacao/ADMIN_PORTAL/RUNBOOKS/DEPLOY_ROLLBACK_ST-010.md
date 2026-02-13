# Runbook ST-010 - Deploy e Rollback do Portal Admin

Data base: 2026-02-13

## Objetivo

Executar rollout seguro do portal admin dedicado (`ConsertaPraMim.Web.Admin`) e desativar o admin legado embutido no portal do prestador por feature flag.

## Pre-deploy

- [ ] Confirmar branch `main` atualizado no ambiente.
- [ ] Executar `dotnet build Backend/src/src.sln`.
- [ ] Executar `dotnet test Backend/tests/ConsertaPraMim.Tests.Unit/ConsertaPraMim.Tests.Unit.csproj`.
- [ ] Validar configuracao `LegacyAdmin:Enabled=false` no `ConsertaPraMim.Web.Provider`.
- [ ] Validar credenciais/admin bootstrap em ambiente alvo.
- [ ] Garantir backup recente do banco de dados.

## Deploy

1. Publicar `ConsertaPraMim.API`.
2. Publicar `ConsertaPraMim.Web.Admin`.
3. Publicar `ConsertaPraMim.Web.Provider` com `LegacyAdmin:Enabled=false`.
4. Reiniciar servicos.
5. Validar health checks e logs iniciais.

## Smoke tests pos-deploy

- [ ] Login admin no novo portal funcionando.
- [ ] Dashboard admin carregando metricas.
- [ ] Tela de usuarios com acao de ativar/desativar funcionando.
- [ ] Tela de pedidos/propostas/chats carregando dados.
- [ ] Envio de notificacao manual funcionando.
- [ ] Acesso a `/Admin` no portal prestador retornando `404` com flag desligada.
- [ ] Verificar registros em `AdminAuditLogs` para ao menos 1 acao sensivel.

## Sinais de incidente

- Falhas de autorizacao para usuarios `Admin`.
- Erro 5xx em endpoints `/api/admin/*`.
- Ausencia de escrita em `AdminAuditLogs` apos acao admin.
- Falha de login no `ConsertaPraMim.Web.Admin`.

## Rollback

1. Reverter deploy para build anterior estavel (API/Web.Admin/Web.Provider).
2. Habilitar temporariamente `LegacyAdmin:Enabled=true` no `ConsertaPraMim.Web.Provider` se for necessario restaurar acesso admin operacional.
3. Reiniciar servicos.
4. Reexecutar smoke tests minimos:
   - login no portal prestador;
   - acesso admin legado;
   - operacao critica de suporte.
5. Abrir incidente com coleta de logs e hash de versao revertida.

## Evidencias recomendadas

- Hash do commit deployado.
- Data/hora de deploy e rollback (quando aplicavel).
- Capturas das telas de smoke test.
- Extrato de logs estruturados e auditoria administrativa.
