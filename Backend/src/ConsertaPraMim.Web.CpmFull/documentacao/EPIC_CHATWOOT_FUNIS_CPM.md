# EPIC-CHATWOOT-001 - Integracao Chatwoot com Funis CPM

## 1. Metadados da EPIC
- Epic ID: `EPIC-CHATWOOT-001`
- Produto: `AppMobileCPM`
- Data de criacao: `2026-03-13`
- Prioridade: `Alta`
- Status inicial: `Planned`
- Time alvo: `Backend`, `Frontend/Admin`, `Dados`, `DevOps`, `QA`
- Objetivo macro: integrar Chatwoot ao funil de `clientes` e `prestadores` sem perder o controle de funil do CPM.

## 2. Contexto atual
- O sistema possui funis Kanban persistidos em SQL Server:
- Tabela de etapas: `cpm_web_kanban_stages`
- Tabela de leads: `cpm_web_kanban_leads`
- Historico: `cpm_web_kanban_lead_history`
- O funil ja move cards com drag-and-drop e grava historico local.
- Ainda nao existe integracao com plataforma de atendimento conversacional (Chatwoot).

## 3. Objetivos de negocio
1. Centralizar atendimento em um canal unico (Chatwoot) sem perder o funil de operacao do CPM.
2. Garantir rastreabilidade ponta a ponta entre lead no funil e conversa no Chatwoot.
3. Reduzir retrabalho operacional (cadastro manual em ferramentas separadas).
4. Permitir evolucao futura de automacoes por etapa (SLA, alertas, follow-up).

## 4. Escopo

## 4.1 Em escopo
1. Criacao de integracao server-to-server com API do Chatwoot.
2. Sincronizacao de lead do CPM para contato e conversa no Chatwoot.
3. Atualizacao de status/labels/custom attributes da conversa ao mover etapa no funil CPM.
4. Recepcao de webhooks do Chatwoot para atualizar ultimo contato e historico do lead.
5. Mecanismo de retentativa e idempotencia.
6. Backfill inicial para leads existentes.
7. Tela admin basica para diagnostico de sincronizacao.

## 4.2 Fora de escopo (nesta EPIC)
1. Substituir o Kanban do CPM pelo Kanban interno do Chatwoot.
2. Criar bot conversacional/IA automatica.
3. Campanhas outbound em massa.
4. Integracao com WhatsApp provider (Meta/Twilio) fora do que ja estiver pronto no Chatwoot.
5. Sincronizacao bidirecional completa de todos os campos em tempo real.

## 5. Diretrizes tecnicas

## 5.1 Modelo de integracao recomendado
1. CPM continua sendo sistema de verdade do funil (`cpm_web_kanban_leads`).
2. Chatwoot atua como sistema de atendimento e conversa.
3. Vinculo tecnico por IDs:
- `ChatwootContactId`
- `ChatwootConversationId`
- `ChatwootInboxId`
4. Toda acao critica grava log tecnico + historico funcional.

## 5.2 Mapeamento de etapas para Chatwoot
1. Mapeamento por `BoardType + StageName`.
2. Para cada etapa, definir:
- `conversation_status` (`open`, `pending`, `resolved`, `snoozed`)
- `labels`
- `custom_attributes.funnel_stage`
- `custom_attributes.board_type`

## 5.3 Mapeamento inicial sugerido
1. Funil `clientes`:
- `Novo lead` -> status `open`, label `clientes_novo_lead`
- `Tentativa de contato` -> status `pending`, label `clientes_tentativa_contato`
- `Agendado` -> status `pending`, label `clientes_agendado`
- `Em atendimento` -> status `open`, label `clientes_em_atendimento`
- `Concluido` -> status `resolved`, label `clientes_concluido`
- `Perdido` -> status `resolved`, label `clientes_perdido`
2. Funil `prestadores`:
- `Novo cadastro` -> status `open`, label `prestadores_novo_cadastro`
- `Primeiro contato` -> status `pending`, label `prestadores_primeiro_contato`
- `Documentacao pendente` -> status `pending`, label `prestadores_documentacao_pendente`
- `Validacao tecnica` -> status `open`, label `prestadores_validacao_tecnica`
- `Ativo na plataforma` -> status `resolved`, label `prestadores_ativo_plataforma`
- `Inativo/Recusado` -> status `resolved`, label `prestadores_inativo_recusado`

## 6. Alteracoes de dados previstas

## 6.1 Alteracoes em `cpm_web_kanban_leads`
1. Adicionar `ChatwootContactId BIGINT NULL`
2. Adicionar `ChatwootConversationId BIGINT NULL`
3. Adicionar `ChatwootInboxId BIGINT NULL`
4. Adicionar `ChatwootSyncStatus NVARCHAR(30) NULL`
5. Adicionar `ChatwootLastSyncAt DATETIME2 NULL`
6. Adicionar `ChatwootLastError NVARCHAR(MAX) NULL`
7. Criar indice para busca por `ChatwootConversationId`

