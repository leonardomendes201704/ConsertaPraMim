# RUNBOOK - Incidentes, contingencia e rollback do Chatbot Telegram (ST-010)

## 1. Objetivo

Padronizar resposta operacional para incidentes do chatbot Telegram com IA, incluindo classificacao, mitigacao imediata, rollback e comunicacao.

## 2. Escopo

- Bridge Web (`ConsertaPraMim.Web.TelegramBridge`)
- Orquestrador IA (`TelegramChatbotOrchestrator`)
- Integracoes OpenAI e `ConsertaPraMim.API`
- Dashboard operacional (`GET /api/chatbot-observability/dashboard`)

## 3. Severidade e gatilhos

- `P0` (critico): indisponibilidade total do fluxo conversacional, loop de respostas incorretas de alta gravidade, vazamento de dado sensivel.
- `P1` (alto): erro recorrente de abertura de pedido/agendamento, falha de guardrail com risco operacional, fallback acima de 40% por 15 min.
- `P2` (medio): aumento de latencia, erros pontuais de dependencia, degradacao parcial sem bloqueio total.

## 4. Checklist de triagem rapida (primeiros 10 minutos)

1. Confirmar se o problema e reproduzivel no chat autenticado.
2. Consultar dashboard operacional com `X-Chatbot-Observability-Token` (quando ambiente nao-dev).
3. Validar metricas chaves:
   - `Ai.Failures`, `Ai.Fallbacks`, `Ai.P95LatencyMs`.
   - `TopErrors` e `RecentIncidents`.
   - `Dependencies` para `openai.responses`, `api.telegram_chatbot.*`, `api.service_appointments.slots`.
4. Verificar se houve bloqueio por feature flag (`rollout_not_enabled`, `rollout_outside_percentage`, `rollout_chat_blocked`).
5. Validar no historico conversacional da API os actions `openai_generate_reply`, `guardrail_intervention`, `schedule_batch_create`.

## 5. Contingencia imediata

### 5.1 Kill switch global

Ajustar configuracao:

- `TelegramBridgeAi:Enabled = false`

E reiniciar a aplicacao da bridge.

### 5.2 Rollback gradual (sem desligar tudo)

Ajustar configuracao:

- `TelegramChatbotRollout:RolloutPercentage = 0`
- Opcional: adicionar chat especifico em `BlockedChatIds`
- Opcional: restringir `EnabledEnvironments`

### 5.3 Fallback assistido

Quando fluxo automatico estiver degradado, manter atendimento humano via mensagem de handoff (`human_assisted_channel`).

## 6. Confirmacao pos-mitigacao

1. Enviar mensagem de teste no chat.
2. Garantir resposta valida sem violacao de guardrail.
3. Confirmar reducao de incidentes no dashboard (janela 15 min).
4. Validar persistencia de action/context snapshot na API.

## 7. Rollforward (retorno gradual)

1. Reativar `RolloutPercentage` em etapas: `5% -> 20% -> 50% -> 100%`.
2. Aguardar 15-30 min por etapa monitorando `fallback`, `errors` e `latencia`.
3. Se qualquer gatilho de `P0/P1` reaparecer, voltar imediatamente para etapa anterior.

## 8. Comunicacao operacional

Template minimo de comunicacao:

- `Incidente`: codigo interno + severidade (`P0/P1/P2`)
- `Inicio`: data/hora UTC
- `Impacto`: fluxos afetados (triagem/agendamento/consulta)
- `Mitigacao aplicada`: kill switch ou percentual de rollout
- `Status atual`: estabilizado / monitorando / rollback em andamento
- `Proximo update`: horario previsto

## 9. Evidencias obrigatorias para encerramento

- Captura do dashboard antes/depois.
- Lista de `errorCode` predominantes no incidente.
- Registro de configuracao aplicada no rollout/kill switch.
- Resultado de smoke de conversa (abertura pedido + consulta + agendamento).
