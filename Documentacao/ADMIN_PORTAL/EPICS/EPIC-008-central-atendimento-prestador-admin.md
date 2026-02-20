# EPIC-008 - Central de atendimento entre Prestador e Admin

Status: Backlog
Trilha: ADMIN_PORTAL

## Objetivo

Entregar um fluxo dedicado de suporte onde o prestador abre um chamado e conversa com o time administrativo ate a resolucao.

## Problema de negocio

- Duvidas operacionais dos prestadores ficam dispersas em canais externos.
- Nao existe fila oficial de atendimento dentro da plataforma.
- Falta rastreabilidade de historico, responsavel e status do atendimento.
- Nao existe visibilidade de SLA basico para priorizacao de resposta.

## Resultado esperado

- Prestador consegue abrir, acompanhar e responder chamados no portal do prestador.
- Admin consegue operar uma fila de atendimento com status, atribuicao e detalhe completo.
- Conversa fica registrada por chamado, com trilha cronologica.
- Solucao com autorizacao por perfil e auditoria das acoes administrativas.

## Metricas de sucesso

- 100% dos chamados com historico persistido e status consistente.
- Tempo medio da primeira resposta administrativa monitoravel por periodo.
- 0 vazamento de chamados entre prestadores diferentes.
- 100% das acoes sensiveis (atribuir/alterar status/fechar) com trilha de auditoria.

## Escopo

### Inclui

- Dominio e persistencia de chamados e mensagens.
- APIs para prestador (abrir/listar/detalhar/responder/fechar).
- APIs para admin (fila/detalhe/responder/atribuir/alterar status).
- UI de suporte no portal prestador.
- UI de atendimento no portal admin.
- Notificacao/realtime basico e indicadores de fila (MVP evolutivo).

### Nao inclui

- Integracao com ferramentas externas de service desk (Zendesk/Freshdesk/Jira Service Management).
- Chatbot de resposta automatica por IA.
- Aplicativo mobile administrativo dedicado.

## Historias vinculadas

- ST-020 - Modelo de dominio e persistencia dos chamados.
- ST-021 - API de chamados para portal prestador.
- ST-022 - API de atendimento para portal admin.
- ST-023 - UI de suporte no portal prestador.
- ST-024 - UI de atendimento no portal admin.
- ST-025 - Realtime, notificacoes e SLA basico da fila.
- ST-026 - Auditoria, testes E2E e rollout operacional.