## 6.2 Novas tabelas
1. `cpm_web_chatwoot_webhook_events`
- Id (PK)
- ProviderEventId (nullable, unique quando existir)
- EventType
- PayloadJson
- Signature
- ReceivedAt
- ProcessedAt
- ProcessStatus
- ErrorMessage
2. `cpm_web_chatwoot_sync_queue`
- Id (PK)
- LeadId
- ActionType (`create_contact`, `create_conversation`, `sync_stage`, `sync_message`)
- PayloadJson
- AttemptCount
- NextAttemptAt
- LastError
- CreatedAt
- ProcessedAt

## 6.3 Configuracoes
1. `appsettings` com secao `Chatwoot` para ambiente local.
2. Em ambientes nao locais, usar variaveis de ambiente/secret store.
3. Campos minimos:
- `BaseUrl`
- `ApiAccessToken`
- `AccountId`
- `ClientsInboxId`
- `ProvidersInboxId`
- `WebhookSecret`
- `Enabled`

## 7. Historias e tasks detalhadas

## US-01 - Configuracao base da integracao
### Descricao
Como equipe tecnica, queremos configurar parametros e infraestrutura minima da integracao para permitir comunicacao segura com o Chatwoot.

### Criterios de aceite
1. Existe secao `Chatwoot` validada no startup.
2. A aplicacao falha de forma explicita quando `Enabled=true` e faltam credenciais obrigatorias.
3. Existe health check de conectividade com Chatwoot.

### Tasks
- `TASK-01.01` Criar `ChatwootOptions` com validacao de campos obrigatorios.
- `TASK-01.02` Registrar `IOptions<ChatwootOptions>` e validacao no `Program.cs`.
- `TASK-01.03` Implementar cliente HTTP tipado `IChatwootApiClient`.
- `TASK-01.04` Definir politicas de timeout e retry (transientes) no cliente HTTP.
- `TASK-01.05` Criar endpoint interno de health check da integracao.
- `TASK-01.06` Documentar variaveis de ambiente necessarias.
- `TASK-01.07` Validar comportamento com `Enabled=false` (sem impacto no fluxo atual).

## US-02 - Persistencia de vinculo Lead <-> Chatwoot
### Descricao
Como sistema, quero armazenar IDs tecnicos do Chatwoot no lead para rastrear e sincronizar a conversa.

### Criterios de aceite
1. Colunas de vinculo estao disponiveis em banco.
2. Leitura e escrita dessas colunas funcionam no repositorio de Kanban.
3. Migracoes/DDL sao idempotentes.

### Tasks
- `TASK-02.01` Criar script idempotente para `ALTER TABLE cpm_web_kanban_leads`.
- `TASK-02.02` Criar indice `IX_cpm_web_kanban_leads_chatwoot_conversation`.
- `TASK-02.03` Atualizar modelos C# de lead com campos Chatwoot.
- `TASK-02.04` Atualizar queries `SELECT/INSERT/UPDATE` do `SqlAdminKanbanService`.
- `TASK-02.05` Garantir backward compatibility para leads antigos sem IDs.
- `TASK-02.06` Criar teste de leitura/escrita dos novos campos.

## US-03 - Criar/atualizar contato no Chatwoot a partir do lead
### Descricao
Como operacao, quero que todo lead relevante tenha contato correspondente no Chatwoot para iniciar atendimento sem cadastro manual.

### Criterios de aceite
1. Lead novo gera contato no Chatwoot quando dados minimos estao presentes.
2. Se contato ja existir, sistema reaproveita e atualiza atributos.
3. Sem telefone/email validos, sistema nao quebra fluxo do lead e registra erro de sincronizacao.

### Tasks
- `TASK-03.01` Definir regra de identificacao unica do contato (telefone priorizado, fallback email).
- `TASK-03.02` Normalizar telefone para E.164 (quando possivel) antes de enviar.
- `TASK-03.03` Implementar fluxo `find or create contact`.
- `TASK-03.04` Mapear atributos: nome, telefone, email, tipo de funil, categoria de servico.
- `TASK-03.05` Gravar `ChatwootContactId` no lead.
- `TASK-03.06` Registrar evento no historico do lead: `chatwoot_contato_sincronizado`.
- `TASK-03.07` Criar testes para:
1. contato novo
2. contato existente
3. erro de API
4. dados incompletos

## US-04 - Criar conversa no Chatwoot para cada lead
### Descricao
Como atendente, quero receber uma conversa ja aberta no inbox certo para cada lead criado no CPM.

