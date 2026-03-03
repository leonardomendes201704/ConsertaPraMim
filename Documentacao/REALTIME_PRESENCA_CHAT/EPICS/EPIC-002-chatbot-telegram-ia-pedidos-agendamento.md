# EPIC-002 - Chatbot Telegram com IA para triagem, pedido e agendamento

## Objetivo

Entregar um fluxo conversacional no Telegram, com linguagem natural e tom humano, para coletar o problema do cliente, abrir pedido automaticamente, listar prestadores elegiveis da area e conduzir agendamento.

## Problema atual

- O canal Telegram atual nao possui autenticacao do cliente por email/senha.
- A conversa nao usa IA para entender contexto de problema, intencao e historico.
- Nao existe fluxo E2E para abrir pedido e propor agendamento direto pela conversa.
- O cliente nao consegue consultar status de pedido em linguagem natural no Telegram.
- A persistencia conversacional ainda nao esta centralizada na `ConsertaPraMim.API`.

## Resultado esperado

- Cliente autentica no painel Telegram Bridge e conversa vinculada ao `ClientId`.
- Bot conversa com tom natural, contextual e nao robotico.
- IA identifica intencao, coleta dados faltantes e abre pedido pela API.
- Bot lista prestadores da area e organiza ate 3 visitas em dias distintos quando solicitado.
- Conversa completa, contexto, eventos e acoes ficam persistidos no banco via API.
- Cliente consulta status, pedidos e agendamentos em linguagem natural.

## Metricas de sucesso

- >= 85% das conversas concluem triagem sem intervencao humana.
- >= 90% dos pedidos abertos no Telegram sao criados com dados minimos validos.
- Tempo medio de resposta do bot <= 6s no p95.
- >= 95% das mensagens persistidas com trilha auditavel (entrada IA, acao API e resposta ao cliente).
- 0 regressao critica em autenticacao, abertura de pedido e agendamento.

## Escopo

### Inclui

- Login de cliente no Telegram Bridge com email/senha.
- Endpoints dedicados na `ConsertaPraMim.API` para sessao, conversa, contexto e acoes do chatbot.
- Orquestrador IA com OpenAI para compreensao de intencao e resposta natural.
- Fluxo de abertura de pedido e consulta de prestadores elegiveis na area.
- Fluxo de agendamento com limite de ate 3 visitas em dias distintos.
- Consulta natural de status de pedido, propostas e agenda.
- Persistencia completa de historico de conversa e contexto no banco.
- Observabilidade, guardrails, QA, runbook e rollout assistido.

### Nao inclui

- Atendimento por voz (audio para texto em tempo real).
- Integracao com WhatsApp oficial.
- Precificacao dinamica automatica por IA sem regra de negocio explicita.

## Historias vinculadas

- ST-004 - Fundacao API do chatbot Telegram e persistencia conversacional.
- ST-005 - Login do cliente no Telegram Bridge e vinculacao de conversa.
- ST-006 - Orquestrador OpenAI com contexto historico e linguagem humana.
- ST-007 - Triagem automatica e abertura de pedido via linguagem natural.
- ST-008 - Matching de prestadores e agendamento multi-visitas.
- ST-009 - Consulta natural de status, pedidos e agenda.
- ST-010 - Guardrails, observabilidade, QA e rollout operacional.
