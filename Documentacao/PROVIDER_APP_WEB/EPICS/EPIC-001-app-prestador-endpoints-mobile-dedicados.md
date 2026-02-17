# EPIC-001 - App do Prestador com Endpoints Mobile Dedicados

Status: In Progress
Trilha: PROVIDER_APP_WEB

## Objetivo

Disponibilizar um app dedicado ao prestador com experiencia equivalente ao portal do prestador para fluxo de atendimento e pedidos, usando contratos API exclusivos para mobile.

## Problema de negocio

- O prestador depende apenas do portal web para operar pedidos, propostas e agenda.
- O app cliente ja evoluiu com endpoints dedicados; o lado prestador ainda nao possui a mesma estrategia de isolamento.
- Reutilizar endpoints dos portais no app aumenta risco de quebra e acoplamento entre canais.

## Resultado esperado

- Novo app do prestador com autenticacao por email/senha (role Provider).
- Endpoints exclusivos sob `/api/mobile/provider/*` para dashboard, pedidos, propostas, agenda e chat.
- Paridade progressiva com fluxo do portal do prestador, sem alterar contratos ja consumidos pelos portais.
- Documentacao Swagger robusta (contexto de negocio, regras e erros esperados).

## Historias vinculadas

- ST-001 - Fundacao do app do prestador (login, dashboard e pedidos proximos)
- ST-002 - Detalhe de pedido e envio de proposta no app do prestador
- ST-003 - Agenda do prestador no app (pendencias e proximas visitas)
- ST-004 - Chat realtime cliente x prestador no app do prestador
- ST-005 - Fluxo operacional de atendimento no app (chegada, inicio, checklist, evidencias)
