# ST-007 - Triagem natural e abertura automatica de pedido

Status: Backlog  
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

- [ ] Definir contrato de intent `OpenServiceRequest` e entidades extraidas pela IA.
- [ ] Mapear entidades extraidas para DTO de abertura de pedido da API.
- [ ] Implementar state machine de triagem (dados completos/incompletos e perguntas de follow-up).
- [ ] Garantir validacao de dados minimos antes de chamar endpoint de criacao de pedido.
- [ ] Registrar no historico da conversa o payload final usado na abertura do pedido.
- [ ] Implementar mensagens de confirmacao e recapitulação amigaveis ao cliente.
- [ ] Criar testes unitarios de regras de completude e integracao da criacao de pedido.
- [ ] Criar/atualizar diagrama de fluxo Mermaid da funcionalidade.
- [ ] Criar/atualizar diagrama de sequencia Mermaid da funcionalidade.
