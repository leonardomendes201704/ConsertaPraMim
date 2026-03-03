# ST-004 - Fundacao API do chatbot Telegram e persistencia conversacional

Status: In Progress  
Epic: EPIC-002

## Objetivo

Criar a base de backend na `ConsertaPraMim.API` para suportar conversa Telegram mediada por IA, com persistencia de historico, contexto e acoes de negocio.

## Criterios de aceite

- Existem endpoints dedicados para sessao, conversa, mensagens e acoes do chatbot em `ConsertaPraMim.API`.
- Toda mensagem recebida/enviada pelo chatbot fica persistida em banco com `timestamp` em UTC.
- Conversa fica vinculada ao `ClientId` autenticado.
- Contrato de request/response do chatbot esta documentado no Swagger com contexto de negocio.
- Regras de autorizacao impedem acesso cruzado entre clientes.
- Cobertura minima de testes unitarios e integracao para fluxo basico de persistencia.

## Tasks

- [x] Definir entidades de dominio para `ChatbotConversation`, `ChatbotMessage`, `ChatbotContextSnapshot` e `ChatbotActionLog`.
- [x] Criar migration e mapeamento EF Core para persistencia completa de conversa/contexto.
- [x] Criar servicos de aplicacao para registrar entrada, saida, estado e eventos conversacionais.
- [ ] Criar endpoints API dedicados (`/api/telegram-chatbot/*`) para iniciar sessao, registrar mensagem, buscar historico e registrar acoes.
- [ ] Garantir persistencia de datas em UTC e conversao para `America/Sao_Paulo` apenas na exibicao quando aplicavel.
- [ ] Implementar autorizacao por cliente e trilha auditavel por `ClientId`.
- [ ] Atualizar Swagger com paridade nos arquivos: `ApiEndpointDocumentationCatalog`, `ComprehensiveSwaggerOperationFilter` e `ApiTagDescriptionsDocumentFilter`.
- [ ] Criar testes unitarios de servico e integracao para persistencia e autorizacao.
- [ ] Criar/atualizar diagrama de fluxo Mermaid da funcionalidade.
- [ ] Criar/atualizar diagrama de sequencia Mermaid da funcionalidade.
