# ST-097 - Politica operacional de handoff entre Telegram e Chatwoot

Status: Done
Epic: EPIC-TELEGRAM-002
Data de conclusao: 2026-03-15

## Objetivo

Definir e implementar regras operacionais claras para quando o bot atende, quando transfere para humano e quando pode retomar a conversa.

## Criterios de aceite

- Existem gatilhos explicitos de handoff automatico ou manual.
- O bot respeita o estado de handoff ativo e nao concorre com o humano.
- A operacao visualiza o estado e o motivo do handoff.
- A retomada do bot fica controlada e auditavel.

## Tasks

- [x] Definir gatilhos de handoff automatico e manual.
- [x] Persistir estados operacionais mais ricos de pausa/retomada.
- [x] Expor o estado de handoff com clareza no CPM Full.
- [x] Ajustar a trilha de mirror para obedecer as regras novas.
- [x] Cobrir testes de concorrencia bot x humano.

## Entrega realizada

1. O `TelegramBridge` passou a manter estado rico de handoff por chat, com `status`, `motivo`, `source`, `startedAtUtc` e `updatedAtUtc`.
2. O `ConsertaPraMim.Web.CpmFull` agora persiste `HumanHandoffStatus`, `HumanHandoffReason` e `HumanHandoffUpdatedAt` em `dbo.cpm_web_telegram_funil_links`.
3. O modal do lead no Kanban passou a exibir `Estado do handoff`, `Motivo do handoff` e `Ultima atualizacao do handoff`, alem de permitir `Ativar handoff` e `Retomar bot`.
4. O espelhamento `Chatwoot -> Telegram` voltou a reativar handoff corretamente depois de uma retomada manual do bot, sem depender mais apenas de `HumanHandoffStartedAt`.
5. Foram adicionados testes de regressao para estado de handoff no bridge, reativacao apos retomada, acao manual no Kanban e supressao de resposta automatica enquanto o handoff estiver ativo.