### Criterios de aceite
1. Lead de clientes abre conversa em inbox de clientes.
2. Lead de prestadores abre conversa em inbox de prestadores.
3. `ChatwootConversationId` fica salvo no lead.
4. Primeira mensagem da conversa contem resumo do lead.

### Tasks
- `TASK-04.01` Definir inbox por `BoardType`.
- `TASK-04.02` Implementar criacao de conversa via API Chatwoot.
- `TASK-04.03` Implementar postagem da primeira mensagem contextual:
1. nome
2. telefone/email
3. servico
4. cep/cidade
5. fonte
6. observacao inicial
- `TASK-04.04` Gravar `ChatwootConversationId` e `ChatwootInboxId`.
- `TASK-04.05` Registrar historico `chatwoot_conversa_criada`.
- `TASK-04.06` Criar tratamento para erros 4xx/5xx sem quebrar criacao do lead local.

## US-05 - Sincronizar mudanca de etapa do funil para Chatwoot
### Descricao
Como operacao, quero que mover card no Kanban reflita no status/labels da conversa no Chatwoot.

### Criterios de aceite
1. Cada mudanca de etapa atualiza status da conversa no Chatwoot conforme mapa.
2. Labels e custom attributes sao atualizados na conversa.
3. Em caso de falha externa, o card local continua movido e a sincronizacao entra em fila de retentativa.

### Tasks
- `TASK-05.01` Criar tabela/config de mapeamento de etapas para status/labels.
- `TASK-05.02` Implementar servico `SyncLeadStageToChatwoot`.
- `TASK-05.03` Integrar chamada no fluxo atual `SaveBoardOrder`.
- `TASK-05.04` Atualizar `custom_attributes` com:
1. `funnel_stage`
2. `board_type`
3. `lead_id`
- `TASK-05.05` Atualizar labels removendo label antigo e aplicando label novo.
- `TASK-05.06` Registrar historico `chatwoot_stage_synced`.
- `TASK-05.07` Em falha, registrar `ChatwootSyncStatus=failed` e enfileirar retentativa.
- `TASK-05.08` Adicionar teste de regressao para drag-and-drop sem impacto funcional.

## US-06 - Receber webhooks do Chatwoot no CPM
### Descricao
Como sistema, quero receber eventos do Chatwoot para enriquecer historico e ultimo contato do lead.

### Criterios de aceite
1. Endpoint de webhook autenticado por assinatura HMAC.
2. Eventos duplicados nao sao processados duas vezes.
3. Eventos relevantes atualizam lead e historico corretamente.

### Tasks
- `TASK-06.01` Criar endpoint `POST /api/integrations/chatwoot/webhook`.
- `TASK-06.02` Validar assinatura via `WebhookSecret`.
- `TASK-06.03` Persistir payload bruto em `cpm_web_chatwoot_webhook_events`.
- `TASK-06.04` Implementar idempotencia por `ProviderEventId + EventType + timestamp`.
- `TASK-06.05` Processar eventos:
1. `message_created`
2. `conversation_status_changed`
3. `conversation_updated`
- `TASK-06.06` Mapear `conversation_id` para `LeadId` local.
- `TASK-06.07` Atualizar `LastContactAt` quando houver mensagem valida.
- `TASK-06.08` Registrar no historico do lead o resumo do evento.
- `TASK-06.09` Retornar HTTP 2xx para evento aceito e 4xx para assinatura invalida.

## US-07 - Fila de sincronizacao e retentativa
### Descricao
Como time de plataforma, queremos evitar perda de sincronizacao quando Chatwoot estiver indisponivel.

### Criterios de aceite
1. Falhas de sincronizacao sao enfileiradas automaticamente.
2. Worker reprocessa itens com backoff exponencial.
3. Itens que excederem limite de tentativas sao marcados como `dead-letter`.

### Tasks
- `TASK-07.01` Criar tabela `cpm_web_chatwoot_sync_queue`.
- `TASK-07.02` Implementar enfileiramento para falhas em `create/sync`.
- `TASK-07.03` Criar `HostedService` para reprocessamento.
- `TASK-07.04` Definir politica de tentativas (ex.: 1m, 5m, 15m, 1h, 6h).
- `TASK-07.05` Definir limite maximo de tentativas (ex.: 10).
- `TASK-07.06` Registrar metricas de sucesso/falha/retry.
- `TASK-07.07` Adicionar endpoint admin de reprocessamento manual por lead.

## US-08 - Backfill inicial de leads existentes
### Descricao
Como operacao, quero sincronizar backlog de leads existentes sem interromper uso da plataforma.

