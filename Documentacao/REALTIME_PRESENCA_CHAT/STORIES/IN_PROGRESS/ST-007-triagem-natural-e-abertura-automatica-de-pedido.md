# ST-007 - Triagem natural e abertura automatica de pedido

Status: In Progress  
Epic: EPIC-002

## Objetivo

Permitir que o chatbot entenda o problema do cliente em linguagem natural e abra o pedido automaticamente pela API quando os dados minimos estiverem completos.

## Criterios de aceite

- IA identifica intencao de abertura de pedido e extrai dados do problema.
- Bot coleta dados faltantes (categoria, defeito, equipamento, marca/modelo, local e disponibilidade).
- Pedido e criado via API sem acao manual no portal.
- Cliente recebe confirmacao natural com resumo do que foi registrado.
- Conversa e estado da triagem ficam persistidos para continuidade.
- Testes cobrem cenarios nominais e lacunas de dados obrigatorios.

## Tasks

- [x] Definir contrato de intent `OpenServiceRequest` e entidades extraidas pela IA.
- [x] Mapear entidades extraidas para DTO de abertura de pedido da API.
- [x] Implementar state machine de triagem (dados completos/incompletos e perguntas de follow-up).
- [x] Garantir validacao de dados minimos antes de chamar endpoint de criacao de pedido.
- [x] Registrar no historico da conversa o payload final usado na abertura do pedido.
- [x] Implementar mensagens de confirmacao e recapitulacao amigaveis ao cliente.
- [ ] Criar testes unitarios de regras de completude e integracao da criacao de pedido.
- [ ] Criar/atualizar diagrama de fluxo Mermaid da funcionalidade.
- [ ] Criar/atualizar diagrama de sequencia Mermaid da funcionalidade.
