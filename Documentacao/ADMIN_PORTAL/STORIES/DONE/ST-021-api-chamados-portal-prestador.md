# ST-021 - API de chamados para portal prestador

Status: Done  
Epic: EPIC-008

## Objetivo

Como prestador, quero abrir e acompanhar meus chamados para solicitar suporte direto ao admin sem sair da plataforma.

## Criterios de aceite

- Prestador consegue criar chamado com mensagem inicial.
- Prestador consegue listar apenas os proprios chamados com paginacao e filtros basicos.
- Prestador consegue visualizar detalhe do chamado com historico de mensagens.
- Prestador consegue enviar nova mensagem em chamado aberto.
- Prestador nao consegue acessar chamados de outro prestador.

## Tasks

- [x] Criar endpoint `POST /api/mobile/provider/support/tickets` (ou equivalente no namespace provider).
- [x] Criar endpoint `GET /api/mobile/provider/support/tickets` com filtros e paginacao.
- [x] Criar endpoint `GET /api/mobile/provider/support/tickets/{ticketId}` com historico de mensagens.
- [x] Criar endpoint `POST /api/mobile/provider/support/tickets/{ticketId}/messages`.
- [x] Criar endpoint `POST /api/mobile/provider/support/tickets/{ticketId}/close` (regra de fechamento pelo prestador).
- [x] Aplicar validacoes de payload e mascaramento de campos sensiveis quando necessario.
- [x] Garantir autorizacao por role/provider e ownership do ticket.
- [x] Criar testes de integracao cobrindo sucesso e negacao de acesso.
- [x] Criar/atualizar diagrama de fluxo Mermaid da funcionalidade.
- [x] Criar/atualizar diagrama de sequencia Mermaid da funcionalidade.

## Entregaveis tecnicos

- Repositorio dedicado de chamados:
  - `ConsertaPraMim.Domain/Repositories/ISupportTicketRepository.cs`
  - `ConsertaPraMim.Infrastructure/Repositories/SupportTicketRepository.cs`
- Endpoints provider mobile no `MobileProviderController`:
  - `POST /api/mobile/provider/support/tickets`
  - `GET /api/mobile/provider/support/tickets`
  - `GET /api/mobile/provider/support/tickets/{ticketId}`
  - `POST /api/mobile/provider/support/tickets/{ticketId}/messages`
  - `POST /api/mobile/provider/support/tickets/{ticketId}/close`
- DTOs e validacoes:
  - `ConsertaPraMim.Application/DTOs/MobileProviderSupportTicketDTOs.cs`
  - `ConsertaPraMim.Application/Validators/SupportTicketValidators.cs`
- Regras de ownership:
  - Leitura e escrita sempre filtradas por `ProviderId`.
  - Chamado de outro prestador retorna `not_found` (sem vazamento de existencia).

## Testes

- `MobileProviderSupportTicketServiceInMemoryIntegrationTests`:
  - fluxo feliz de criacao/listagem/mensagens/fechamento.
  - negacao de acesso entre prestadores.
- `MobileProviderSupportTicketsControllerInMemoryIntegrationTests`:
  - contratos de `201/200` para fluxo feliz.
  - `404` para acesso a chamado de outro prestador.

## Diagramas Mermaid

- Fluxo: `Documentacao/DIAGRAMAS/ADMIN_PORTAL/ST-021-api-chamados-portal-prestador/fluxo-api-chamados-portal-prestador.mmd`
- Sequencia: `Documentacao/DIAGRAMAS/ADMIN_PORTAL/ST-021-api-chamados-portal-prestador/sequencia-api-chamados-portal-prestador.mmd`