### Criterios de aceite
1. Processo de backfill e executavel de forma incremental.
2. Backfill nao duplica contatos/conversas.
3. Processo gera relatorio final por status.

### Tasks
- `TASK-08.01` Implementar job `BackfillChatwootConversations`.
- `TASK-08.02` Selecionar leads sem `ChatwootConversationId`.
- `TASK-08.03` Processar em lotes com tamanho configuravel.
- `TASK-08.04` Implementar checkpoint por ultimo `LeadId` processado.
- `TASK-08.05` Gerar resumo: total, sucesso, falha, pendente.
- `TASK-08.06` Permitir dry-run para validacao.

## US-09 - Observabilidade, auditoria e diagnostico
### Descricao
Como time tecnico, queremos visibilidade completa da integracao para suporte rapido.

### Criterios de aceite
1. Logs estruturados com correlation id.
2. Dashboard admin com status de sincronizacao por lead.
3. Possibilidade de abrir conversa do Chatwoot direto do lead no admin.

### Tasks
- `TASK-09.01` Padronizar logs estruturados para todas as chamadas Chatwoot.
- `TASK-09.02` Adicionar correlation id por requisicao/evento.
- `TASK-09.03` Criar campos de status na UI admin do funil:
1. synced
2. pending
3. failed
- `TASK-09.04` Adicionar link direto `Abrir no Chatwoot` quando houver conversation id.
- `TASK-09.05` Criar tela/lista simples de erros de sincronizacao recentes.
- `TASK-09.06` Criar botao `Reprocessar sincronizacao` por lead.

## US-10 - Seguranca e conformidade
### Descricao
Como time de seguranca, queremos garantir protecao de credenciais e dados pessoais.

### Criterios de aceite
1. Token do Chatwoot nao aparece em logs.
2. Webhook rejeita payload sem assinatura valida.
3. Dados sensiveis mascarados em logs e telas tecnicas.

### Tasks
- `TASK-10.01` Bloquear logging de headers sensiveis.
- `TASK-10.02` Mascarar telefone/email em logs de erro.
- `TASK-10.03` Implementar whitelist de IP (se aplicavel na infra).
- `TASK-10.04` Revisar retention de payloads de webhook.
- `TASK-10.05` Documentar runbook de rotacao de token/secret.

## US-11 - QA, testes e homologacao
### Descricao
Como QA, queremos cobertura de testes para garantir estabilidade da integracao.

### Criterios de aceite
1. Casos felizes e de falha validados.
2. Testes de regressao do Kanban aprovados.
3. Plano de rollback definido.

### Tasks
- `TASK-11.01` Criar suite de testes de integracao com Chatwoot mockado.
- `TASK-11.02` Criar cenarios E2E:
1. criar lead cliente
2. criar lead prestador
3. mover etapa
4. receber webhook
- `TASK-11.03` Validar idempotencia de webhook com replay do mesmo evento.
- `TASK-11.04` Validar retentativa com indisponibilidade simulada.
- `TASK-11.05` Validar performance com lote de 1k leads.
- `TASK-11.06` Criar checklist de homologacao funcional com operacao.
- `TASK-11.07` Criar plano de rollback:
1. desabilitar integracao
2. manter funil local operacional
3. retomar fila apos incidente

## 8. Sequencia de entrega recomendada
1. Sprint 1:
- US-01
- US-02
- US-03
2. Sprint 2:
- US-04
- US-05
3. Sprint 3:
- US-06
- US-07
4. Sprint 4:
- US-08
- US-09
- US-10
- US-11

## 9. Dependencias externas
1. Conta Chatwoot ativa com API token de administrador.
2. Inboxs separados para clientes e prestadores.
3. Endpoint publico HTTPS para webhook.
4. Definicao final de regras de negocio de transicao de etapa.

## 10. Riscos e mitigacoes
1. Risco: duplicacao de contato no Chatwoot.
- Mitigacao: regra fixa de deduplicacao por telefone/email normalizado.
2. Risco: perda de evento webhook.
- Mitigacao: tabela de eventos + idempotencia + retentativa.
3. Risco: instabilidade externa do Chatwoot.
- Mitigacao: fila local + backoff + monitoramento.
4. Risco: regressao no drag-and-drop do Kanban.
- Mitigacao: testes de regressao UI e testes de sincronizacao assicrona.

## 11. Definicao de pronto (DoD) da EPIC
1. Leads novos de ambos os funis criam contato e conversa no Chatwoot.
2. Mudanca de etapa atualiza status/labels/custom attributes da conversa.
3. Webhooks relevantes atualizam historico e ultimo contato no CPM.
4. Falhas de sincronizacao ficam visiveis e reprocessaveis.
5. Documentacao tecnica e operacional publicada para time de suporte.
6. Homologacao aprovada por produto e operacao.

