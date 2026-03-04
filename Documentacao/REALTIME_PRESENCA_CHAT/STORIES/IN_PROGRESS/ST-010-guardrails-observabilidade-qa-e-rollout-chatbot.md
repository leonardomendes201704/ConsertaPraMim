# ST-010 - Guardrails, observabilidade, QA e rollout do chatbot

Status: In Progress  
Epic: EPIC-002

## Objetivo

Fechar o ciclo de confiabilidade da solucao com guardrails de IA, monitoramento, plano de QA, manual operacional e estrategia de rollout seguro.

## Criterios de aceite

- Existe politica de guardrails para respostas inseguras/fora de escopo e escalonamento.
- Logs e metricas cobrem ponta a ponta (Telegram, IA, API, agendamento, erros e latencia).
- Feature flag permite ativar/desativar chatbot por ambiente.
- Manual QA/Operacao documenta smoke, regressao, troubleshooting e rollback.
- Fluxo de rollout possui etapas, criterio de aceite e gatilho de rollback.
- Defeitos criticos possuem plano de contingencia e comunicacao operacional.

## Tasks

- [x] Definir guardrails conversacionais (tom, seguranca, proibicoes e handoff para atendimento humano).
- [x] Implementar catalogo de erros e mensagens de fallback padronizadas.
- [x] Instrumentar metricas de negocio e tecnicas (taxa de resolucao, latencia, custo IA, erro por endpoint).
- [x] Publicar dashboard operacional para acompanhamento do chatbot.
- [x] Implementar feature flag por ambiente para liberar chatbot gradualmente.
- [ ] Criar plano de testes QA (smoke, regressao, carga basica e cenarios de falha).
- [ ] Criar/atualizar manual QA/Operacao e runbook de incidentes/rollback.
- [ ] Atualizar changelog e status das stories no fechamento de cada entrega.
- [ ] Criar/atualizar diagrama de fluxo Mermaid da funcionalidade.
- [ ] Criar/atualizar diagrama de sequencia Mermaid da funcionalidade.
