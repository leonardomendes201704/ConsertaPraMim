# ST-021 - API de chamados para portal prestador

Status: Backlog  
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

- [ ] Criar endpoint `POST /api/mobile/provider/support/tickets` (ou equivalente no namespace provider).
- [ ] Criar endpoint `GET /api/mobile/provider/support/tickets` com filtros e paginacao.
- [ ] Criar endpoint `GET /api/mobile/provider/support/tickets/{ticketId}` com historico de mensagens.
- [ ] Criar endpoint `POST /api/mobile/provider/support/tickets/{ticketId}/messages`.
- [ ] Criar endpoint `POST /api/mobile/provider/support/tickets/{ticketId}/close` (regra de fechamento pelo prestador).
- [ ] Aplicar validacoes de payload e mascaramento de campos sensiveis quando necessario.
- [ ] Garantir autorizacao por role/provider e ownership do ticket.
- [ ] Criar testes de integracao cobrindo sucesso e negacao de acesso.
- [ ] Criar/atualizar diagrama de fluxo Mermaid da funcionalidade.
- [ ] Criar/atualizar diagrama de sequencia Mermaid da funcionalidade.
