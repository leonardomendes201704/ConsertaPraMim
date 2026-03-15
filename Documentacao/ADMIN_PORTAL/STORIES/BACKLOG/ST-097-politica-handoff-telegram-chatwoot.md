# ST-097 - Politica operacional de handoff entre Telegram e Chatwoot

Status: Backlog
Epic: EPIC-TELEGRAM-002

## Objetivo

Definir e implementar regras operacionais claras para quando o bot atende, quando transfere para humano e quando pode retomar a conversa.

## Criterios de aceite

- Existem gatilhos explicitos de handoff por intencao, erro ou criterio operacional.
- O bot respeita o estado de handoff ativo e nao concorre com o humano.
- A operacao visualiza o estado e o motivo do handoff.
- A retomada, se existir, fica controlada e auditavel.

## Tasks

- [ ] Definir gatilhos de handoff automatico e manual.
- [ ] Persistir estados operacionais mais ricos de pausa/retomada.
- [ ] Expor o estado de handoff com clareza no CPM Full.
- [ ] Ajustar a trilha de mirror para obedecer as regras novas.
- [ ] Cobrir testes de concorrencia bot x humano.
