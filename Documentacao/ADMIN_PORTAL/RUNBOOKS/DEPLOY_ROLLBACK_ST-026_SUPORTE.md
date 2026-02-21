# Runbook ST-026 - Deploy e Rollback do modulo de suporte

Data base: 2026-02-20

## Objetivo

Executar rollout seguro do modulo de suporte prestador <-> admin com trilha de auditoria, observabilidade e plano de reversao rapido.

## Pre-deploy

- [ ] Branch alvo atualizada e build de release gerada.
- [ ] `dotnet build Backend/src/src.sln -c Release`.
- [ ] `dotnet test Backend/tests/ConsertaPraMim.Tests.Unit/ConsertaPraMim.Tests.Unit.csproj -c Release --filter "SupportTicket"`.
- [ ] Backup recente do banco SQL Server (`ConsertaPraMimDb`).
- [ ] Validar migrations aplicadas do modulo de suporte.
- [ ] Validar conectividade do `notificationHub` para portais admin e prestador.

## Deploy

1. Publicar `ConsertaPraMim.API`.
2. Publicar `ConsertaPraMim.Web.Admin`.
3. Publicar `ConsertaPraMim.Web.Provider`.
4. Reiniciar servicos e validar health checks.
5. Executar smoke de suporte:
- prestador abre chamado;
- admin visualiza fila e responde;
- prestador recebe atualizacao no detalhe;
- admin fecha e reabre chamado;
- prestador encerra chamado.

## Rollback

1. Reverter API + portais para o ultimo build estavel.
2. Reaplicar configuracoes anteriores de runtime (quando necessario).
3. Reiniciar servicos.
4. Validar smoke minimo:
- login admin/prestador;
- navegacao em suporte;
- sem erro 5xx em `/api/admin/support-tickets` e `/api/mobile/provider/support-tickets`.

## Evidencias obrigatorias

- Hash de commit deployado.
- Horario de inicio/fim do deploy.
- Resultado do smoke test.
- Registro de auditoria de ao menos 1 acao sensivel (`assign`, `status`, `close`, `reopen`).
- Logs de monitoramento sem erro critico apos 15 minutos.
