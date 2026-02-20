# ST-020 - Modelo de dominio e persistencia dos chamados

Status: In Progress  
Epic: EPIC-008

## Objetivo

Como plataforma, quero estruturar entidades e armazenamento de chamados para suportar atendimento prestador x admin com historico consistente.

## Criterios de aceite

- Existe entidade de chamado com status de ciclo de vida.
- Existe entidade de mensagem vinculada ao chamado e autor da mensagem.
- Relacao com prestador e (opcionalmente) admin responsavel persistida.
- Migration aplicada criando tabelas, indices e constraints essenciais.
- Leitura e escrita das entidades funcionam com EF Core sem regressao nas demais areas.

## Tasks

- [x] Criar entidade `SupportTicket` no dominio com campos de negocio (assunto, categoria, prioridade, status, owner, atribuicao, datas).
- [x] Criar entidade `SupportTicketMessage` com relacao 1:N para chamado.
- [x] Criar mapeamentos EF e adicionar `DbSet` no `ConsertaPraMimDbContext`.
- [x] Definir enum de status e regras iniciais de transicao.
- [x] Criar migration das tabelas, FKs e indices (`Status`, `ProviderId`, `AssignedAdminId`, `UpdatedAt`).
- [ ] Criar seed minimo de status/defaults (quando aplicavel).
- [x] Criar testes de repositorio para persistencia basica.
- [ ] Criar/atualizar diagrama de fluxo Mermaid da funcionalidade.
- [ ] Criar/atualizar diagrama de sequencia Mermaid da funcionalidade.

## Checklist tecnico - PR-01 (escopo minimo)

- [x] Criar enums de dominio (`SupportTicketStatus`, `SupportTicketPriority`).
- [x] Criar entidade `SupportTicket` com campos obrigatorios e invariantes basicas.
- [x] Criar entidade `SupportTicketMessage` com referencia ao chamado e autor.
- [x] Implementar metodos de dominio minimos (`AddMessage`, `AssignAdmin`, `ChangeStatus`).
- [x] Criar mapeamentos Fluent API das duas entidades.
- [x] Registrar `DbSet` e configuracoes no `ConsertaPraMimDbContext`.
- [x] Garantir build local da solucao sem regressao.
- [ ] Abrir PR focado apenas em modelo + mapeamento (sem migration neste primeiro passo).

## Checklist tecnico - PR-02 (migration)

- [x] Gerar migration `AddSupportTickets`.
- [x] Criar tabela `SupportTickets` com FK para `Users` (Provider e Admin opcional).
- [x] Criar tabela `SupportTicketMessages` com FK para `SupportTickets` e `Users`.
- [x] Criar indices de consulta operacional para fila e timeline.
- [x] Atualizar `ConsertaPraMimDbContextModelSnapshot`.
- [x] Garantir build local da solucao sem regressao apos migration.
