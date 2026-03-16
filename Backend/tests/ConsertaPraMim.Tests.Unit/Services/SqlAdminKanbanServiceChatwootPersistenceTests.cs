using System.Data;
using AppMobileCPM.Integrations.Chatwoot;
using AppMobileCPM.Integrations.Telegram;
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
        _ = service.GetJourneyDetails(-1);

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
        Assert.Contains("PayloadPurgedAt", webhookColumnNames);
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

        using var journeyColumnsCommand = connection.CreateCommand();
        journeyColumnsCommand.CommandText = """
SELECT c.name
FROM sys.columns c
INNER JOIN sys.objects o ON o.object_id = c.object_id
WHERE o.type = 'U' AND o.name = 'cpm_web_journey_executions'
ORDER BY c.column_id;
""";

        var journeyColumnNames = new List<string>();
        using (var reader = journeyColumnsCommand.ExecuteReader())
        {
            while (reader.Read())
            {
                journeyColumnNames.Add(reader.GetString(0));
            }
        }

        Assert.Contains("LastStageAutomationReason", journeyColumnNames);
        Assert.Contains("LastStageAutomationOrigin", journeyColumnNames);
        Assert.Contains("LastStageAutomationAtUtc", journeyColumnNames);
        Assert.Contains("ActiveTimerCode", journeyColumnNames);
        Assert.Contains("ActiveTimerDueAtUtc", journeyColumnNames);
        Assert.Contains("MatchingStatus", journeyColumnNames);
        Assert.Contains("MatchingSummary", journeyColumnNames);
        Assert.Contains("MatchingRequestedCategory", journeyColumnNames);
        Assert.Contains("MatchingRequestedSubcategory", journeyColumnNames);
        Assert.Contains("MatchingEvaluatedProviders", journeyColumnNames);
        Assert.Contains("MatchingEligibleProviders", journeyColumnNames);
        Assert.Contains("MatchingCandidatesJson", journeyColumnNames);
        Assert.Contains("MatchingLastRunAtUtc", journeyColumnNames);
        using var journeyTimerIndexCommand = connection.CreateCommand();
        journeyTimerIndexCommand.CommandText = """
SELECT COUNT(1)
FROM sys.indexes
WHERE object_id = OBJECT_ID('dbo.cpm_web_journey_executions')
  AND name = 'IX_cpm_web_journey_executions_active_timer';
""";

        Assert.Equal(1, Convert.ToInt32(journeyTimerIndexCommand.ExecuteScalar()));

        using var telegramColumnsCommand = connection.CreateCommand();
        telegramColumnsCommand.CommandText = """
SELECT c.name
FROM sys.columns c
INNER JOIN sys.objects o ON o.object_id = c.object_id
WHERE o.type = 'U' AND o.name = 'cpm_web_telegram_funil_links'
ORDER BY c.column_id;
""";

        var telegramColumnNames = new List<string>();
        using (var reader = telegramColumnsCommand.ExecuteReader())
        {
            while (reader.Read())
            {
                telegramColumnNames.Add(reader.GetString(0));
            }
        }

        Assert.Contains("ChatbotConversationId", telegramColumnNames);
        Assert.Contains("LeadId", telegramColumnNames);
        Assert.Contains("BoardType", telegramColumnNames);
        Assert.Contains("ChannelConversationId", telegramColumnNames);
        Assert.Contains("TelegramChatId", telegramColumnNames);
        Assert.Contains("ClientId", telegramColumnNames);
        Assert.Contains("ClientPhone", telegramColumnNames);
        Assert.Contains("ClientEmail", telegramColumnNames);
        Assert.Contains("ServiceRequestId", telegramColumnNames);
        Assert.Contains("HumanHandoffStartedAt", telegramColumnNames);
        Assert.Contains("HumanHandoffStatus", telegramColumnNames);
        Assert.Contains("HumanHandoffReason", telegramColumnNames);
        Assert.Contains("HumanHandoffUpdatedAt", telegramColumnNames);
        Assert.Contains("LastTelegramMessageSyncedAt", telegramColumnNames);
        Assert.Contains("LastChatwootMessageSyncedAt", telegramColumnNames);

        using var telegramUniqueIndexCommand = connection.CreateCommand();
        telegramUniqueIndexCommand.CommandText = """
SELECT COUNT(1)
FROM sys.indexes
WHERE object_id = OBJECT_ID('dbo.cpm_web_telegram_funil_links')
  AND name = 'UX_cpm_web_telegram_funil_links_conversation';
""";

        Assert.Equal(1, Convert.ToInt32(telegramUniqueIndexCommand.ExecuteScalar()));
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

    [Fact(DisplayName = "UpsertTelegramLead deve persistir e expor vinculo Telegram no detalhe do lead")]
    public void UpsertTelegramLead_DevePersistirEExporVinculoTelegramNoDetalhe()
    {
        using var database = new LocalDbKanbanDatabaseScope();
        if (!database.IsAvailable)
        {
            return;
        }

        var service = CreateService(database.ConnectionString);
        var chatbotConversationId = Guid.Parse("7d46e080-26f0-4a78-ae9c-50f75951ed88");
        var clientId = Guid.Parse("9c7cbf35-0b16-4f3b-a68b-697b7b66c5d3");
        var serviceRequestId = Guid.Parse("84eec1a7-e7dc-4f8f-a0c6-b0f6a3fec3fb");

        var result = service.UpsertTelegramLead(new AdminKanbanTelegramLeadUpsertRequest
        {
            BoardType = AdminKanbanBoardTypes.Clients,
            ChatbotConversationId = chatbotConversationId,
            ChannelConversationId = "telegram-client-001",
            TelegramChatId = 5513997114422,
            ClientId = clientId,
            ClientName = "Ricardo Almeida",
            ClientPhone = "+5513997114422",
            ClientEmail = "ricardo@email.com",
            ServiceRequestId = serviceRequestId,
            ServiceCategory = "Eletricista",
            PostalCode = "11701-200",
            City = "Praia Grande",
            StatusNote = "Lead criado pelo bot Telegram para clientes.",
            InternalNotes = "Contexto inicial da conversa Telegram.",
            LastContactAt = new DateTime(2026, 3, 14, 12, 30, 0, DateTimeKind.Utc)
        });

        Assert.True(result.Created);

        var synced = service.UpdateLeadChatwootSync(result.LeadId, new AdminKanbanLeadChatwootSyncUpdateRequest
        {
            ChatwootContactId = 901,
            ChatwootConversationId = 902,
            ChatwootInboxId = 1,
            ChatwootSyncStatus = "synced",
            ChatwootLastSyncAt = new DateTime(2026, 3, 14, 12, 35, 0, DateTimeKind.Utc)
        });

        Assert.True(synced);

        var details = service.GetLeadDetails(result.LeadId);

        Assert.NotNull(details);
        Assert.Equal("Telegram", details!.Source);
        Assert.Equal(chatbotConversationId, details.Telegram.ChatbotConversationId);
        Assert.Equal("telegram-client-001", details.Telegram.ChannelConversationId);
        Assert.Equal(5513997114422, details.Telegram.TelegramChatId);
        Assert.Equal(clientId, details.Telegram.ClientId);
        Assert.Equal("+5513997114422", details.Telegram.ClientPhone);
        Assert.Equal("ricardo@email.com", details.Telegram.ClientEmail);
        Assert.Equal(serviceRequestId, details.Telegram.ServiceRequestId);
        Assert.True(details.Telegram.UpdatedAt.HasValue);
        Assert.Equal("+5513997114422", details.Phone);
        Assert.Equal(902, details.Chatwoot.ConversationId);
        Assert.Contains(details.History, item => item.EventType == "telegram_lead_criado");

        using var connection = new SqlConnection(database.ConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
SELECT ChatbotConversationId, ChannelConversationId, TelegramChatId, ClientId, ClientPhone, ClientEmail, ServiceRequestId
FROM dbo.cpm_web_telegram_funil_links
WHERE LeadId = @leadId;
""";
        command.Parameters.Add(new SqlParameter("@leadId", SqlDbType.Int) { Value = result.LeadId });

        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal(chatbotConversationId, reader.GetGuid(0));
        Assert.Equal("telegram-client-001", reader.GetString(1));
        Assert.Equal(5513997114422L, reader.GetInt64(2));
        Assert.Equal(clientId, reader.GetGuid(3));
        Assert.Equal("+5513997114422", reader.GetString(4));
        Assert.Equal("ricardo@email.com", reader.GetString(5));
        Assert.Equal(serviceRequestId, reader.GetGuid(6));
    }

    [Fact(DisplayName = "Diagnostico Telegram deve resumir fila e permitir retentativa manual")]
    public void TelegramDiagnostics_DeveResumirFilaEPermitirRetentativaManual()
    {
        using var database = new LocalDbKanbanDatabaseScope();
        if (!database.IsAvailable)
        {
            return;
        }

        var service = CreateService(database.ConnectionString);
        var chatbotConversationId = Guid.Parse("b90f7527-b32c-4387-b8c2-a689d0f6edc9");
        var clientId = Guid.Parse("c5e77036-6175-4cc4-8d6f-93fe0a1d3ec3");
        var referenceUtc = new DateTime(2026, 3, 14, 16, 0, 0, DateTimeKind.Utc);

        var upsert = service.UpsertTelegramLead(new AdminKanbanTelegramLeadUpsertRequest
        {
            BoardType = AdminKanbanBoardTypes.Clients,
            ChatbotConversationId = chatbotConversationId,
            ChannelConversationId = "telegram-client-diagnostics",
            TelegramChatId = 5513997000001,
            ClientId = clientId,
            ClientName = "Lead Diagnostico Telegram",
            ClientEmail = "diagnostico.telegram@teste.com",
            ServiceCategory = "Eletricista",
            City = "Praia Grande",
            StatusNote = "Lead criado para validar diagnostico Telegram.",
            LastContactAt = referenceUtc
        });

        var touched = service.TouchTelegramLeadLink(upsert.LeadId, new AdminKanbanTelegramLinkTouchRequest
        {
            HumanHandoffStartedAt = referenceUtc.AddMinutes(3),
            HumanHandoffStatus = TelegramHandoffPolicy.ActiveStatus,
            HumanHandoffReason = TelegramHandoffPolicy.ChatwootFirstHumanReplyReasonLabel,
            HumanHandoffUpdatedAt = referenceUtc.AddMinutes(3),
            LastTelegramMessageSyncedAt = referenceUtc.AddMinutes(1),
            LastChatwootMessageSyncedAt = referenceUtc.AddMinutes(2)
        });

        Assert.True(touched);

        var touchedDetails = service.GetLeadDetails(upsert.LeadId);
        Assert.NotNull(touchedDetails);
        Assert.Equal(referenceUtc.AddMinutes(3), touchedDetails!.Telegram.HumanHandoffStartedAt);
        Assert.Equal(TelegramHandoffPolicy.ActiveStatus, touchedDetails.Telegram.HumanHandoffStatus);
        Assert.Equal(TelegramHandoffPolicy.ChatwootFirstHumanReplyReasonLabel, touchedDetails.Telegram.HumanHandoffReason);
        Assert.Equal(referenceUtc.AddMinutes(3), touchedDetails.Telegram.HumanHandoffUpdatedAt);
        Assert.Equal(referenceUtc.AddMinutes(1), touchedDetails.Telegram.LastTelegramMessageSyncedAt);
        Assert.Equal(referenceUtc.AddMinutes(2), touchedDetails.Telegram.LastChatwootMessageSyncedAt);

        var queued = service.EnqueueTelegramDeliveryQueueItem(new AdminKanbanTelegramDeliveryQueueEnqueueRequest
        {
            LeadId = upsert.LeadId,
            Direction = TelegramDeliveryDirections.TelegramToChatwoot,
            DeliveryKey = "telegram-diagnostics-001",
            PayloadJson = """{"message":"teste"}""",
            ChatwootConversationId = 902,
            TelegramChatId = 5513997000001,
            NextAttemptAt = referenceUtc,
            MaxAttempts = 5,
            LastError = "Falha inicial de teste"
        });

        var acquired = service.AcquireDueTelegramDeliveryQueueItems(10, referenceUtc.AddMinutes(1), "worker-telegram-1");
        Assert.Single(acquired);
        Assert.Equal(queued.Id, acquired[0].Id);

        var deadLetter = service.FinalizeTelegramDeliveryQueueItem(new AdminKanbanTelegramDeliveryQueueFinalizeRequest
        {
            QueueItemId = queued.Id,
            FinalStatus = TelegramDeliveryQueueStatuses.DeadLetter,
            FinalizedAt = referenceUtc.AddMinutes(2),
            LastError = "Falha definitiva de teste",
            WorkerInstance = "worker-telegram-1"
        });

        Assert.NotNull(deadLetter);
        Assert.Equal(TelegramDeliveryQueueStatuses.DeadLetter, deadLetter!.Status);

        var diagnostics = service.GetTelegramDiagnostics(AdminKanbanBoardTypes.Clients, 10, 10);

        Assert.Equal(1, diagnostics.TotalTelegramLeads);
        Assert.Equal(1, diagnostics.LeadsWithInboundMirror);
        Assert.Equal(1, diagnostics.LeadsWithOutboundMirror);
        Assert.Equal(1, diagnostics.HumanHandoffCount);
        Assert.Equal(0, diagnostics.ActiveQueueCount);
        Assert.Equal(1, diagnostics.DeadLetterCount);
        Assert.Contains(diagnostics.RecentIssues, item => item.QueueItemId == queued.Id && item.Status == TelegramDeliveryQueueStatuses.DeadLetter);
        Assert.Contains(diagnostics.RecentQueueItems, item => item.QueueItemId == queued.Id && item.Status == TelegramDeliveryQueueStatuses.DeadLetter);

        var requeued = service.RequeueTelegramDeliveryQueueItem(queued.Id, referenceUtc.AddMinutes(5), "admin-manual");

        Assert.NotNull(requeued);
        Assert.Equal(TelegramDeliveryQueueStatuses.Retrying, requeued!.Status);
        Assert.Equal("admin-manual", requeued.WorkerInstance);
        Assert.Equal(referenceUtc.AddMinutes(5), requeued.NextAttemptAt);

        var diagnosticsAfterRetry = service.GetTelegramDiagnostics(AdminKanbanBoardTypes.Clients, 10, 10);
        Assert.Equal(1, diagnosticsAfterRetry.ActiveQueueCount);
        Assert.Equal(0, diagnosticsAfterRetry.DeadLetterCount);
        Assert.Contains(diagnosticsAfterRetry.RecentQueueItems, item => item.QueueItemId == queued.Id && item.Status == TelegramDeliveryQueueStatuses.Retrying);
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

    [Fact(DisplayName = "UpsertTelegramLead deve criar lead cliente e reaproveitar o vinculo da conversa")]
    public void UpsertTelegramLead_DeveCriarEAtualizarLeadComIdempotenciaPorConversa()
    {
        using var database = new LocalDbKanbanDatabaseScope();
        if (!database.IsAvailable)
        {
            return;
        }

        var service = CreateService(database.ConnectionString);
        var chatbotConversationId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var serviceRequestId = Guid.NewGuid();

        var created = service.UpsertTelegramLead(new AdminKanbanTelegramLeadUpsertRequest
        {
            BoardType = AdminKanbanBoardTypes.Clients,
            ChatbotConversationId = chatbotConversationId,
            ChannelConversationId = "telegram-chat-001",
            TelegramChatId = 778899,
            ClientId = clientId,
            ClientName = "Ricardo Telegram",
            ClientPhone = "+5513997114422",
            ClientEmail = "ricardo.telegram@teste.com",
            ServiceRequestId = serviceRequestId,
            ServiceCategory = "Eletricista",
            PostalCode = "11701-200",
            City = "Praia Grande",
            StatusNote = "Lead criado pelo bot Telegram.",
            InternalNotes = "Origem automatica do bot Telegram.",
            LastContactAt = new DateTime(2026, 3, 14, 1, 10, 0, DateTimeKind.Utc)
        });

        Assert.True(created.Created);

        var updated = service.UpsertTelegramLead(new AdminKanbanTelegramLeadUpsertRequest
        {
            BoardType = AdminKanbanBoardTypes.Clients,
            ChatbotConversationId = chatbotConversationId,
            ChannelConversationId = "telegram-chat-001",
            TelegramChatId = 778899,
            ClientId = clientId,
            ClientName = "Ricardo Telegram Atualizado",
            ClientPhone = string.Empty,
            ClientEmail = string.Empty,
            ServiceRequestId = null,
            ServiceCategory = string.Empty,
            PostalCode = string.Empty,
            City = string.Empty,
            StatusNote = "Lead atualizado automaticamente pelo bot Telegram.",
            InternalNotes = "Reentrada da conversa Telegram.",
            LastContactAt = new DateTime(2026, 3, 14, 1, 20, 0, DateTimeKind.Utc)
        });

        Assert.False(updated.Created);
        Assert.Equal(created.LeadId, updated.LeadId);

        var details = service.GetLeadDetails(created.LeadId);
        Assert.NotNull(details);
        Assert.Equal("Ricardo Telegram Atualizado", details!.Name);
        Assert.Equal("+5513997114422", details.Phone);
        Assert.Equal("ricardo.telegram@teste.com", details.Email);
        Assert.Equal("Eletricista", details.ServiceCategory);
        Assert.Equal("Praia Grande", details.City);
        Assert.Equal("Telegram", details.Source);
        Assert.Equal("+5513997114422", details.Telegram.ClientPhone);
        Assert.Equal("ricardo.telegram@teste.com", details.Telegram.ClientEmail);
        Assert.Contains(details.History, item => item.EventType == "telegram_lead_criado");
        Assert.Contains(details.History, item => item.EventType == "telegram_lead_atualizado");

        using var connection = new SqlConnection(database.ConnectionString);
        connection.Open();

        using var countCommand = connection.CreateCommand();
        countCommand.CommandText = """
SELECT COUNT(1)
FROM dbo.cpm_web_telegram_funil_links
WHERE ChatbotConversationId = @chatbotConversationId;
""";
        countCommand.Parameters.Add(new SqlParameter("@chatbotConversationId", SqlDbType.UniqueIdentifier) { Value = chatbotConversationId });
        Assert.Equal(1, Convert.ToInt32(countCommand.ExecuteScalar()));

        using var linkCommand = connection.CreateCommand();
        linkCommand.CommandText = """
SELECT LeadId, BoardType, ChannelConversationId, TelegramChatId, ClientId, ClientPhone, ClientEmail, ServiceRequestId
FROM dbo.cpm_web_telegram_funil_links
WHERE ChatbotConversationId = @chatbotConversationId;
""";
        linkCommand.Parameters.Add(new SqlParameter("@chatbotConversationId", SqlDbType.UniqueIdentifier) { Value = chatbotConversationId });

        using var reader = linkCommand.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal(created.LeadId, reader.GetInt32(0));
        Assert.Equal(AdminKanbanBoardTypes.Clients, reader.GetString(1));
        Assert.Equal("telegram-chat-001", reader.GetString(2));
        Assert.Equal(778899L, reader.GetInt64(3));
        Assert.Equal(clientId, reader.GetGuid(4));
        Assert.Equal("+5513997114422", reader.GetString(5));
        Assert.Equal("ricardo.telegram@teste.com", reader.GetString(6));
        Assert.Equal(serviceRequestId, reader.GetGuid(7));
    }

    [Fact(DisplayName = "DeleteLead deve remover lead local, historico, vinculo Telegram e filas relacionadas")]
    public void DeleteLead_DeveRemoverLeadEArtefatosRelacionados()
    {
        using var database = new LocalDbKanbanDatabaseScope();
        if (!database.IsAvailable)
        {
            return;
        }

        var service = CreateService(database.ConnectionString);
        var chatbotConversationId = Guid.NewGuid();

        var upsert = service.UpsertTelegramLead(new AdminKanbanTelegramLeadUpsertRequest
        {
            BoardType = AdminKanbanBoardTypes.Clients,
            ChatbotConversationId = chatbotConversationId,
            ChannelConversationId = "telegram-reset-001",
            TelegramChatId = 5513997114422,
            ClientId = Guid.NewGuid(),
            ClientName = "Lead Reset Telegram",
            ClientPhone = "+5513997114422",
            ClientEmail = "reset.telegram@teste.com",
            ServiceCategory = "Eletricista",
            City = "Santos",
            StatusNote = "Lead criado para exclusao operacional."
        });

        service.AddHistoryNote(upsert.LeadId, "Nota de teste para exclusao.");
        service.EnqueueTelegramDeliveryQueueItem(new AdminKanbanTelegramDeliveryQueueEnqueueRequest
        {
            LeadId = upsert.LeadId,
            Direction = TelegramDeliveryDirections.TelegramToChatwoot,
            DeliveryKey = "telegram-delete-001",
            PayloadJson = """{"message":"reset"}""",
            ChatwootConversationId = 7788,
            TelegramChatId = 5513997114422,
            NextAttemptAt = new DateTime(2026, 3, 15, 18, 10, 0, DateTimeKind.Utc),
            MaxAttempts = 5,
            LastError = "Falha de teste"
        });
        service.EnqueueChatwootSyncQueueItem(new AdminKanbanChatwootSyncQueueEnqueueRequest
        {
            LeadId = upsert.LeadId,
            OperationType = ChatwootSyncOperationTypes.LeadSync,
            NextAttemptAt = new DateTime(2026, 3, 15, 18, 11, 0, DateTimeKind.Utc),
            MaxAttempts = 5,
            LastError = "Falha Chatwoot"
        });

        var deleted = service.DeleteLead(upsert.LeadId);

        Assert.True(deleted);
        Assert.Null(service.GetLeadDetails(upsert.LeadId));

        using var connection = new SqlConnection(database.ConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
SELECT
    (SELECT COUNT(1) FROM dbo.cpm_web_kanban_leads WHERE Id = @leadId) AS LeadCount,
    (SELECT COUNT(1) FROM dbo.cpm_web_kanban_lead_history WHERE LeadId = @leadId) AS HistoryCount,
    (SELECT COUNT(1) FROM dbo.cpm_web_telegram_funil_links WHERE LeadId = @leadId) AS TelegramLinkCount,
    (SELECT COUNT(1) FROM dbo.cpm_web_telegram_delivery_queue WHERE LeadId = @leadId) AS TelegramQueueCount,
    (SELECT COUNT(1) FROM dbo.cpm_web_chatwoot_sync_queue WHERE LeadId = @leadId) AS ChatwootQueueCount;
""";
        command.Parameters.Add(new SqlParameter("@leadId", SqlDbType.Int) { Value = upsert.LeadId });

        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal(0, reader.GetInt32(0));
        Assert.Equal(0, reader.GetInt32(1));
        Assert.Equal(0, reader.GetInt32(2));
        Assert.Equal(0, reader.GetInt32(3));
        Assert.Equal(0, reader.GetInt32(4));
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

    [Fact(DisplayName = "Painel Telegram deve agregar leitura de negocio por periodo e board")]
    public void TelegramBusinessDashboard_DeveAgregarVolumeConversaoEGargalos()
    {
        using var database = new LocalDbKanbanDatabaseScope();
        if (!database.IsAvailable)
        {
            return;
        }

        var service = CreateService(database.ConnectionString);

        var clientLead = service.UpsertTelegramLead(new AdminKanbanTelegramLeadUpsertRequest
        {
            BoardType = AdminKanbanBoardTypes.Clients,
            ChatbotConversationId = Guid.Parse("c96ab1e0-718a-495f-b146-f84c76a80d4e"),
            ChannelConversationId = "telegram-client-business-dashboard",
            TelegramChatId = 5513997000100,
            ClientId = Guid.Parse("23c32e56-ebfa-44dd-ab0a-c0ef4257f4e1"),
            ClientName = "Cliente Dashboard Telegram",
            ClientPhone = "+5513997000100",
            ServiceCategory = "Eletricista",
            City = "Santos",
            StatusNote = "Lead de cliente para validar painel Telegram."
        });

        var providerLead = service.UpsertTelegramLead(new AdminKanbanTelegramLeadUpsertRequest
        {
            BoardType = AdminKanbanBoardTypes.Providers,
            ChatbotConversationId = Guid.Parse("8bb58d65-cd1b-4de9-b648-41ef4d58bdbb"),
            ChannelConversationId = "telegram-provider-business-dashboard",
            TelegramChatId = 5513997000200,
            ClientId = Guid.Parse("6b150119-2d7b-45b9-b227-0a0f67e1f98d"),
            ClientName = "Prestador Dashboard Telegram",
            ClientEmail = "prestador.dashboard@teste.com",
            ServiceCategory = "Encanador",
            City = "Praia Grande",
            StatusNote = "Lead de prestador para validar painel Telegram."
        });

        _ = service.CreateLead(new AdminKanbanLeadUpsertRequest
        {
            BoardType = AdminKanbanBoardTypes.Clients,
            StageId = 0,
            Name = "Lead fora do painel Telegram",
            Phone = "(13) 99999-2222",
            Email = "nao.telegram@teste.com",
            ServiceCategory = "Chaveiro",
            City = "Sao Vicente",
            Source = "Landing",
            StatusNote = "Nao deve entrar na agregacao Telegram."
        });

        Assert.True(service.UpdateLeadChatwootSync(clientLead.LeadId, new AdminKanbanLeadChatwootSyncUpdateRequest
        {
            ChatwootContactId = 501,
            ChatwootConversationId = 601,
            ChatwootInboxId = 1,
            ChatwootSyncStatus = "synced",
            ChatwootLastSyncAt = new DateTime(2026, 3, 15, 13, 30, 0, DateTimeKind.Utc)
        }));

        Assert.True(service.TouchTelegramLeadLink(clientLead.LeadId, new AdminKanbanTelegramLinkTouchRequest
        {
            HumanHandoffStartedAt = new DateTime(2026, 3, 15, 14, 30, 0, DateTimeKind.Utc),
            HumanHandoffStatus = TelegramHandoffPolicy.ActiveStatus,
            HumanHandoffReason = "Primeira resposta humana do Chatwoot",
            HumanHandoffUpdatedAt = new DateTime(2026, 3, 15, 14, 30, 0, DateTimeKind.Utc)
        }));

        using (var connection = new SqlConnection(database.ConnectionString))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
UPDATE dbo.cpm_web_kanban_leads
SET CreatedAt = CASE
        WHEN Id = @clientLeadId THEN @clientCreatedAt
        WHEN Id = @providerLeadId THEN @providerCreatedAt
        ELSE CreatedAt
    END,
    LastContactAt = CASE
        WHEN Id = @clientLeadId THEN @clientLastContactAt
        WHEN Id = @providerLeadId THEN NULL
        ELSE LastContactAt
    END
WHERE Id IN (@clientLeadId, @providerLeadId);
""";
            command.Parameters.AddRange(
            [
                new SqlParameter("@clientLeadId", SqlDbType.Int) { Value = clientLead.LeadId },
                new SqlParameter("@providerLeadId", SqlDbType.Int) { Value = providerLead.LeadId },
                new SqlParameter("@clientCreatedAt", SqlDbType.DateTime2) { Value = new DateTime(2026, 3, 15, 12, 0, 0, DateTimeKind.Utc) },
                new SqlParameter("@providerCreatedAt", SqlDbType.DateTime2) { Value = new DateTime(2026, 3, 15, 15, 0, 0, DateTimeKind.Utc) },
                new SqlParameter("@clientLastContactAt", SqlDbType.DateTime2) { Value = new DateTime(2026, 3, 15, 14, 45, 0, DateTimeKind.Utc) }
            ]);
            _ = command.ExecuteNonQuery();
        }

        var snapshot = service.GetTelegramBusinessDashboard(new AdminKanbanTelegramBusinessDashboardFilter
        {
            CreatedFromUtc = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc),
            CreatedToUtcExclusive = new DateTime(2026, 3, 16, 0, 0, 0, DateTimeKind.Utc),
            BreakdownLimit = 8
        });

        Assert.Equal(2, snapshot.TotalTelegramLeads);
        Assert.Equal(1, snapshot.ClientsLeads);
        Assert.Equal(1, snapshot.ProvidersLeads);
        Assert.Equal(1, snapshot.LeadsWithPhone);
        Assert.Equal(1, snapshot.LeadsWithEmail);
        Assert.Equal(2, snapshot.LeadsWithContactInfo);
        Assert.Equal(2, snapshot.LeadsWithQualifiedCategory);
        Assert.Equal(2, snapshot.LeadsWithQualifiedCity);
        Assert.Equal(1, snapshot.LeadsWithChatwootConversation);
        Assert.Equal(1, snapshot.LeadsWithHumanHandoff);
        Assert.Equal(90, snapshot.MedianMinutesToChatwoot);
        Assert.Equal(150, snapshot.MedianMinutesToHandoff);
        Assert.Equal(2, snapshot.BoardBreakdown.Count);
        Assert.Contains(snapshot.BoardBreakdown, item => item.BoardType == AdminKanbanBoardTypes.Clients && item.TotalLeads == 1 && item.LeadsWithChatwootConversation == 1);
        Assert.Contains(snapshot.BoardBreakdown, item => item.BoardType == AdminKanbanBoardTypes.Providers && item.TotalLeads == 1 && item.LeadsWithChatwootConversation == 0);
        Assert.Contains(snapshot.TopCategories, item => item.ServiceCategory == "Eletricista" && item.TotalLeads == 1);
        Assert.Contains(snapshot.TopCategories, item => item.ServiceCategory == "Encanador" && item.TotalLeads == 1);
        Assert.Contains(snapshot.TopCities, item => item.City == "Santos" && item.TotalLeads == 1);
        Assert.Contains(snapshot.TopCities, item => item.City == "Praia Grande" && item.TotalLeads == 1);
        Assert.Contains(snapshot.StagePressures, item => item.BoardType == AdminKanbanBoardTypes.Clients && item.StageName == "Novo lead");
        Assert.Contains(snapshot.StagePressures, item => item.BoardType == AdminKanbanBoardTypes.Providers && item.StageName == "Novo cadastro");
        Assert.Single(snapshot.HandoffReasons);
        Assert.Equal("Primeira resposta humana do Chatwoot", snapshot.HandoffReasons[0].Reason);
        Assert.Single(snapshot.DailyVolumes);
        Assert.Equal(2, snapshot.DailyVolumes[0].TotalLeads);
    }

    [Fact(DisplayName = "UpdateLeadChatwootSync deve mascarar PII antes de persistir ultimo erro")]
    public void UpdateLeadChatwootSync_DeveMascararPiiAntesDePersistirUltimoErro()
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
            Name = "Lead Mascara Chatwoot",
            Phone = "(13) 99711-4422",
            Email = "ricardo@email.com",
            ServiceCategory = "Eletricista",
            Source = "Teste automatizado",
            Priority = "normal",
            StatusNote = "Mascara de seguranca",
            InternalNotes = string.Empty
        });

        Assert.True(service.UpdateLeadChatwootSync(leadId, new AdminKanbanLeadChatwootSyncUpdateRequest
        {
            ChatwootSyncStatus = ChatwootSyncStatuses.Failed,
            ChatwootLastError = "email=ricardo@email.com phone=+5513997114422 token=segredo"
        }));

        using var connection = new SqlConnection(database.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT ChatwootLastError FROM dbo.cpm_web_kanban_leads WHERE Id = @leadId;";
        command.Parameters.Add(new SqlParameter("@leadId", SqlDbType.Int) { Value = leadId });

        var storedError = Convert.ToString(command.ExecuteScalar()) ?? string.Empty;
        Assert.DoesNotContain("ricardo@email.com", storedError, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("997114422", storedError, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("r***o@email.com", storedError, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[redacted]", storedError, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "PurgeChatwootWebhookPayloads deve expurgar payload e assinatura antigos")]
    public void PurgeChatwootWebhookPayloads_DeveExpurgarPayloadEAssinaturaAntigos()
    {
        using var database = new LocalDbKanbanDatabaseScope();
        if (!database.IsAvailable)
        {
            return;
        }

        var service = CreateService(database.ConnectionString);
        _ = service.GetStages(AdminKanbanBoardTypes.Clients);

        var oldEvent = service.CreateOrGetChatwootWebhookEvent(new AdminKanbanChatwootWebhookEventUpsertRequest
        {
            ProviderEventId = "evt-old",
            EventType = "message_created",
            ConversationId = 9001,
            PayloadJson = """{"event":"message_created","content":"telefone 13997114422"}""",
            Signature = "sha256=assinatura-antiga",
            ReceivedAt = new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc)
        });
        var newEvent = service.CreateOrGetChatwootWebhookEvent(new AdminKanbanChatwootWebhookEventUpsertRequest
        {
            ProviderEventId = "evt-new",
            EventType = "message_created",
            ConversationId = 9002,
            PayloadJson = """{"event":"message_created","content":"telefone 13997114422"}""",
            Signature = "sha256=assinatura-recente",
            ReceivedAt = new DateTime(2026, 3, 13, 12, 0, 0, DateTimeKind.Utc)
        });

        var affectedRows = service.PurgeChatwootWebhookPayloads(
            new DateTime(2026, 3, 10, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 3, 14, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(1, affectedRows);

        using var connection = new SqlConnection(database.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, PayloadJson, Signature, PayloadPurgedAt FROM dbo.cpm_web_chatwoot_webhook_events ORDER BY Id;";

        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal(oldEvent.Id, reader.GetInt32(0));
        Assert.Equal("{\"redacted\":true,\"reason\":\"retention\"}", reader.GetString(1));
        Assert.True(reader.IsDBNull(2));
        Assert.False(reader.IsDBNull(3));

        Assert.True(reader.Read());
        Assert.Equal(newEvent.Id, reader.GetInt32(0));
        Assert.Contains("telefone", reader.GetString(1), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("sha256=assinatura-recente", reader.GetString(2));
        Assert.True(reader.IsDBNull(3));
    }

    [Fact(DisplayName = "PurgeTelegramDeliveryPayloads deve expurgar payloads antigos processados")]
    public void PurgeTelegramDeliveryPayloads_DeveExpurgarPayloadsAntigosProcessados()
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
            Name = "Ricardo Almeida",
            Source = "Telegram"
        });

        var oldQueueItem = service.EnqueueTelegramDeliveryQueueItem(new AdminKanbanTelegramDeliveryQueueEnqueueRequest
        {
            LeadId = leadId,
            Direction = TelegramDeliveryDirections.TelegramToChatwoot,
            DeliveryKey = "telegram:old",
            PayloadJson = """{"message":"telefone 13997114422"}""",
            TelegramChatId = 5513997114422,
            NextAttemptAt = new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc),
            MaxAttempts = 5,
            LastError = "Erro ao entregar para o chat 5513997114422."
        });
        _ = service.FinalizeTelegramDeliveryQueueItem(new AdminKanbanTelegramDeliveryQueueFinalizeRequest
        {
            QueueItemId = oldQueueItem.Id,
            FinalStatus = TelegramDeliveryQueueStatuses.Processed,
            FinalizedAt = new DateTime(2026, 3, 1, 12, 5, 0, DateTimeKind.Utc),
            ClearLastError = false,
            WorkerInstance = "test"
        });

        var newQueueItem = service.EnqueueTelegramDeliveryQueueItem(new AdminKanbanTelegramDeliveryQueueEnqueueRequest
        {
            LeadId = leadId,
            Direction = TelegramDeliveryDirections.ChatwootToTelegram,
            DeliveryKey = "chatwoot:new",
            PayloadJson = """{"message":"telefone 13997114422"}""",
            TelegramChatId = 5513997114422,
            NextAttemptAt = new DateTime(2026, 3, 13, 12, 0, 0, DateTimeKind.Utc),
            MaxAttempts = 5,
            LastError = "Erro recente"
        });
        _ = service.FinalizeTelegramDeliveryQueueItem(new AdminKanbanTelegramDeliveryQueueFinalizeRequest
        {
            QueueItemId = newQueueItem.Id,
            FinalStatus = TelegramDeliveryQueueStatuses.Processed,
            FinalizedAt = new DateTime(2026, 3, 13, 12, 5, 0, DateTimeKind.Utc),
            ClearLastError = false,
            WorkerInstance = "test"
        });

        using (var seedConnection = new SqlConnection(database.ConnectionString))
        {
            seedConnection.Open();
            using var seedCommand = seedConnection.CreateCommand();
            seedCommand.CommandText = """
UPDATE dbo.cpm_web_telegram_delivery_queue
SET CreatedAt = CASE
        WHEN Id = @oldQueueItemId THEN @oldCreatedAt
        WHEN Id = @newQueueItemId THEN @newCreatedAt
        ELSE CreatedAt
    END
WHERE Id IN (@oldQueueItemId, @newQueueItemId);
""";
            seedCommand.Parameters.AddRange(
            [
                new SqlParameter("@oldQueueItemId", SqlDbType.Int) { Value = oldQueueItem.Id },
                new SqlParameter("@newQueueItemId", SqlDbType.Int) { Value = newQueueItem.Id },
                new SqlParameter("@oldCreatedAt", SqlDbType.DateTime2) { Value = new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc) },
                new SqlParameter("@newCreatedAt", SqlDbType.DateTime2) { Value = new DateTime(2026, 3, 13, 12, 0, 0, DateTimeKind.Utc) }
            ]);
            _ = seedCommand.ExecuteNonQuery();
        }

        var affectedRows = service.PurgeTelegramDeliveryPayloads(
            new DateTime(2026, 3, 10, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 3, 14, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(1, affectedRows);

        using var connection = new SqlConnection(database.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, PayloadJson, PayloadPurgedAt FROM dbo.cpm_web_telegram_delivery_queue ORDER BY Id;";

        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal(oldQueueItem.Id, reader.GetInt32(0));
        Assert.Equal("{\"redacted\":true,\"reason\":\"retention\"}", reader.GetString(1));
        Assert.False(reader.IsDBNull(2));

        Assert.True(reader.Read());
        Assert.Equal(newQueueItem.Id, reader.GetInt32(0));
        Assert.Contains("telefone", reader.GetString(1), StringComparison.OrdinalIgnoreCase);
        Assert.True(reader.IsDBNull(2));
    }


    [Fact(DisplayName = "UpsertJourneyIntake deve criar jornada da landing e expor no detalhe do lead")]
    public void UpsertJourneyIntake_DeveCriarJornadaDaLandingEExporNoDetalhe()
    {
        using var database = new LocalDbKanbanDatabaseScope();
        if (!database.IsAvailable)
        {
            return;
        }

        var service = CreateService(database.ConnectionString);
        var requestedAt = new DateTime(2026, 3, 15, 12, 0, 0, DateTimeKind.Utc);

        var result = service.UpsertJourneyIntake(new AdminKanbanJourneyIntakeRequest
        {
            BoardType = AdminKanbanBoardTypes.Clients,
            SourceChannel = AdminKanbanJourneySourceChannels.Landing,
            SourceOrigin = "https://www.consertapramim.com/landing",
            Name = "Lead Jornada Landing",
            Phone = "(13) 99999-0001",
            Email = "landing@teste.com",
            ServiceCategory = "Encanador",
            ProblemDescription = "Troca de torneira na cozinha com vazamento constante.",
            Street = "Rua Pernambuco",
            Neighborhood = "Ocian",
            State = "SP",
            PostalCode = "11701-200",
            City = "Praia Grande",
            Latitude = -24.005001,
            Longitude = -46.401001,
            LandingLeadId = Guid.Parse("44444444-4444-4444-4444-444444444444"),
            VisitorId = "visitor-landing-01",
            SessionId = "session-landing-01",
            RequestedAtUtc = requestedAt,
            LastContactAtUtc = requestedAt,
            Qualification = new AdminKanbanJourneyQualificationRecord
            {
                Status = AdminKanbanJourneyQualificationStatuses.Qualified,
                Source = AdminKanbanJourneyQualificationSources.Deterministic,
                ConfidenceScore = 0.92m,
                HasRequiredData = true,
                NeedsConfirmation = false,
                NormalizedServiceCategoryId = "encanador",
                NormalizedServiceCategoryName = "Encanador",
                ProblemContext = "Troca de torneira na cozinha com vazamento constante.",
                Street = "Rua Pernambuco",
                Neighborhood = "Ocian",
                City = "Praia Grande",
                State = "SP",
                PostalCode = "11701-200",
                Latitude = -24.005001,
                Longitude = -46.401001,
                Summary = "Triagem estruturada concluida com alto nivel de confianca.",
                QualifiedAtUtc = requestedAt,
                RequiredFields = ["Telefone", "Categoria", "CEP", "Cidade", "Logradouro ou bairro", "Contexto do problema"],
                MissingRequiredFields = [],
                OptionalFields = ["E-mail", "UF", "Latitude", "Longitude"]
            }
        });

        Assert.True(result.CreatedLead);
        Assert.True(result.CreatedJourney);
        Assert.True(result.LeadId > 0);
        Assert.True(result.JourneyId > 0);
        Assert.Equal(AdminKanbanJourneyStates.QualificationValidated, result.CurrentState);

        var details = service.GetLeadDetails(result.LeadId);
        Assert.NotNull(details);
        Assert.Equal(result.JourneyId, details!.Journey.JourneyId);
        Assert.Equal(AdminKanbanJourneySourceChannels.Landing, details.Journey.SourceChannel);
        Assert.Equal(AdminKanbanJourneyQualificationStatuses.Qualified, details.Journey.Qualification.Status);
        Assert.Equal("Encanador", details.Journey.Qualification.NormalizedServiceCategoryName);
        Assert.Equal("Praia Grande", details.Journey.Qualification.City);
        Assert.Equal(Guid.Parse("44444444-4444-4444-4444-444444444444"), details.Journey.LandingLeadId);
        Assert.Equal("visitor-landing-01", details.Journey.VisitorId);
        Assert.Contains(details.History, item => item.EventType == "jornada_criada");
    }

    [Fact(DisplayName = "UpsertJourneyIntake deve reaproveitar lead na reentrada omnichannel por telefone")]
    public void UpsertJourneyIntake_DeveReaproveitarLeadNaReentradaOmnichannelPorTelefone()
    {
        using var database = new LocalDbKanbanDatabaseScope();
        if (!database.IsAvailable)
        {
            return;
        }

        var service = CreateService(database.ConnectionString);
        var firstRequestedAt = new DateTime(2026, 3, 15, 12, 0, 0, DateTimeKind.Utc);
        var landingResult = service.UpsertJourneyIntake(new AdminKanbanJourneyIntakeRequest
        {
            BoardType = AdminKanbanBoardTypes.Clients,
            SourceChannel = AdminKanbanJourneySourceChannels.Landing,
            SourceOrigin = "landing-public",
            Name = "Cliente Omnichannel",
            Phone = "13999990002",
            Email = "omnichannel@teste.com",
            ServiceCategory = "Eletricista",
            ProblemDescription = "Preciso revisar o quadro de energia do apartamento.",
            Street = "Rua Bahia",
            Neighborhood = "Boqueirao",
            State = "SP",
            PostalCode = "11700-120",
            City = "Praia Grande",
            RequestedAtUtc = firstRequestedAt,
            LastContactAtUtc = firstRequestedAt,
            Qualification = new AdminKanbanJourneyQualificationRecord
            {
                Status = AdminKanbanJourneyQualificationStatuses.Qualified,
                Source = AdminKanbanJourneyQualificationSources.Deterministic,
                ConfidenceScore = 0.88m,
                HasRequiredData = true,
                NeedsConfirmation = false,
                NormalizedServiceCategoryId = "eletricista",
                NormalizedServiceCategoryName = "Eletricista",
                ProblemContext = "Preciso revisar o quadro de energia do apartamento.",
                Street = "Rua Bahia",
                Neighborhood = "Boqueirao",
                City = "Praia Grande",
                State = "SP",
                PostalCode = "11700-120",
                Summary = "Cliente qualificado via landing.",
                QualifiedAtUtc = firstRequestedAt,
                RequiredFields = ["Telefone", "Categoria", "CEP", "Cidade", "Logradouro ou bairro", "Contexto do problema"],
                MissingRequiredFields = [],
                OptionalFields = ["E-mail", "UF", "Latitude", "Longitude"]
            }
        });

        var serviceRequestId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        var secondRequestedAt = firstRequestedAt.AddHours(2);
        var serviceRequestResult = service.UpsertJourneyIntake(new AdminKanbanJourneyIntakeRequest
        {
            BoardType = AdminKanbanBoardTypes.Clients,
            SourceChannel = AdminKanbanJourneySourceChannels.ServiceRequest,
            SourceOrigin = "api/service-requests",
            Name = "Cliente Omnichannel",
            Phone = "13999990002",
            Email = "omnichannel@teste.com",
            ServiceCategory = "Eletricista",
            ProblemDescription = "Pedido formalizado pelo portal do cliente.",
            Street = "Rua Bahia",
            Neighborhood = "Boqueirao",
            State = "SP",
            PostalCode = "11701-200",
            City = "Praia Grande",
            ServiceRequestId = serviceRequestId,
            RequestedAtUtc = secondRequestedAt,
            LastContactAtUtc = secondRequestedAt,
            Qualification = new AdminKanbanJourneyQualificationRecord
            {
                Status = AdminKanbanJourneyQualificationStatuses.Qualified,
                Source = AdminKanbanJourneyQualificationSources.Deterministic,
                ConfidenceScore = 0.94m,
                HasRequiredData = true,
                NeedsConfirmation = false,
                NormalizedServiceCategoryId = "eletricista",
                NormalizedServiceCategoryName = "Eletricista",
                ProblemContext = "Pedido formalizado pelo portal do cliente.",
                Street = "Rua Bahia",
                Neighborhood = "Boqueirao",
                City = "Praia Grande",
                State = "SP",
                PostalCode = "11701-200",
                Summary = "Pedido formalizado mantendo o mesmo lead omnichannel.",
                QualifiedAtUtc = secondRequestedAt,
                RequiredFields = ["Telefone", "Categoria", "CEP", "Cidade", "Logradouro ou bairro", "Contexto do problema"],
                MissingRequiredFields = [],
                OptionalFields = ["E-mail", "UF", "Latitude", "Longitude"]
            }
        });

        Assert.Equal(landingResult.LeadId, serviceRequestResult.LeadId);
        Assert.False(serviceRequestResult.CreatedLead);
        Assert.Equal(AdminKanbanJourneyStates.ServiceRequestOpened, serviceRequestResult.CurrentState);

        var journey = service.GetJourneyDetails(landingResult.LeadId);
        Assert.NotNull(journey);
        Assert.Equal(serviceRequestId, journey!.ServiceRequestId);

        var details = service.GetLeadDetails(landingResult.LeadId);
        Assert.NotNull(details);
        Assert.Equal("Omnichannel", details!.Source);
        Assert.Contains(details.History, item => item.EventType == "jornada_reentrada_omnichannel" || item.EventType == "jornada_pedido_vinculado");
    }

    [Fact(DisplayName = "UpdateJourneyScheduling deve persistir snapshot de autoagendamento no detalhe da jornada")]
    public void UpdateJourneyScheduling_DevePersistirSnapshotDeAutoagendamento()
    {
        using var database = new LocalDbKanbanDatabaseScope();
        if (!database.IsAvailable)
        {
            return;
        }

        var service = CreateService(database.ConnectionString);
        var requestedAt = new DateTime(2026, 3, 16, 12, 0, 0, DateTimeKind.Utc);

        var upsert = service.UpsertJourneyIntake(new AdminKanbanJourneyIntakeRequest
        {
            BoardType = AdminKanbanBoardTypes.Clients,
            SourceChannel = AdminKanbanJourneySourceChannels.Telegram,
            SourceOrigin = "telegram-bot",
            Name = "Cliente Agenda",
            Phone = "13999990003",
            Email = "agenda@teste.com",
            ServiceCategory = "Eletricista",
            ProblemDescription = "Preciso agendar visita tecnica para revisar o chuveiro.",
            Street = "Rua Bahia",
            Neighborhood = "Ocian",
            State = "SP",
            PostalCode = "11701-200",
            City = "Praia Grande",
            ChatbotConversationId = Guid.Parse("66666666-6666-6666-6666-666666666666"),
            ChannelConversationId = "5513997114422",
            TelegramChatId = 5513997114422,
            RequestedAtUtc = requestedAt,
            LastContactAtUtc = requestedAt,
            Qualification = new AdminKanbanJourneyQualificationRecord
            {
                Status = AdminKanbanJourneyQualificationStatuses.Qualified,
                Source = AdminKanbanJourneyQualificationSources.Deterministic,
                ConfidenceScore = 0.94m,
                HasRequiredData = true,
                NeedsConfirmation = false,
                NormalizedServiceCategoryId = "eletricista",
                NormalizedServiceCategoryName = "Eletricista",
                ProblemContext = "Preciso agendar visita tecnica para revisar o chuveiro.",
                Street = "Rua Bahia",
                Neighborhood = "Ocian",
                City = "Praia Grande",
                State = "SP",
                PostalCode = "11701-200",
                Summary = "Triagem estruturada concluida para autoagendamento.",
                QualifiedAtUtc = requestedAt,
                RequiredFields = ["Telefone", "Categoria", "CEP", "Cidade", "Logradouro ou bairro", "Contexto do problema"],
                MissingRequiredFields = [],
                OptionalFields = ["E-mail", "UF", "Latitude", "Longitude"]
            }
        });

        var schedulingUpdate = service.UpdateJourneyScheduling(upsert.LeadId, new AdminKanbanJourneySchedulingUpdateRequest
        {
            Status = AdminKanbanJourneySchedulingStatuses.Confirmed,
            Summary = "Atendimento confirmado para 16/03/2026 09:00.",
            GoogleCalendarEventId = "cpm-jour-66666666",
            GoogleCalendarEventLink = "https://calendar.google.com/event?eid=teste",
            SuggestedAtUtc = requestedAt.AddMinutes(5),
            ConfirmedAtUtc = requestedAt.AddMinutes(10),
            ScheduledStartAtUtc = requestedAt.AddHours(21),
            ScheduledEndAtUtc = requestedAt.AddHours(22),
            CurrentState = AdminKanbanJourneyStates.AppointmentConfirmed,
            HistoryEventType = "agenda_confirmada",
            HistoryDescription = "Evento confirmado no Google Calendar para o cliente.",
            SourceChannel = AdminKanbanJourneySourceChannels.Telegram,
            SuggestedSlots =
            [
                new AdminKanbanJourneySuggestedSlotRecord
                {
                    OptionNumber = 1,
                    StartsAtUtc = requestedAt.AddHours(21),
                    EndsAtUtc = requestedAt.AddHours(22),
                    Label = "Segunda, 16/03, 09:00 as 10:00"
                }
            ]
        });

        Assert.NotNull(schedulingUpdate);
        Assert.Equal(AdminKanbanJourneyStates.AppointmentConfirmed, schedulingUpdate!.CurrentState);

        var details = service.GetLeadDetails(upsert.LeadId);

        Assert.NotNull(details);
        Assert.Equal(AdminKanbanJourneyStates.AppointmentConfirmed, details!.Journey.CurrentState);
        Assert.Equal(AdminKanbanJourneySchedulingStatuses.Confirmed, details.Journey.Scheduling.Status);
        Assert.Equal("Atendimento confirmado para 16/03/2026 09:00.", details.Journey.Scheduling.Summary);
        Assert.Equal("cpm-jour-66666666", details.Journey.Scheduling.GoogleCalendarEventId);
        Assert.Equal("https://calendar.google.com/event?eid=teste", details.Journey.Scheduling.GoogleCalendarEventLink);
        Assert.Equal(requestedAt.AddMinutes(5), details.Journey.Scheduling.SuggestedAtUtc);
        Assert.Equal(requestedAt.AddMinutes(10), details.Journey.Scheduling.ConfirmedAtUtc);
        Assert.Equal(requestedAt.AddHours(21), details.Journey.Scheduling.ScheduledStartAtUtc);
        Assert.Equal(requestedAt.AddHours(22), details.Journey.Scheduling.ScheduledEndAtUtc);
        Assert.Single(details.Journey.Scheduling.SuggestedSlots);
        Assert.Contains(details.History, item => item.EventType == "agenda_confirmada");

        using var connection = new SqlConnection(database.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
SELECT SchedulingStatus, SchedulingSummary, GoogleCalendarEventId, GoogleCalendarEventLink, SuggestedSlotsJson, SuggestedAtUtc, SchedulingConfirmedAtUtc, ScheduledStartAtUtc, ScheduledEndAtUtc
FROM dbo.cpm_web_journey_executions
WHERE LeadId = @leadId;
""";
        command.Parameters.Add(new SqlParameter("@leadId", SqlDbType.Int) { Value = upsert.LeadId });

        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal(AdminKanbanJourneySchedulingStatuses.Confirmed, reader.GetString(0));
        Assert.Equal("Atendimento confirmado para 16/03/2026 09:00.", reader.GetString(1));
        Assert.Equal("cpm-jour-66666666", reader.GetString(2));
        Assert.Equal("https://calendar.google.com/event?eid=teste", reader.GetString(3));
        Assert.Contains("09:00", reader.GetString(4), StringComparison.Ordinal);
        Assert.Equal(requestedAt.AddMinutes(5), reader.GetDateTime(5));
        Assert.Equal(requestedAt.AddMinutes(10), reader.GetDateTime(6));
        Assert.Equal(requestedAt.AddHours(21), reader.GetDateTime(7));
        Assert.Equal(requestedAt.AddHours(22), reader.GetDateTime(8));
    }

    [Fact(DisplayName = "UpdateJourneyMatching deve persistir snapshot de matching geografico no detalhe da jornada")]
    public void UpdateJourneyMatching_DevePersistirSnapshotDeMatchingGeografico()
    {
        using var database = new LocalDbKanbanDatabaseScope();
        if (!database.IsAvailable)
        {
            return;
        }

        var service = CreateService(database.ConnectionString);
        var requestedAt = new DateTime(2026, 3, 18, 11, 0, 0, DateTimeKind.Utc);
        var scheduledStartAtUtc = new DateTime(2026, 3, 19, 13, 0, 0, DateTimeKind.Utc);
        var scheduledEndAtUtc = new DateTime(2026, 3, 19, 14, 0, 0, DateTimeKind.Utc);
        var lastRunAtUtc = requestedAt.AddMinutes(45);
        var providerId = Guid.Parse("88888888-8888-8888-8888-888888888888");

        var upsert = service.UpsertJourneyIntake(new AdminKanbanJourneyIntakeRequest
        {
            BoardType = AdminKanbanBoardTypes.Clients,
            SourceChannel = AdminKanbanJourneySourceChannels.Telegram,
            SourceOrigin = "telegram-bot",
            Name = "Cliente Matching",
            Phone = "13999990005",
            Email = "matching@teste.com",
            ServiceCategory = "Eletricista",
            ProblemDescription = "Preciso de atendimento para revisar o chuveiro.",
            Street = "Rua Bahia",
            Neighborhood = "Ocian",
            State = "SP",
            PostalCode = "11701-200",
            City = "Praia Grande",
            ChatbotConversationId = Guid.Parse("88888888-1111-1111-1111-888888888888"),
            ChannelConversationId = "5513997114499",
            TelegramChatId = 5513997114499,
            RequestedAtUtc = requestedAt,
            LastContactAtUtc = requestedAt,
            Qualification = new AdminKanbanJourneyQualificationRecord
            {
                Status = AdminKanbanJourneyQualificationStatuses.Qualified,
                Source = AdminKanbanJourneyQualificationSources.Deterministic,
                ConfidenceScore = 0.93m,
                HasRequiredData = true,
                NeedsConfirmation = false,
                NormalizedServiceCategoryId = "eletricista",
                NormalizedServiceCategoryName = "Eletricista",
                ProblemContext = "Preciso de atendimento para revisar o chuveiro.",
                Street = "Rua Bahia",
                Neighborhood = "Ocian",
                City = "Praia Grande",
                State = "SP",
                PostalCode = "11701-200",
                Latitude = -24.025331,
                Longitude = -46.469028,
                Summary = "Cliente qualificado para matching geografico.",
                QualifiedAtUtc = requestedAt,
                RequiredFields = ["Telefone", "Categoria", "CEP", "Cidade", "Logradouro ou bairro", "Contexto do problema"],
                MissingRequiredFields = [],
                OptionalFields = ["E-mail", "UF", "Latitude", "Longitude"]
            }
        });

        _ = service.UpdateJourneyScheduling(upsert.LeadId, new AdminKanbanJourneySchedulingUpdateRequest
        {
            Status = AdminKanbanJourneySchedulingStatuses.Confirmed,
            Summary = "Janela confirmada para seguir ao matching.",
            GoogleCalendarEventId = "cpm-match-88888888",
            SuggestedAtUtc = requestedAt.AddMinutes(10),
            ConfirmedAtUtc = requestedAt.AddMinutes(20),
            ScheduledStartAtUtc = scheduledStartAtUtc,
            ScheduledEndAtUtc = scheduledEndAtUtc,
            CurrentState = AdminKanbanJourneyStates.AppointmentConfirmed,
            HistoryEventType = "agenda_confirmada",
            HistoryDescription = "Agenda confirmada para testar matching.",
            SourceChannel = AdminKanbanJourneySourceChannels.Telegram,
            SuggestedSlots =
            [
                new AdminKanbanJourneySuggestedSlotRecord
                {
                    OptionNumber = 1,
                    StartsAtUtc = scheduledStartAtUtc,
                    EndsAtUtc = scheduledEndAtUtc,
                    Label = "Quinta, 19/03, 10:00 as 11:00"
                }
            ]
        });

        var update = service.UpdateJourneyMatching(upsert.LeadId, new AdminKanbanJourneyMatchingUpdateRequest
        {
            Status = AdminKanbanJourneyMatchingStatuses.EligibleProvidersFound,
            Summary = "Matching geografico encontrou 1 prestador elegivel para a jornada.",
            RequestedCategory = "Eletricista",
            RequestedSubcategory = "chuveiro",
            EvaluatedProvidersCount = 2,
            EligibleProvidersCount = 1,
            LastRunAtUtc = lastRunAtUtc,
            CurrentState = AdminKanbanJourneyStates.MatchingInProgress,
            HistoryEventType = "jornada_matching_snapshot",
            HistoryDescription = "Snapshot de matching persistido para auditoria.",
            SourceChannel = AdminKanbanJourneySourceChannels.Telegram,
            Candidates =
            [
                new AdminKanbanJourneyProviderMatchRecord
                {
                    ProviderId = providerId,
                    ProviderName = "Prestador Elegivel",
                    ProviderEmail = "prestador@teste.com",
                    ProviderPhone = "13999990099",
                    IsEligible = true,
                    RankPosition = 1,
                    Score = 84.5m,
                    DistanceKm = 5.25d,
                    CoverageRadiusKm = 12d,
                    Rating = 4.8d,
                    ReviewCount = 32,
                    OperationalStatus = "Online",
                    ClientPreference = "PF e PJ",
                    RequestedCategory = "Eletricista",
                    RequestedSubcategory = "chuveiro",
                    CategoryMatched = true,
                    SubcategoryMatched = true,
                    RadiusMatched = true,
                    AvailabilityMatched = true,
                    CapacityMatched = true,
                    Summary = "Prestador apto para disparo."
                }
            ]
        });

        Assert.NotNull(update);
        Assert.Equal(AdminKanbanJourneyStates.MatchingInProgress, update!.CurrentState);
        Assert.Equal(AdminKanbanJourneyMatchingStatuses.EligibleProvidersFound, update.Matching.Status);
        Assert.Single(update.Matching.Candidates);

        var details = service.GetLeadDetails(upsert.LeadId);

        Assert.NotNull(details);
        Assert.Equal(AdminKanbanJourneyMatchingStatuses.EligibleProvidersFound, details!.Journey.Matching.Status);
        Assert.Equal("Matching geografico encontrou 1 prestador elegivel para a jornada.", details.Journey.Matching.Summary);
        Assert.Equal("Eletricista", details.Journey.Matching.RequestedCategory);
        Assert.Equal("chuveiro", details.Journey.Matching.RequestedSubcategory);
        Assert.Equal(2, details.Journey.Matching.EvaluatedProvidersCount);
        Assert.Equal(1, details.Journey.Matching.EligibleProvidersCount);
        Assert.Equal(lastRunAtUtc, details.Journey.Matching.LastRunAtUtc);
        Assert.Single(details.Journey.Matching.Candidates);
        Assert.Equal(providerId, details.Journey.Matching.Candidates[0].ProviderId);
        Assert.Contains(details.History, item => item.EventType == "jornada_matching_snapshot");

        using var connection = new SqlConnection(database.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
SELECT MatchingStatus, MatchingSummary, MatchingRequestedCategory, MatchingRequestedSubcategory, MatchingEvaluatedProviders, MatchingEligibleProviders, MatchingCandidatesJson, MatchingLastRunAtUtc
FROM dbo.cpm_web_journey_executions
WHERE LeadId = @leadId;
""";
        command.Parameters.Add(new SqlParameter("@leadId", SqlDbType.Int) { Value = upsert.LeadId });

        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal(AdminKanbanJourneyMatchingStatuses.EligibleProvidersFound, reader.GetString(0));
        Assert.Equal("Matching geografico encontrou 1 prestador elegivel para a jornada.", reader.GetString(1));
        Assert.Equal("Eletricista", reader.GetString(2));
        Assert.Equal("chuveiro", reader.GetString(3));
        Assert.Equal(2, reader.GetInt32(4));
        Assert.Equal(1, reader.GetInt32(5));
        Assert.Contains("Prestador Elegivel", reader.GetString(6), StringComparison.Ordinal);
        Assert.Equal(lastRunAtUtc, reader.GetDateTime(7));
    }

    [Fact(DisplayName = "ApplyJourneyStageAutomation deve persistir motivo, origem e timer no snapshot da jornada")]
    public void ApplyJourneyStageAutomation_DevePersistirMotivoOrigemETimer()
    {
        using var database = new LocalDbKanbanDatabaseScope();
        if (!database.IsAvailable)
        {
            return;
        }

        var service = CreateService(database.ConnectionString);
        var requestedAt = new DateTime(2026, 3, 18, 12, 0, 0, DateTimeKind.Utc);
        var suggestedAtUtc = requestedAt.AddMinutes(10);
        var timerDueAtUtc = suggestedAtUtc.AddHours(3);

        var upsert = service.UpsertJourneyIntake(new AdminKanbanJourneyIntakeRequest
        {
            BoardType = AdminKanbanBoardTypes.Clients,
            SourceChannel = AdminKanbanJourneySourceChannels.Telegram,
            SourceOrigin = "telegram-bot",
            Name = "Cliente Stage Automation",
            Phone = "13999990004",
            Email = "stage.automation@teste.com",
            ServiceCategory = "Encanador",
            ProblemDescription = "Preciso marcar visita para vazamento na cozinha.",
            Street = "Rua Parana",
            Neighborhood = "Ocian",
            State = "SP",
            PostalCode = "11701-330",
            City = "Praia Grande",
            ChatbotConversationId = Guid.Parse("77777777-7777-7777-7777-777777777777"),
            ChannelConversationId = "5513997114455",
            TelegramChatId = 5513997114455,
            RequestedAtUtc = requestedAt,
            LastContactAtUtc = requestedAt,
            Qualification = new AdminKanbanJourneyQualificationRecord
            {
                Status = AdminKanbanJourneyQualificationStatuses.Qualified,
                Source = AdminKanbanJourneyQualificationSources.Deterministic,
                ConfidenceScore = 0.91m,
                HasRequiredData = true,
                NeedsConfirmation = false,
                NormalizedServiceCategoryId = "encanador",
                NormalizedServiceCategoryName = "Encanador",
                ProblemContext = "Preciso marcar visita para vazamento na cozinha.",
                Street = "Rua Parana",
                Neighborhood = "Ocian",
                City = "Praia Grande",
                State = "SP",
                PostalCode = "11701-330",
                Summary = "Cliente qualificado para agendamento automatico.",
                QualifiedAtUtc = requestedAt,
                RequiredFields = ["Telefone", "Categoria", "CEP", "Cidade", "Logradouro ou bairro", "Contexto do problema"],
                MissingRequiredFields = [],
                OptionalFields = ["E-mail", "UF", "Latitude", "Longitude"]
            }
        });

        _ = service.UpdateJourneyScheduling(upsert.LeadId, new AdminKanbanJourneySchedulingUpdateRequest
        {
            Status = AdminKanbanJourneySchedulingStatuses.SlotSuggested,
            Summary = "Foram sugeridas tres janelas para o cliente.",
            SuggestedAtUtc = suggestedAtUtc,
            CurrentState = AdminKanbanJourneyStates.SlotSuggested,
            HistoryEventType = "agenda_janela_sugerida",
            HistoryDescription = "Autoagendamento sugeriu janelas ao cliente.",
            SourceChannel = AdminKanbanJourneySourceChannels.Telegram,
            SuggestedSlots =
            [
                new AdminKanbanJourneySuggestedSlotRecord
                {
                    OptionNumber = 1,
                    StartsAtUtc = requestedAt.AddHours(20),
                    EndsAtUtc = requestedAt.AddHours(21),
                    Label = "Quarta, 18/03, 08:00 as 09:00"
                }
            ]
        });

        var candidates = service.ListJourneyStageAutomationCandidates(AdminKanbanBoardTypes.Clients, suggestedAtUtc, 10);
        var candidate = Assert.Single(candidates.Where(item => item.LeadId == upsert.LeadId));
        Assert.Equal(AdminKanbanJourneyStates.SlotSuggested, candidate.CurrentState);

        var transition = service.ApplyJourneyStageAutomation(new AdminKanbanJourneyStageAutomationUpdateRequest
        {
            LeadId = upsert.LeadId,
            BoardType = AdminKanbanBoardTypes.Clients,
            TargetStageName = AdminKanbanJourneyClientStageNames.WaitingScheduleConfirmation,
            TargetCurrentState = AdminKanbanJourneyStates.WaitingScheduleConfirmation,
            Reason = "Cliente recebeu as janelas e a jornada agora aguarda confirmacao da agenda.",
            Origin = AdminKanbanJourneyAutomationOrigins.StateMachine,
            HistoryEventType = "jornada_kanban_automatizada",
            HistoryDescription = "Kanban movido automaticamente para Aguardando confirmacao da agenda. Motivo: Cliente recebeu as janelas e a jornada agora aguarda confirmacao da agenda.",
            MetadataJson = "{\"source\":\"test\"}",
            ActiveTimerCode = AdminKanbanJourneyTimerCodes.PendingScheduleConfirmation,
            ActiveTimerDueAtUtc = timerDueAtUtc
        });

        Assert.NotNull(transition);
        Assert.True(transition!.StageChanged);
        Assert.Equal(AdminKanbanJourneyClientStageNames.WaitingScheduleConfirmation, transition.ToStageName);
        Assert.Equal(AdminKanbanJourneyStates.WaitingScheduleConfirmation, transition.CurrentState);

        var details = service.GetLeadDetails(upsert.LeadId);
        Assert.NotNull(details);
        Assert.Equal(AdminKanbanJourneyClientStageNames.WaitingScheduleConfirmation, details!.StageName);
        Assert.Equal(AdminKanbanJourneyStates.WaitingScheduleConfirmation, details.Journey.CurrentState);
        Assert.Equal("Cliente recebeu as janelas e a jornada agora aguarda confirmacao da agenda.", details.Journey.StageAutomation.LastReason);
        Assert.Equal(AdminKanbanJourneyAutomationOrigins.StateMachine, details.Journey.StageAutomation.LastOrigin);
        Assert.Equal(timerDueAtUtc, details.Journey.StageAutomation.ActiveTimerDueAtUtc);
        Assert.Equal(AdminKanbanJourneyTimerCodes.PendingScheduleConfirmation, details.Journey.StageAutomation.ActiveTimerCode);
        Assert.Contains(details.History, item => item.EventType == "jornada_kanban_automatizada");

        using var connection = new SqlConnection(database.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
SELECT CurrentState, LastStageAutomationReason, LastStageAutomationOrigin, ActiveTimerCode, ActiveTimerDueAtUtc
FROM dbo.cpm_web_journey_executions
WHERE LeadId = @leadId;
""";
        command.Parameters.Add(new SqlParameter("@leadId", SqlDbType.Int) { Value = upsert.LeadId });

        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal(AdminKanbanJourneyStates.WaitingScheduleConfirmation, reader.GetString(0));
        Assert.Equal("Cliente recebeu as janelas e a jornada agora aguarda confirmacao da agenda.", reader.GetString(1));
        Assert.Equal(AdminKanbanJourneyAutomationOrigins.StateMachine, reader.GetString(2));
        Assert.Equal(AdminKanbanJourneyTimerCodes.PendingScheduleConfirmation, reader.GetString(3));
        Assert.Equal(timerDueAtUtc, reader.GetDateTime(4));
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
