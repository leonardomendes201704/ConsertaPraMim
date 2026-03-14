using System.Data;
using AppMobileCPM.Integrations.Chatwoot;
using AppMobileCPM.Services;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace ConsertaPraMim.Tests.Unit.Services;

public sealed class SqlAdminKanbanServiceChatwootPersistenceTests
{
    [Fact(DisplayName = "EnsureInitialized deve criar colunas e indice de Chatwoot no kanban")]
    public void EnsureInitialized_DeveCriarColunasEIndiceChatwoot()
    {
        using var database = new LocalDbKanbanDatabaseScope();
        if (!database.IsAvailable)
        {
            return;
        }

        var service = CreateService(database.ConnectionString);

        _ = service.GetStages(AdminKanbanBoardTypes.Clients);

        using var connection = new SqlConnection(database.ConnectionString);
        connection.Open();

        using var columnsCommand = connection.CreateCommand();
        columnsCommand.CommandText = """
SELECT c.name
FROM sys.columns c
INNER JOIN sys.objects o ON o.object_id = c.object_id
WHERE o.type = 'U' AND o.name = 'cpm_web_kanban_leads'
ORDER BY c.column_id;
""";

        var columnNames = new List<string>();
        using (var reader = columnsCommand.ExecuteReader())
        {
            while (reader.Read())
            {
                columnNames.Add(reader.GetString(0));
            }
        }

        Assert.Contains("ChatwootContactId", columnNames);
        Assert.Contains("ChatwootConversationId", columnNames);
        Assert.Contains("ChatwootInboxId", columnNames);
        Assert.Contains("ChatwootSyncStatus", columnNames);
        Assert.Contains("ChatwootLastSyncAt", columnNames);
        Assert.Contains("ChatwootLastError", columnNames);

        using var webhookColumnsCommand = connection.CreateCommand();
        webhookColumnsCommand.CommandText = """
SELECT c.name
FROM sys.columns c
INNER JOIN sys.objects o ON o.object_id = c.object_id
WHERE o.type = 'U' AND o.name = 'cpm_web_chatwoot_webhook_events'
ORDER BY c.column_id;
""";

        var webhookColumnNames = new List<string>();
        using (var reader = webhookColumnsCommand.ExecuteReader())
        {
            while (reader.Read())
            {
                webhookColumnNames.Add(reader.GetString(0));
            }
        }

        Assert.Contains("ProviderEventId", webhookColumnNames);
        Assert.Contains("EventType", webhookColumnNames);
        Assert.Contains("ConversationId", webhookColumnNames);
        Assert.Contains("PayloadJson", webhookColumnNames);
        Assert.Contains("Signature", webhookColumnNames);
        Assert.Contains("ProcessStatus", webhookColumnNames);
        Assert.Contains("ProcessedAt", webhookColumnNames);
        Assert.Contains("ErrorMessage", webhookColumnNames);

        using var queueColumnsCommand = connection.CreateCommand();
        queueColumnsCommand.CommandText = """
SELECT c.name
FROM sys.columns c
INNER JOIN sys.objects o ON o.object_id = c.object_id
WHERE o.type = 'U' AND o.name = 'cpm_web_chatwoot_sync_queue'
ORDER BY c.column_id;
""";

        var queueColumnNames = new List<string>();
        using (var reader = queueColumnsCommand.ExecuteReader())
        {
            while (reader.Read())
            {
                queueColumnNames.Add(reader.GetString(0));
            }
        }

        Assert.Contains("LeadId", queueColumnNames);
        Assert.Contains("OperationType", queueColumnNames);
        Assert.Contains("Status", queueColumnNames);
        Assert.Contains("AttemptCount", queueColumnNames);
        Assert.Contains("MaxAttempts", queueColumnNames);
        Assert.Contains("NextAttemptAt", queueColumnNames);
        Assert.Contains("LastAttemptAt", queueColumnNames);
        Assert.Contains("LastError", queueColumnNames);
        Assert.Contains("WorkerInstance", queueColumnNames);
        Assert.Contains("ProcessedAt", queueColumnNames);
        Assert.Contains("DeadLetterAt", queueColumnNames);

        using var backfillColumnsCommand = connection.CreateCommand();
        backfillColumnsCommand.CommandText = """
SELECT c.name
FROM sys.columns c
INNER JOIN sys.objects o ON o.object_id = c.object_id
WHERE o.type = 'U' AND o.name = 'cpm_web_chatwoot_backfill_checkpoints'
ORDER BY c.column_id;
""";

        var backfillColumnNames = new List<string>();
        using (var reader = backfillColumnsCommand.ExecuteReader())
        {
            while (reader.Read())
            {
                backfillColumnNames.Add(reader.GetString(0));
            }
        }

        Assert.Contains("ScopeKey", backfillColumnNames);
        Assert.Contains("LastProcessedLeadId", backfillColumnNames);
        Assert.Contains("LastRunStartedAt", backfillColumnNames);
        Assert.Contains("LastRunCompletedAt", backfillColumnNames);
        Assert.Contains("LastRunStatus", backfillColumnNames);
        Assert.Contains("LastSummaryJson", backfillColumnNames);
        Assert.Contains("UpdatedAt", backfillColumnNames);

        using var indexCommand = connection.CreateCommand();
        indexCommand.CommandText = """
SELECT COUNT(1)
FROM sys.indexes
WHERE object_id = OBJECT_ID('dbo.cpm_web_kanban_leads')
  AND name = 'IX_cpm_web_kanban_leads_chatwoot_conversation';
""";

        Assert.Equal(1, Convert.ToInt32(indexCommand.ExecuteScalar()));

        using var webhookIndexCommand = connection.CreateCommand();
        webhookIndexCommand.CommandText = """
SELECT COUNT(1)
FROM sys.indexes
WHERE object_id = OBJECT_ID('dbo.cpm_web_chatwoot_webhook_events')
  AND name = 'IX_cpm_web_chatwoot_webhook_events_provider_event';
""";

        Assert.Equal(1, Convert.ToInt32(webhookIndexCommand.ExecuteScalar()));

        using var queueDueIndexCommand = connection.CreateCommand();
        queueDueIndexCommand.CommandText = """
SELECT COUNT(1)
FROM sys.indexes
WHERE object_id = OBJECT_ID('dbo.cpm_web_chatwoot_sync_queue')
  AND name = 'IX_cpm_web_chatwoot_sync_queue_due';
""";

        Assert.Equal(1, Convert.ToInt32(queueDueIndexCommand.ExecuteScalar()));

        using var queueActiveIndexCommand = connection.CreateCommand();
        queueActiveIndexCommand.CommandText = """
SELECT COUNT(1)
FROM sys.indexes
WHERE object_id = OBJECT_ID('dbo.cpm_web_chatwoot_sync_queue')
  AND name = 'UX_cpm_web_chatwoot_sync_queue_active';
""";

        Assert.Equal(1, Convert.ToInt32(queueActiveIndexCommand.ExecuteScalar()));

        using var backfillIndexCommand = connection.CreateCommand();
        backfillIndexCommand.CommandText = """
SELECT COUNT(1)
FROM sys.indexes
WHERE object_id = OBJECT_ID('dbo.cpm_web_chatwoot_backfill_checkpoints')
  AND name = 'UX_cpm_web_chatwoot_backfill_checkpoints_scope';
""";

        Assert.Equal(1, Convert.ToInt32(backfillIndexCommand.ExecuteScalar()));
    }

