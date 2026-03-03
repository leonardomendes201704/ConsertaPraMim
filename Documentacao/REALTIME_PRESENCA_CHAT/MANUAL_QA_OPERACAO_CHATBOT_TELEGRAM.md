# Manual QA/Operacao - Chatbot Telegram (ST-004 a ST-010)

## 1. Objetivo

Padronizar QA e operacao para o fluxo de chatbot Telegram mediado por IA, incluindo persistencia conversacional na API, triagem automatica, abertura de pedido, matching, agendamento e consultas naturais.

## 2. Escopo atual da entrega

- ST-004 em andamento:
  - Entidades de dominio base para conversa, mensagens, snapshots de contexto e logs de acao.
  - Mapeamento EF Core + migration inicial de persistencia do chatbot.

## 3. Validacoes executadas no ciclo atual

- `dotnet build Backend/src/src.sln`
- `dotnet ef migrations add AddTelegramChatbotConversationFoundation --project Backend/src/ConsertaPraMim.Infrastructure --startup-project Backend/src/ConsertaPraMim.API --output-dir Migrations`

## 4. Checklist smoke inicial (em evolucao)

- [ ] QA-CBT-001: persistencia de conversa vinculada ao `ClientId` autenticado.
- [ ] QA-CBT-002: registro de mensagem inbound/outbound com `timestamp` UTC.
- [ ] QA-CBT-003: bloqueio de acesso cruzado entre clientes em historico/conversa.
- [ ] QA-CBT-004: registro de log de acao conversacional com trilha auditavel.

## 5. Troubleshooting inicial

### 5.1 Conversa nao persiste no banco

- Validar se migration da ST-004 foi aplicada no ambiente.
- Verificar string de conexao e disponibilidade do SQL Server.
- Revisar logs da API para erro de validacao de entidade.

### 5.2 Historico retorna vazio para cliente valido

- Confirmar `ClientId` do token JWT e vinculo da conversa no banco.
- Revisar filtros de autorizacao por cliente no endpoint.

## 6. Historico de revisoes

- 2026-03-03: versao inicial criada durante a ST-004 (Task 1).
- 2026-03-03: atualizacao com mapeamento EF Core e migration da ST-004 (Task 2).
