# ST-015 - Abertura de disputa com evidencias

Status: In Progress  
Epic: EPIC-006

## Objetivo

Criar fluxo formal para cliente ou prestador abrir disputa sobre atendimento, valor ou conduta, anexando evidencias e motivacao estruturada.

## Criterios de aceite

- Cliente e prestador podem abrir disputa em pedidos elegiveis.
- Formulario exige tipo de disputa, descricao e evidencia minima.
- Sistema congela estados criticos do pedido enquanto disputa estiver aberta.
- Ambas as partes visualizam status e prazo da disputa.
- Admin recebe caso em fila de mediacao com prioridade.
- Toda comunicacao da disputa fica registrada na trilha de auditoria.

## Tasks

- [x] Definir tipos de disputa e taxonomia de motivos.
- [x] Criar entidade `DisputeCase` com SLA, prioridade e ownership.
- [x] Criar endpoint de abertura de disputa por cliente/prestador.
- [x] Integrar upload de evidencias especificas da disputa.
- [x] Implementar regra de congelamento de fluxo do pedido.
- [x] Exibir card de disputa nas telas de cliente/prestador.
- [x] Notificar admin com roteamento para fila correta.
- [x] Criar auditoria de mensagens e anexos da disputa.
- [ ] Criar testes de autorizacao e elegibilidade de abertura.
- [ ] Atualizar manual com politicas de abertura de disputa.
