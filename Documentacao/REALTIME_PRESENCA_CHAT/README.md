# Realtime Presenca e Chat Tracking

Este diretorio centraliza o planejamento das evolucoes de tempo real:

- Status operacional do prestador (Ausente, Online, EmAtendimento).
- Confirmacao de leitura no chat (estilo WhatsApp).
- Bridge web dedicada para atendimento Telegram (texto, anexos e realtime).
- Chatbot Telegram com IA para triagem, abertura de pedido, matching e agendamento.

## Estrutura

- `EPICS/`: visao macro de negocio.
- `STORIES/BACKLOG/`: historias ainda nao iniciadas.
- `STORIES/IN_PROGRESS/`: historias em andamento.
- `STORIES/DONE/`: historias concluidas.
- `MANUAL_QA_OPERACAO_TELEGRAM_BRIDGE_ST-003.md`: manual operacional e de QA da bridge Telegram.
- `MANUAL_QA_OPERACAO_CHATBOT_TELEGRAM.md`: manual operacional e de QA da trilha de chatbot Telegram com IA.
- `MANUAL_QA_OPERACAO_GOOGLE_CALENDAR_SYNC_ST-011.md`: manual de setup e QA da fundacao de sincronizacao com Google Calendar.

## Fluxo de trabalho

1. Ao iniciar uma historia, mover de `STORIES/BACKLOG/` para `STORIES/IN_PROGRESS/`.
2. Atualizar checkboxes das tasks na propria historia.
3. Ao concluir, mover para `STORIES/DONE/`.
4. Registrar o que foi entregue no changelog principal do projeto.
5. Criar/atualizar diagramas Mermaid (`fluxo` e `sequencia`) em `Documentacao/DIAGRAMAS/` e versionar no mesmo commit da funcionalidade.

Diagramas publicados nesta trilha:
- `Documentacao/DIAGRAMAS/REALTIME_PRESENCA_CHAT/ST-003-telegram-bridge-web-whatsapp-realtime/`
- `Documentacao/DIAGRAMAS/REALTIME_PRESENCA_CHAT/ST-004-fundacao-api-chatbot-telegram-persistencia/`
- `Documentacao/DIAGRAMAS/REALTIME_PRESENCA_CHAT/ST-011-fundacao-google-calendar-sync/`

## Convencao de IDs

- Epic: `EPIC-001`
- Story: `ST-001`, `ST-002`, ...
- Task: checklist dentro da story (`- [ ]` / `- [x]`)