    [Fact(DisplayName = "UpdateLeadChatwootSync deve persistir e ler vinculo do Chatwoot no lead")]
    public void UpdateLeadChatwootSync_DevePersistirELerVinculoDoChatwoot()
    {
        using var database = new LocalDbKanbanDatabaseScope();
        if (!database.IsAvailable)
        {
            return;
        }

        var service = CreateService(database.ConnectionString);

        var leadId = service.CreateLead(new AdminKanbanLeadUpsertRequest
        {
            BoardType = AdminKanbanBoardTypes.Clients,
            StageId = 0,
            Name = "Lead Integracao Chatwoot",
            Phone = "(13) 99999-0000",
            Email = "lead.chatwoot@teste.com",
            ServiceCategory = "Encanador",
            Source = "Teste automatizado",
            Priority = "alta",
            StatusNote = "Lead criado para validar persistencia Chatwoot.",
            InternalNotes = "Nao remover",
            LastContactAt = new DateTime(2026, 3, 13, 12, 0, 0, DateTimeKind.Utc)
        });

        var firstSyncAt = new DateTime(2026, 3, 13, 13, 30, 0, DateTimeKind.Utc);
        var synced = service.UpdateLeadChatwootSync(leadId, new AdminKanbanLeadChatwootSyncUpdateRequest
        {
            ChatwootContactId = 101,
            ChatwootConversationId = 202,
            ChatwootInboxId = 1,
            ChatwootSyncStatus = "synced",
            ChatwootLastSyncAt = firstSyncAt,
            ChatwootLastError = "Erro antigo ja tratado"
        });

        Assert.True(synced);

        var secondSyncAt = new DateTime(2026, 3, 13, 14, 15, 0, DateTimeKind.Utc);
        var updated = service.UpdateLeadChatwootSync(leadId, new AdminKanbanLeadChatwootSyncUpdateRequest
        {
            ChatwootSyncStatus = "failed",
            ChatwootLastSyncAt = secondSyncAt,
            ClearChatwootLastError = true
        });

        Assert.True(updated);

        var details = service.GetLeadDetails(leadId);

        Assert.NotNull(details);
        Assert.Equal(101, details!.Chatwoot.ContactId);
        Assert.Equal(202, details.Chatwoot.ConversationId);
        Assert.Equal(1, details.Chatwoot.InboxId);
        Assert.Equal("failed", details.Chatwoot.SyncStatus);
        Assert.Equal(secondSyncAt, details.Chatwoot.LastSyncAt);
        Assert.Equal(string.Empty, details.Chatwoot.LastError);

        using var connection = new SqlConnection(database.ConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
SELECT ChatwootContactId, ChatwootConversationId, ChatwootInboxId, ChatwootSyncStatus, ChatwootLastSyncAt, ChatwootLastError
FROM dbo.cpm_web_kanban_leads
WHERE Id = @leadId;
""";
        command.Parameters.Add(new SqlParameter("@leadId", SqlDbType.Int) { Value = leadId });

        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal(101L, reader.GetInt64(0));
        Assert.Equal(202L, reader.GetInt64(1));
        Assert.Equal(1L, reader.GetInt64(2));
        Assert.Equal("failed", reader.GetString(3));
        Assert.Equal(secondSyncAt, reader.GetDateTime(4));
        Assert.True(reader.IsDBNull(5));
    }

    [Fact(DisplayName = "Fila de sincronizacao Chatwoot deve enfileirar, adquirir e finalizar item")]
    public void ChatwootSyncQueue_DevePersistirLifecycleDaFila()
    {
        using var database = new LocalDbKanbanDatabaseScope();
        if (!database.IsAvailable)
        {
            return;
        }

        var service = CreateService(database.ConnectionString);

        var leadId = service.CreateLead(new AdminKanbanLeadUpsertRequest
        {
            BoardType = AdminKanbanBoardTypes.Clients,
            StageId = 0,
            Name = "Lead Fila Chatwoot",
            Phone = "(13) 99111-2222",
            Email = "fila.chatwoot@teste.com",
            ServiceCategory = "Marceneiro",
            Source = "WhatsApp",
            Priority = "normal",
            StatusNote = "Aguardando reprocessamento",
            InternalNotes = string.Empty
        });

        var queuedAt = new DateTime(2026, 3, 13, 19, 0, 0, DateTimeKind.Utc);
        var queueItem = service.EnqueueChatwootSyncQueueItem(new AdminKanbanChatwootSyncQueueEnqueueRequest
        {
            LeadId = leadId,
            OperationType = ChatwootSyncOperationTypes.LeadSync,
            NextAttemptAt = queuedAt,
            MaxAttempts = 10,
            LastError = "Falha de rede"
        });

        Assert.Equal(ChatwootSyncQueueStatuses.Queued, queueItem.Status);
        Assert.Equal(leadId, queueItem.LeadId);
        Assert.Equal(ChatwootSyncOperationTypes.LeadSync, queueItem.OperationType);

        var acquired = service.AcquireDueChatwootSyncQueueItems(10, queuedAt.AddMinutes(1), "worker-chatwoot-1");

        Assert.Single(acquired);
        Assert.Equal(queueItem.Id, acquired[0].Id);
        Assert.Equal(ChatwootSyncQueueStatuses.Processing, acquired[0].Status);
        Assert.Equal(1, acquired[0].AttemptCount);
        Assert.Equal("worker-chatwoot-1", acquired[0].WorkerInstance);

        var retrying = service.FinalizeChatwootSyncQueueItem(new AdminKanbanChatwootSyncQueueFinalizeRequest
        {
            QueueItemId = queueItem.Id,
            FinalStatus = ChatwootSyncQueueStatuses.Retrying,
            FinalizedAt = queuedAt.AddMinutes(2),
            NextAttemptAt = queuedAt.AddMinutes(7),
            LastError = "Falha transiente",
            WorkerInstance = "worker-chatwoot-1"
        });

        Assert.NotNull(retrying);
        Assert.Equal(ChatwootSyncQueueStatuses.Retrying, retrying!.Status);
        Assert.Equal("Falha transiente", retrying.LastError);
        Assert.Equal(queuedAt.AddMinutes(7), retrying.NextAttemptAt);

        var resolved = service.CompleteActiveChatwootSyncQueueItems(
            leadId,
            ChatwootSyncOperationTypes.LeadSync,
            ChatwootSyncQueueStatuses.Processed,
            null,
            queuedAt.AddMinutes(8));

        Assert.Equal(1, resolved);

        using var connection = new SqlConnection(database.ConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
SELECT Status, AttemptCount, NextAttemptAt, LastError, WorkerInstance, ProcessedAt, DeadLetterAt
FROM dbo.cpm_web_chatwoot_sync_queue
WHERE Id = @id;
""";
        command.Parameters.Add(new SqlParameter("@id", SqlDbType.Int) { Value = queueItem.Id });

        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal(ChatwootSyncQueueStatuses.Processed, reader.GetString(0));
        Assert.Equal(1, reader.GetInt32(1));
        Assert.Equal(queuedAt.AddMinutes(7), reader.GetDateTime(2));
        Assert.True(reader.IsDBNull(3));
        Assert.Equal("worker-chatwoot-1", reader.GetString(4));
        Assert.False(reader.IsDBNull(5));
        Assert.True(reader.IsDBNull(6));
    }

    [Fact(DisplayName = "Webhook do Chatwoot deve persistir evento, localizar lead e atualizar ultimo contato")]
    public void ChatwootWebhook_DevePersistirEventoELastContactAt()
    {
        using var database = new LocalDbKanbanDatabaseScope();
        if (!database.IsAvailable)
        {
            return;
        }

        var service = CreateService(database.ConnectionString);

        var leadId = service.CreateLead(new AdminKanbanLeadUpsertRequest
        {
            BoardType = AdminKanbanBoardTypes.Clients,
            StageId = 0,
            Name = "Lead Webhook Chatwoot",
            Phone = "(13) 98888-7777",
            Email = "lead.webhook@teste.com",
            ServiceCategory = "Eletricista",
            Source = "WhatsApp",
            Priority = "normal",
            StatusNote = "Aguardando webhook",
            InternalNotes = string.Empty
        });

        var synced = service.UpdateLeadChatwootSync(leadId, new AdminKanbanLeadChatwootSyncUpdateRequest
        {
            ChatwootConversationId = 9901,
            ChatwootContactId = 6601,
            ChatwootInboxId = 1,
            ChatwootSyncStatus = "synced",
            ChatwootLastSyncAt = new DateTime(2026, 3, 13, 18, 0, 0, DateTimeKind.Utc)
        });

        Assert.True(synced);
        Assert.Equal(leadId, service.FindLeadIdByChatwootConversationId(9901));

        var receivedAt = new DateTime(2026, 3, 13, 18, 30, 0, DateTimeKind.Utc);
        var webhookEvent = service.CreateOrGetChatwootWebhookEvent(new AdminKanbanChatwootWebhookEventUpsertRequest
        {
            ProviderEventId = "delivery-sql-1",
            EventType = "message_created",
            ConversationId = 9901,
            PayloadJson = """{"event":"message_created","conversation":{"id":9901}}""",
            Signature = "assinatura",
            ReceivedAt = receivedAt
        });

        Assert.False(webhookEvent.IsDuplicate);
        Assert.Equal("received", webhookEvent.ProcessStatus);

        var duplicateWebhookEvent = service.CreateOrGetChatwootWebhookEvent(new AdminKanbanChatwootWebhookEventUpsertRequest
        {
            ProviderEventId = "delivery-sql-1",
            EventType = "message_created",
            ConversationId = 9901,
            PayloadJson = """{"event":"message_created","conversation":{"id":9901}}""",
            Signature = "assinatura",
            ReceivedAt = receivedAt
        });

        Assert.True(duplicateWebhookEvent.IsDuplicate);
        Assert.Equal(webhookEvent.Id, duplicateWebhookEvent.Id);

        var lastContactAt = new DateTime(2026, 3, 13, 18, 45, 0, DateTimeKind.Utc);
        var updated = service.ApplyChatwootWebhookLeadUpdate(leadId, new AdminKanbanLeadWebhookUpdateRequest
        {
            LastContactAt = lastContactAt,
            HistoryEventType = "chatwoot_mensagem_recebida",
            HistoryDescription = "Contato enviou nova mensagem na conversa do Chatwoot."
        });

        Assert.True(updated);
        Assert.True(service.CompleteChatwootWebhookEvent(webhookEvent.Id, "processed", null));

        var details = service.GetLeadDetails(leadId);
        Assert.NotNull(details);
        Assert.Equal(lastContactAt, details!.LastContactAt);
        Assert.Contains(details.History, item =>
            item.EventType == "chatwoot_mensagem_recebida" &&
            item.Description.Contains("nova mensagem", StringComparison.OrdinalIgnoreCase));

        using var connection = new SqlConnection(database.ConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
SELECT ProviderEventId, EventType, ConversationId, ProcessStatus, ProcessedAt, ErrorMessage
FROM dbo.cpm_web_chatwoot_webhook_events
WHERE Id = @id;
""";
        command.Parameters.Add(new SqlParameter("@id", SqlDbType.Int) { Value = webhookEvent.Id });

        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal("delivery-sql-1", reader.GetString(0));
        Assert.Equal("message_created", reader.GetString(1));
        Assert.Equal(9901L, reader.GetInt64(2));
        Assert.Equal("processed", reader.GetString(3));
        Assert.False(reader.IsDBNull(4));
        Assert.True(reader.IsDBNull(5));
    }

    [Fact(DisplayName = "Backfill Chatwoot deve listar candidatos pendentes e persistir checkpoint por escopo")]
    public void ChatwootBackfill_DeveListarCandidatosEPersistirCheckpoint()
    {
        using var database = new LocalDbKanbanDatabaseScope();
        if (!database.IsAvailable)
        {
            return;
        }

        var service = CreateService(database.ConnectionString);

        var leadId1 = service.CreateLead(new AdminKanbanLeadUpsertRequest
        {
            BoardType = AdminKanbanBoardTypes.Clients,
            StageId = 0,
            Name = "Lead Backfill 1",
            Phone = "(13) 99111-1111",
            Email = "backfill1@teste.com",
            ServiceCategory = "Eletricista",
            Source = "WhatsApp",
            Priority = "normal"
        });

        var leadId2 = service.CreateLead(new AdminKanbanLeadUpsertRequest
        {
            BoardType = AdminKanbanBoardTypes.Clients,
            StageId = 0,
            Name = "Lead Backfill 2",
            Phone = "(13) 99222-2222",
            Email = "backfill2@teste.com",
            ServiceCategory = "Encanador",
            Source = "Formulario",
            Priority = "normal"
        });

        var synced = service.UpdateLeadChatwootSync(leadId1, new AdminKanbanLeadChatwootSyncUpdateRequest
        {
            ChatwootConversationId = 8881,
            ChatwootContactId = 7771,
            ChatwootInboxId = 1,
            ChatwootSyncStatus = ChatwootSyncStatuses.Synced,
            ChatwootLastSyncAt = new DateTime(2026, 3, 13, 19, 0, 0, DateTimeKind.Utc)
        });

        Assert.True(synced);

        var candidates = service.ListChatwootBackfillCandidates(AdminKanbanBoardTypes.Clients, null, 20);

        Assert.Contains(candidates, item => item.LeadId == leadId2);
        Assert.DoesNotContain(candidates, item => item.LeadId == leadId1);

        var savedCheckpoint = service.SaveChatwootBackfillCheckpoint(new AdminKanbanChatwootBackfillCheckpointUpsertRequest
        {
            ScopeKey = "board:clientes",
            LastProcessedLeadId = leadId2,
            LastRunStartedAt = new DateTime(2026, 3, 13, 20, 0, 0, DateTimeKind.Utc),
            LastRunCompletedAt = new DateTime(2026, 3, 13, 20, 5, 0, DateTimeKind.Utc),
            LastRunStatus = "completed",
            LastSummaryJson = "{\"totalSelected\":1}"
        });

        Assert.Equal("board:clientes", savedCheckpoint.ScopeKey);
        Assert.Equal(leadId2, savedCheckpoint.LastProcessedLeadId);

        var reloadedCheckpoint = service.GetChatwootBackfillCheckpoint("board:clientes");

        Assert.NotNull(reloadedCheckpoint);
        Assert.Equal(leadId2, reloadedCheckpoint!.LastProcessedLeadId);
        Assert.Equal("completed", reloadedCheckpoint.LastRunStatus);

        using var connection = new SqlConnection(database.ConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
SELECT ScopeKey, LastProcessedLeadId, LastRunStatus
FROM dbo.cpm_web_chatwoot_backfill_checkpoints
WHERE ScopeKey = @scopeKey;
""";
        command.Parameters.Add(new SqlParameter("@scopeKey", SqlDbType.NVarChar, 80) { Value = "board:clientes" });

        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal("board:clientes", reader.GetString(0));
        Assert.Equal(leadId2, reader.GetInt32(1));
        Assert.Equal("completed", reader.GetString(2));
    }

    [Fact(DisplayName = "Diagnostico Chatwoot deve resumir status, erros recentes e fila operacional")]
    public void ChatwootDiagnostics_DeveRetornarResumoErrosEFila()
    {
        using var database = new LocalDbKanbanDatabaseScope();
        if (!database.IsAvailable)
        {
            return;
        }

        var service = CreateService(database.ConnectionString);

        var syncedLeadId = service.CreateLead(new AdminKanbanLeadUpsertRequest
        {
            BoardType = AdminKanbanBoardTypes.Clients,
            StageId = 0,
            Name = "Lead Synced",
            Phone = "(13) 99100-1000",
            Email = "synced@teste.com",
            ServiceCategory = "Eletricista",
            Source = "WhatsApp",
            Priority = "normal"
        });

        var failedLeadId = service.CreateLead(new AdminKanbanLeadUpsertRequest
        {
            BoardType = AdminKanbanBoardTypes.Clients,
            StageId = 0,
            Name = "Lead Failed",
            Phone = "(13) 99200-2000",
            Email = "failed@teste.com",
            ServiceCategory = "Encanador",
            Source = "Instagram",
            Priority = "alta"
        });

        var pendingLeadId = service.CreateLead(new AdminKanbanLeadUpsertRequest
        {
            BoardType = AdminKanbanBoardTypes.Providers,
            StageId = 0,
            Name = "Lead Pending",
            Phone = "(13) 99300-3000",
            Email = "pending@teste.com",
            ServiceCategory = "Pintor",
            Source = "Formulario",
            Priority = "normal"
        });

        Assert.True(service.UpdateLeadChatwootSync(syncedLeadId, new AdminKanbanLeadChatwootSyncUpdateRequest
        {
            ChatwootContactId = 1101,
            ChatwootConversationId = 2101,
            ChatwootInboxId = 1,
            ChatwootSyncStatus = ChatwootSyncStatuses.Synced,
            ChatwootLastSyncAt = new DateTime(2026, 3, 13, 21, 0, 0, DateTimeKind.Utc)
        }));

        Assert.True(service.UpdateLeadChatwootSync(failedLeadId, new AdminKanbanLeadChatwootSyncUpdateRequest
        {
            ChatwootContactId = 1102,
            ChatwootConversationId = 2102,
            ChatwootInboxId = 1,
            ChatwootSyncStatus = ChatwootSyncStatuses.Failed,
            ChatwootLastSyncAt = new DateTime(2026, 3, 13, 21, 10, 0, DateTimeKind.Utc),
            ChatwootLastError = "Falha ao atualizar labels no Chatwoot."
        }));

        var queuedItem = service.EnqueueChatwootSyncQueueItem(new AdminKanbanChatwootSyncQueueEnqueueRequest
        {
            LeadId = pendingLeadId,
            OperationType = ChatwootSyncOperationTypes.LeadSync,
            NextAttemptAt = new DateTime(2026, 3, 13, 21, 20, 0, DateTimeKind.Utc),
            MaxAttempts = 10,
            LastError = "Aguardando primeira execucao"
        });

        var deadLetterItem = service.EnqueueChatwootSyncQueueItem(new AdminKanbanChatwootSyncQueueEnqueueRequest
        {
            LeadId = failedLeadId,
            OperationType = ChatwootSyncOperationTypes.StageSync,
            NextAttemptAt = new DateTime(2026, 3, 13, 21, 21, 0, DateTimeKind.Utc),
            MaxAttempts = 3,
            LastError = "Falha inicial"
        });

        var finalizedDeadLetter = service.FinalizeChatwootSyncQueueItem(new AdminKanbanChatwootSyncQueueFinalizeRequest
        {
            QueueItemId = deadLetterItem.Id,
            FinalStatus = ChatwootSyncQueueStatuses.DeadLetter,
            FinalizedAt = new DateTime(2026, 3, 13, 21, 40, 0, DateTimeKind.Utc),
            LastError = "Tentativas esgotadas"
        });

        Assert.NotNull(finalizedDeadLetter);

        var diagnostics = service.GetChatwootDiagnostics(null, 10, 10);

        Assert.Equal(3, diagnostics.TotalLeads);
        Assert.Equal(1, diagnostics.SyncedCount);
        Assert.Equal(1, diagnostics.FailedCount);
        Assert.Equal(1, diagnostics.PendingCount);
        Assert.Equal(1, diagnostics.ActiveQueueCount);
        Assert.Equal(1, diagnostics.DeadLetterCount);
        Assert.Contains(diagnostics.RecentIssues, item =>
            item.LeadId == failedLeadId &&
            item.SyncStatus == ChatwootSyncStatuses.Failed &&
            item.LastError.Contains("labels", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(diagnostics.RecentQueueItems, item =>
            item.QueueItemId == queuedItem.Id &&
            item.Status == ChatwootSyncQueueStatuses.Queued);
        Assert.Contains(diagnostics.RecentQueueItems, item =>
            item.QueueItemId == deadLetterItem.Id &&
            item.Status == ChatwootSyncQueueStatuses.DeadLetter);
    }

    private static SqlAdminKanbanService CreateService(string connectionString)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connectionString
            })
            .Build();

        return new SqlAdminKanbanService(configuration);
    }

    private sealed class LocalDbKanbanDatabaseScope : IDisposable
    {
        private const string DefaultMasterConnectionString = "Server=(localdb)\\MSSQLLocalDB;Integrated Security=true;Initial Catalog=master;Encrypt=False;TrustServerCertificate=True;";
        private bool _disposed;
        private bool _databaseCreated;
        private readonly string _masterConnectionString;

        public LocalDbKanbanDatabaseScope()
        {
            DatabaseName = $"CpmFullChatwoot_{Guid.NewGuid():N}";
            _masterConnectionString = Environment.GetEnvironmentVariable("CPMFULL_SQLSERVER_TEST_MASTER_CONNECTION")
                ?? DefaultMasterConnectionString;
            ConnectionString = BuildDatabaseConnectionString(_masterConnectionString, DatabaseName);

            try
            {
                using var connection = new SqlConnection(_masterConnectionString);
                connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = $"CREATE DATABASE [{DatabaseName}];";
                command.ExecuteNonQuery();
                _databaseCreated = true;
                IsAvailable = true;
            }
            catch (Exception ex) when (ShouldBypassForUnavailableSqlServer(ex))
            {
                IsAvailable = false;
            }
        }

        public string DatabaseName { get; }

        public string ConnectionString { get; }

        public bool IsAvailable { get; }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            if (!_databaseCreated)
            {
                return;
            }

            using var connection = new SqlConnection(_masterConnectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = $"""
IF DB_ID('{DatabaseName}') IS NOT NULL
BEGIN
    ALTER DATABASE [{DatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [{DatabaseName}];
END;
""";
            command.ExecuteNonQuery();
        }

        private static string BuildDatabaseConnectionString(string masterConnectionString, string databaseName)
        {
            var builder = new SqlConnectionStringBuilder(masterConnectionString)
            {
                InitialCatalog = databaseName
            };

            return builder.ConnectionString;
        }

        private static bool ShouldBypassForUnavailableSqlServer(Exception ex)
        {
            return ex switch
            {
                SqlException => true,
                InvalidOperationException => true,
                _ when ex.InnerException is not null => ShouldBypassForUnavailableSqlServer(ex.InnerException),
                _ => false
            };
        }
    }
}
