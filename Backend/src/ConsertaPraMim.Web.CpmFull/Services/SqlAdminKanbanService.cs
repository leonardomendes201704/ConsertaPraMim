using System.Data;
using AppMobileCPM.Integrations.Chatwoot;
using AppMobileCPM.Integrations.Telegram;
using Microsoft.Data.SqlClient;

namespace AppMobileCPM.Services;

public sealed partial class SqlAdminKanbanService : IAdminKanbanService
{
    private const string TablePrefix = "cpm_web_";
    private const string RedactedWebhookPayloadJson = "{\"redacted\":true,\"reason\":\"retention\"}";
    private const string RedactedTelegramPayloadJson = "{\"redacted\":true,\"reason\":\"retention\"}";

    private static readonly IReadOnlyList<(string Name, string Color)> ClientJourneyStages =
    [
        ("Novo lead", "#0d6efd"),
        ("Triagem automatica", "#fd7e14"),
        ("Dados pendentes", "#ffc107"),
        ("Endereco e categoria validados", "#20c997"),
        ("Janela sugerida", "#6f42c1"),
        ("Aguardando confirmacao da agenda", "#6610f2"),
        ("Agendamento confirmado", "#0dcaf0"),
        ("Em matching", "#198754"),
        ("Disparo para prestadores", "#198754"),
        ("Aguardando aceite", "#fd7e14"),
        ("Prestador conectado", "#20c997"),
        ("Servico em andamento", "#0dcaf0"),
        ("Aguardando confirmacao de conclusao", "#ffc107"),
        ("Aguardando avaliacao do cliente", "#6c757d"),
        ("Aguardando avaliacao do prestador", "#6c757d"),
        ("Concluido", "#198754"),
        ("Sem match", "#dc3545"),
        ("Cancelado", "#6c757d"),
        ("Excecao operacional", "#dc3545")
    ];

    private static readonly IReadOnlyList<(string Name, string Color)> ProviderDefaultStages =
    [
        ("Novo cadastro", "#0d6efd"),
        ("Primeiro contato", "#fd7e14"),
        ("Documentacao pendente", "#ffc107"),
        ("Validacao tecnica", "#6f42c1"),
        ("Ativo na plataforma", "#198754"),
        ("Inativo/Recusado", "#dc3545")
    ];

    private readonly string _connectionString;
    private readonly object _initLock = new();
    private bool _initialized;

    public SqlAdminKanbanService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' nao encontrada.");
    }

    public AdminKanbanBoardData GetBoard(string boardType)
    {
        var normalizedBoardType = AdminKanbanBoardTypes.Normalize(boardType);
        EnsureInitialized();

        var stages = GetStages(normalizedBoardType)
            .Select(stage => new AdminKanbanStageRecord
            {
                Id = stage.Id,
                BoardType = stage.BoardType,
                Name = stage.Name,
                Color = stage.Color,
                SortOrder = stage.SortOrder,
                Leads = []
            })
            .ToList();

        var leadsByStage = new Dictionary<int, List<AdminKanbanLeadCardRecord>>();
        foreach (var stage in stages)
        {
            leadsByStage[stage.Id] = [];
        }

        using (var connection = OpenConnection())
        using (var command = connection.CreateCommand())
        {
            command.CommandText = $"""
SELECT
    l.Id,
    l.StageId,
    l.BoardType,
    l.Name,
    l.Phone,
    l.Email,
    l.ServiceCategory,
    l.Source,
    l.Priority,
    l.StatusNote,
    l.ChatwootSyncStatus,
    l.CreatedAt,
    l.UpdatedAt,
    l.LastContactAt,
    COALESCE(sh.StageEnteredAt, l.UpdatedAt, l.CreatedAt) AS StageEnteredAt
FROM dbo.{TablePrefix}kanban_leads l
OUTER APPLY (
    SELECT TOP (1) h.CreatedAt AS StageEnteredAt
    FROM dbo.{TablePrefix}kanban_lead_history h
    WHERE h.LeadId = l.Id
      AND h.ToStageId = l.StageId
    ORDER BY h.CreatedAt DESC, h.Id DESC
) sh
WHERE l.IsActive = 1 AND l.BoardType = @boardType
ORDER BY l.StageId, l.SortOrder, l.UpdatedAt DESC, l.Id;
""";
            command.Parameters.Add(new SqlParameter("@boardType", SqlDbType.NVarChar, 30) { Value = normalizedBoardType });

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var lead = new AdminKanbanLeadCardRecord
                {
                    Id = reader.GetInt32(0),
                    StageId = reader.GetInt32(1),
                    BoardType = reader.GetString(2),
                    Name = reader.GetString(3),
                    Phone = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                    Email = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                    ServiceCategory = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                    Source = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                    Priority = reader.IsDBNull(8) ? "normal" : reader.GetString(8),
                    StatusNote = reader.IsDBNull(9) ? string.Empty : reader.GetString(9),
                    ChatwootSyncStatus = reader.IsDBNull(10) ? string.Empty : reader.GetString(10),
                    CreatedAt = reader.GetDateTime(11),
                    UpdatedAt = reader.IsDBNull(12) ? null : reader.GetDateTime(12),
                    LastContactAt = reader.IsDBNull(13) ? null : reader.GetDateTime(13),
                    StageEnteredAt = reader.GetDateTime(14)
                };

                if (leadsByStage.TryGetValue(lead.StageId, out var leads))
                {
                    leads.Add(lead);
                }
            }
        }

        var hydratedStages = stages
            .Select(stage => new AdminKanbanStageRecord
            {
                Id = stage.Id,
                BoardType = stage.BoardType,
                Name = stage.Name,
                Color = stage.Color,
                SortOrder = stage.SortOrder,
                Leads = leadsByStage[stage.Id]
            })
            .ToList();

        return new AdminKanbanBoardData
        {
            BoardType = normalizedBoardType,
            Stages = hydratedStages
        };
    }

    public IReadOnlyList<AdminKanbanStageRecord> GetStages(string boardType)
    {
        var normalizedBoardType = AdminKanbanBoardTypes.Normalize(boardType);
        EnsureInitialized();

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
SELECT Id, BoardType, Name, Color, SortOrder
FROM dbo.{TablePrefix}kanban_stages
WHERE IsActive = 1 AND BoardType = @boardType
ORDER BY SortOrder, Id;
""";
        command.Parameters.Add(new SqlParameter("@boardType", SqlDbType.NVarChar, 30) { Value = normalizedBoardType });

        var items = new List<AdminKanbanStageRecord>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            items.Add(new AdminKanbanStageRecord
            {
                Id = reader.GetInt32(0),
                BoardType = reader.GetString(1),
                Name = reader.GetString(2),
                Color = reader.IsDBNull(3) ? "#0d6efd" : reader.GetString(3),
                SortOrder = reader.GetInt32(4),
                Leads = []
            });
        }

        return items;
    }

    public AdminKanbanLeadDetailsRecord? GetLeadDetails(int leadId)
    {
        EnsureInitialized();
        using var connection = OpenConnection();

        AdminKanbanLeadDetailsRecord? details = null;

        using (var command = connection.CreateCommand())
        {
            command.CommandText = $"""
SELECT TOP (1)
    l.Id, l.StageId, s.Name, l.BoardType, l.Name, l.Phone, l.Email, l.ServiceCategory, l.PostalCode, l.City,
    l.Source, l.Priority, l.StatusNote, l.InternalNotes, l.CreatedAt, l.UpdatedAt, l.LastContactAt,
    l.ChatwootContactId, l.ChatwootConversationId, l.ChatwootInboxId, l.ChatwootSyncStatus, l.ChatwootLastSyncAt, l.ChatwootLastError
FROM dbo.{TablePrefix}kanban_leads l
INNER JOIN dbo.{TablePrefix}kanban_stages s ON s.Id = l.StageId
WHERE l.Id = @leadId AND l.IsActive = 1;
""";
            command.Parameters.Add(new SqlParameter("@leadId", SqlDbType.Int) { Value = leadId });

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                details = new AdminKanbanLeadDetailsRecord
                {
                    Id = reader.GetInt32(0),
                    StageId = reader.GetInt32(1),
                    StageName = reader.GetString(2),
                    BoardType = reader.GetString(3),
                    Name = reader.GetString(4),
                    Phone = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                    Email = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                    ServiceCategory = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                    PostalCode = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
                    City = reader.IsDBNull(9) ? string.Empty : reader.GetString(9),
                    Source = reader.IsDBNull(10) ? string.Empty : reader.GetString(10),
                    Priority = reader.IsDBNull(11) ? "normal" : reader.GetString(11),
                    StatusNote = reader.IsDBNull(12) ? string.Empty : reader.GetString(12),
                    InternalNotes = reader.IsDBNull(13) ? string.Empty : reader.GetString(13),
                    CreatedAt = reader.GetDateTime(14),
                    UpdatedAt = reader.IsDBNull(15) ? null : reader.GetDateTime(15),
                    LastContactAt = reader.IsDBNull(16) ? null : reader.GetDateTime(16),
                    Telegram = new AdminKanbanLeadTelegramLinkRecord(),
                    Chatwoot = new AdminKanbanLeadChatwootSyncRecord
                    {
                        ContactId = ReadNullableInt64(reader, 17),
                        ConversationId = ReadNullableInt64(reader, 18),
                        InboxId = ReadNullableInt64(reader, 19),
                        SyncStatus = reader.IsDBNull(20) ? string.Empty : reader.GetString(20),
                        LastSyncAt = reader.IsDBNull(21) ? null : reader.GetDateTime(21),
                        LastError = reader.IsDBNull(22) ? string.Empty : reader.GetString(22)
                    },
                    History = []
                };
            }
        }

        if (details is null)
        {
            return null;
        }

        AdminKanbanLeadTelegramLinkRecord? telegramLink = null;
        using (var telegramCommand = connection.CreateCommand())
        {
            telegramCommand.CommandText = $"""
SELECT TOP (1)
    ChatbotConversationId,
    ChannelConversationId,
    TelegramChatId,
    ClientId,
    ClientPhone,
    ClientEmail,
    ServiceRequestId,
    HumanHandoffStartedAt,
    HumanHandoffStatus,
    HumanHandoffReason,
    HumanHandoffUpdatedAt,
    LastTelegramMessageSyncedAt,
    LastChatwootMessageSyncedAt,
    UpdatedAt
FROM dbo.{TablePrefix}telegram_funil_links
WHERE LeadId = @leadId
ORDER BY UpdatedAt DESC, Id DESC;
""";
            telegramCommand.Parameters.Add(new SqlParameter("@leadId", SqlDbType.Int) { Value = leadId });

            using var reader = telegramCommand.ExecuteReader();
            if (reader.Read())
            {
                telegramLink = new AdminKanbanLeadTelegramLinkRecord
                {
                    ChatbotConversationId = reader.IsDBNull(0) ? null : reader.GetGuid(0),
                    ChannelConversationId = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    TelegramChatId = ReadNullableInt64(reader, 2),
                    ClientId = reader.IsDBNull(3) ? null : reader.GetGuid(3),
                    ClientPhone = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                    ClientEmail = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                    ServiceRequestId = reader.IsDBNull(6) ? null : reader.GetGuid(6),
                    HumanHandoffStartedAt = reader.IsDBNull(7) ? null : reader.GetDateTime(7),
                    HumanHandoffStatus = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
                    HumanHandoffReason = reader.IsDBNull(9) ? string.Empty : reader.GetString(9),
                    HumanHandoffUpdatedAt = reader.IsDBNull(10) ? null : reader.GetDateTime(10),
                    LastTelegramMessageSyncedAt = reader.IsDBNull(11) ? null : reader.GetDateTime(11),
                    LastChatwootMessageSyncedAt = reader.IsDBNull(12) ? null : reader.GetDateTime(12),
                    UpdatedAt = reader.IsDBNull(13) ? null : reader.GetDateTime(13)
                };
            }
        }

        var history = new List<AdminKanbanLeadHistoryRecord>();
        using (var historyCommand = connection.CreateCommand())
        {
            historyCommand.CommandText = $"""
SELECT h.Id, h.EventType, h.FromStageId, fs.Name, h.ToStageId, ts.Name, h.Description, h.CreatedAt
FROM dbo.{TablePrefix}kanban_lead_history h
LEFT JOIN dbo.{TablePrefix}kanban_stages fs ON fs.Id = h.FromStageId
LEFT JOIN dbo.{TablePrefix}kanban_stages ts ON ts.Id = h.ToStageId
WHERE h.LeadId = @leadId
ORDER BY h.CreatedAt DESC, h.Id DESC;
""";
            historyCommand.Parameters.Add(new SqlParameter("@leadId", SqlDbType.Int) { Value = leadId });

            using var reader = historyCommand.ExecuteReader();
            while (reader.Read())
            {
                history.Add(new AdminKanbanLeadHistoryRecord
                {
                    Id = reader.GetInt32(0),
                    EventType = reader.GetString(1),
                    FromStageId = reader.IsDBNull(2) ? null : reader.GetInt32(2),
                    FromStageName = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                    ToStageId = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                    ToStageName = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                    Description = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                    CreatedAt = reader.GetDateTime(7)
                });
            }
        }

        return new AdminKanbanLeadDetailsRecord
        {
            Id = details.Id,
            StageId = details.StageId,
            StageName = details.StageName,
            BoardType = details.BoardType,
            Name = details.Name,
            Phone = details.Phone,
            Email = details.Email,
            ServiceCategory = details.ServiceCategory,
            PostalCode = details.PostalCode,
            City = details.City,
            Source = details.Source,
            Priority = details.Priority,
            StatusNote = details.StatusNote,
            InternalNotes = details.InternalNotes,
            CreatedAt = details.CreatedAt,
            UpdatedAt = details.UpdatedAt,
            LastContactAt = details.LastContactAt,
            Journey = GetJourneyDetails(leadId) ?? details.Journey,
            Telegram = telegramLink ?? details.Telegram,
            Chatwoot = details.Chatwoot,
            History = history
        };
    }

    public int CreateLead(AdminKanbanLeadUpsertRequest request)
    {
        EnsureInitialized();
        var normalizedBoardType = AdminKanbanBoardTypes.Normalize(request.BoardType);

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        var stageId = ResolveStageId(connection, transaction, normalizedBoardType, request.StageId);
        var nextSortOrder = GetNextLeadSortOrder(connection, transaction, normalizedBoardType, stageId);

        using var insertCommand = connection.CreateCommand();
        insertCommand.Transaction = transaction;
        insertCommand.CommandText = $"""
INSERT INTO dbo.{TablePrefix}kanban_leads
(BoardType, StageId, SortOrder, Name, Phone, Email, ServiceCategory, PostalCode, City, Source, Priority, StatusNote, InternalNotes, LastContactAt, IsActive, CreatedAt, UpdatedAt)
VALUES
(@boardType, @stageId, @sortOrder, @name, @phone, @email, @serviceCategory, @postalCode, @city, @source, @priority, @statusNote, @internalNotes, @lastContactAt, 1, SYSUTCDATETIME(), NULL);
SELECT CAST(SCOPE_IDENTITY() AS INT);
""";
        insertCommand.Parameters.AddRange(
        [
            new SqlParameter("@boardType", SqlDbType.NVarChar, 30) { Value = normalizedBoardType },
            new SqlParameter("@stageId", SqlDbType.Int) { Value = stageId },
            new SqlParameter("@sortOrder", SqlDbType.Int) { Value = nextSortOrder },
            new SqlParameter("@name", SqlDbType.NVarChar, 140) { Value = TrimTo(request.Name, 140) },
            new SqlParameter("@phone", SqlDbType.NVarChar, 30) { Value = ToDbValue(request.Phone) },
            new SqlParameter("@email", SqlDbType.NVarChar, 180) { Value = ToDbValue(request.Email) },
            new SqlParameter("@serviceCategory", SqlDbType.NVarChar, 140) { Value = ToDbValue(request.ServiceCategory) },
            new SqlParameter("@postalCode", SqlDbType.NVarChar, 9) { Value = ToDbValue(request.PostalCode) },
            new SqlParameter("@city", SqlDbType.NVarChar, 120) { Value = ToDbValue(request.City) },
            new SqlParameter("@source", SqlDbType.NVarChar, 120) { Value = ToDbValue(request.Source) },
            new SqlParameter("@priority", SqlDbType.NVarChar, 20) { Value = NormalizePriority(request.Priority) },
            new SqlParameter("@statusNote", SqlDbType.NVarChar, 500) { Value = ToDbValue(request.StatusNote) },
            new SqlParameter("@internalNotes", SqlDbType.NVarChar, -1) { Value = ToDbValue(request.InternalNotes) },
            new SqlParameter("@lastContactAt", SqlDbType.DateTime2) { Value = request.LastContactAt.HasValue ? request.LastContactAt.Value : DBNull.Value }
        ]);

        var leadId = Convert.ToInt32(insertCommand.ExecuteScalar());
        InsertHistory(
            connection,
            transaction,
            leadId,
            eventType: "criado",
            fromStageId: null,
            toStageId: stageId,
            description: "Lead cadastrado manualmente no funil."
        );

        transaction.Commit();
        return leadId;
    }

    public bool DeleteLead(int leadId)
    {
        EnsureInitialized();

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
DELETE FROM dbo.{TablePrefix}telegram_delivery_queue
WHERE LeadId = @leadId;

DELETE FROM dbo.{TablePrefix}chatwoot_sync_queue
WHERE LeadId = @leadId;

DELETE FROM dbo.{TablePrefix}journey_events
WHERE LeadId = @leadId;

DELETE FROM dbo.{TablePrefix}journey_executions
WHERE LeadId = @leadId;

DELETE FROM dbo.{TablePrefix}telegram_funil_links
WHERE LeadId = @leadId;

DELETE FROM dbo.{TablePrefix}kanban_lead_history
WHERE LeadId = @leadId;

DELETE FROM dbo.{TablePrefix}kanban_leads
WHERE Id = @leadId
  AND IsActive = 1;

SELECT @@ROWCOUNT;
""";
        command.Parameters.Add(new SqlParameter("@leadId", SqlDbType.Int) { Value = leadId });

        var deleted = Convert.ToInt32(command.ExecuteScalar()) > 0;
        if (!deleted)
        {
            transaction.Rollback();
            return false;
        }

        transaction.Commit();
        return true;
    }

    public AdminKanbanTelegramLeadUpsertResult UpsertTelegramLead(AdminKanbanTelegramLeadUpsertRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        EnsureInitialized();
        var normalizedBoardType = AdminKanbanBoardTypes.Normalize(request.BoardType);

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        int? existingLeadId = null;
        int? existingStageId = null;

        using (var findLinkCommand = connection.CreateCommand())
        {
            findLinkCommand.Transaction = transaction;
            findLinkCommand.CommandText = $"""
SELECT TOP (1) link.LeadId, lead.StageId
FROM dbo.{TablePrefix}telegram_funil_links link
INNER JOIN dbo.{TablePrefix}kanban_leads lead
    ON lead.Id = link.LeadId
WHERE link.ChatbotConversationId = @chatbotConversationId
  AND lead.IsActive = 1
ORDER BY link.Id;
""";
            findLinkCommand.Parameters.AddRange(
            [
                new SqlParameter("@chatbotConversationId", SqlDbType.UniqueIdentifier) { Value = request.ChatbotConversationId }
            ]);

            using var reader = findLinkCommand.ExecuteReader();
            if (reader.Read())
            {
                existingLeadId = reader.GetInt32(0);
                existingStageId = reader.GetInt32(1);
            }
        }

        var created = !existingLeadId.HasValue;
        var stageId = created
            ? ResolveStageId(connection, transaction, normalizedBoardType, requestedStageId: 0)
            : existingStageId ?? ResolveStageId(connection, transaction, normalizedBoardType, requestedStageId: 0);
        var leadId = existingLeadId ?? CreateTelegramLead(connection, transaction, normalizedBoardType, stageId, request);

        if (!created)
        {
            UpdateTelegramLead(connection, transaction, leadId, stageId, request);
            InsertHistory(
                connection,
                transaction,
                leadId,
                eventType: "telegram_lead_atualizado",
                fromStageId: null,
                toStageId: null,
                description: "Lead atualizado automaticamente a partir da conversa do bot Telegram."
            );
        }

        SaveTelegramLeadLink(connection, transaction, leadId, normalizedBoardType, request);

        transaction.Commit();

        return new AdminKanbanTelegramLeadUpsertResult
        {
            LeadId = leadId,
            Created = created,
            StageId = stageId,
            BoardType = normalizedBoardType,
            ChatbotConversationId = request.ChatbotConversationId
        };
    }

    public int? FindLeadIdByTelegramChatbotConversationId(Guid chatbotConversationId)
    {
        EnsureInitialized();

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
SELECT TOP (1) link.LeadId
FROM dbo.{TablePrefix}telegram_funil_links link
INNER JOIN dbo.{TablePrefix}kanban_leads lead ON lead.Id = link.LeadId
WHERE link.ChatbotConversationId = @chatbotConversationId
  AND lead.IsActive = 1
ORDER BY link.UpdatedAt DESC, link.Id DESC;
""";
        command.Parameters.Add(new SqlParameter("@chatbotConversationId", SqlDbType.UniqueIdentifier) { Value = chatbotConversationId });

        var result = command.ExecuteScalar();
        return result is null ? null : Convert.ToInt32(result);
    }

    public int? FindLeadIdByTelegramChatId(long telegramChatId)
    {
        EnsureInitialized();

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
SELECT TOP (1) link.LeadId
FROM dbo.{TablePrefix}telegram_funil_links link
INNER JOIN dbo.{TablePrefix}kanban_leads lead ON lead.Id = link.LeadId
WHERE link.TelegramChatId = @telegramChatId
  AND lead.IsActive = 1
ORDER BY link.UpdatedAt DESC, link.Id DESC;
""";
        command.Parameters.Add(new SqlParameter("@telegramChatId", SqlDbType.BigInt) { Value = telegramChatId });

        var result = command.ExecuteScalar();
        return result is null ? null : Convert.ToInt32(result);
    }

    public bool TouchTelegramLeadLink(int leadId, AdminKanbanTelegramLinkTouchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        EnsureInitialized();

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
UPDATE dbo.{TablePrefix}telegram_funil_links
SET HumanHandoffStartedAt = CASE
        WHEN @humanHandoffStartedAt IS NOT NULL THEN @humanHandoffStartedAt
        ELSE HumanHandoffStartedAt
    END,
    HumanHandoffStatus = CASE
        WHEN NULLIF(LTRIM(RTRIM(@humanHandoffStatus)), '') IS NOT NULL THEN LTRIM(RTRIM(@humanHandoffStatus))
        ELSE HumanHandoffStatus
    END,
    HumanHandoffReason = CASE
        WHEN NULLIF(LTRIM(RTRIM(@humanHandoffReason)), '') IS NOT NULL THEN LTRIM(RTRIM(@humanHandoffReason))
        ELSE HumanHandoffReason
    END,
    HumanHandoffUpdatedAt = CASE
        WHEN @humanHandoffUpdatedAt IS NOT NULL
             AND (HumanHandoffUpdatedAt IS NULL OR @humanHandoffUpdatedAt >= HumanHandoffUpdatedAt)
        THEN @humanHandoffUpdatedAt
        ELSE HumanHandoffUpdatedAt
    END,
    LastTelegramMessageSyncedAt = CASE
        WHEN @lastTelegramMessageSyncedAt IS NOT NULL
             AND (LastTelegramMessageSyncedAt IS NULL OR @lastTelegramMessageSyncedAt > LastTelegramMessageSyncedAt)
        THEN @lastTelegramMessageSyncedAt
        ELSE LastTelegramMessageSyncedAt
    END,
    LastChatwootMessageSyncedAt = CASE
        WHEN @lastChatwootMessageSyncedAt IS NOT NULL
             AND (LastChatwootMessageSyncedAt IS NULL OR @lastChatwootMessageSyncedAt > LastChatwootMessageSyncedAt)
        THEN @lastChatwootMessageSyncedAt
        ELSE LastChatwootMessageSyncedAt
    END,
    UpdatedAt = SYSUTCDATETIME()
WHERE LeadId = @leadId;
""";
        command.Parameters.AddRange(
        [
            new SqlParameter("@humanHandoffStartedAt", SqlDbType.DateTime2) { Value = request.HumanHandoffStartedAt.HasValue ? request.HumanHandoffStartedAt.Value : DBNull.Value },
            new SqlParameter("@humanHandoffStatus", SqlDbType.NVarChar, 40) { Value = string.IsNullOrWhiteSpace(request.HumanHandoffStatus) ? DBNull.Value : request.HumanHandoffStatus.Trim() },
            new SqlParameter("@humanHandoffReason", SqlDbType.NVarChar, 180) { Value = string.IsNullOrWhiteSpace(request.HumanHandoffReason) ? DBNull.Value : request.HumanHandoffReason.Trim() },
            new SqlParameter("@humanHandoffUpdatedAt", SqlDbType.DateTime2) { Value = request.HumanHandoffUpdatedAt.HasValue ? request.HumanHandoffUpdatedAt.Value : DBNull.Value },
            new SqlParameter("@lastTelegramMessageSyncedAt", SqlDbType.DateTime2) { Value = request.LastTelegramMessageSyncedAt.HasValue ? request.LastTelegramMessageSyncedAt.Value : DBNull.Value },
            new SqlParameter("@lastChatwootMessageSyncedAt", SqlDbType.DateTime2) { Value = request.LastChatwootMessageSyncedAt.HasValue ? request.LastChatwootMessageSyncedAt.Value : DBNull.Value },
            new SqlParameter("@leadId", SqlDbType.Int) { Value = leadId }
        ]);

        return command.ExecuteNonQuery() > 0;
    }

    public AdminKanbanTelegramDeliveryQueueItemRecord EnqueueTelegramDeliveryQueueItem(AdminKanbanTelegramDeliveryQueueEnqueueRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        EnsureInitialized();

        var direction = NormalizeTelegramDeliveryDirection(request.Direction);
        var deliveryKey = TrimTo(request.DeliveryKey, 180);
        if (string.IsNullOrWhiteSpace(deliveryKey))
        {
            throw new InvalidOperationException("DeliveryKey da fila Telegram e obrigatorio.");
        }

        var nextAttemptAt = request.NextAttemptAt.Kind == DateTimeKind.Utc
            ? request.NextAttemptAt
            : request.NextAttemptAt.ToUniversalTime();
        var maxAttempts = request.MaxAttempts > 0 ? request.MaxAttempts : 10;
        var sanitizedLastError = string.IsNullOrWhiteSpace(request.LastError)
            ? null
            : TelegramSecuritySanitizer.SanitizeMessage(request.LastError, 1000);

        using var connection = OpenConnection();
        if (TryGetTelegramDeliveryQueueItemByDirectionAndKey(connection, direction, deliveryKey, out var existingItem))
        {
            return existingItem with { IsDuplicate = true };
        }

        using var insertCommand = connection.CreateCommand();
        insertCommand.CommandText = $"""
INSERT INTO dbo.{TablePrefix}telegram_delivery_queue
(LeadId, Direction, DeliveryKey, PayloadJson, ChatwootConversationId, TelegramChatId, Status, AttemptCount, MaxAttempts, NextAttemptAt, LastAttemptAt, LastError, WorkerInstance, CreatedAt, UpdatedAt, ProcessedAt, DeadLetterAt)
VALUES
(@leadId, @direction, @deliveryKey, @payloadJson, @chatwootConversationId, @telegramChatId, 'queued', 0, @maxAttempts, @nextAttemptAt, NULL, @lastError, NULL, SYSUTCDATETIME(), SYSUTCDATETIME(), NULL, NULL);
SELECT CAST(SCOPE_IDENTITY() AS INT);
""";
        insertCommand.Parameters.AddRange(
        [
            new SqlParameter("@leadId", SqlDbType.Int) { Value = request.LeadId },
            new SqlParameter("@direction", SqlDbType.NVarChar, 40) { Value = direction },
            new SqlParameter("@deliveryKey", SqlDbType.NVarChar, 180) { Value = deliveryKey },
            new SqlParameter("@payloadJson", SqlDbType.NVarChar, -1) { Value = request.PayloadJson },
            new SqlParameter("@chatwootConversationId", SqlDbType.BigInt) { Value = request.ChatwootConversationId.HasValue ? request.ChatwootConversationId.Value : DBNull.Value },
            new SqlParameter("@telegramChatId", SqlDbType.BigInt) { Value = request.TelegramChatId.HasValue ? request.TelegramChatId.Value : DBNull.Value },
            new SqlParameter("@maxAttempts", SqlDbType.Int) { Value = maxAttempts },
            new SqlParameter("@nextAttemptAt", SqlDbType.DateTime2) { Value = nextAttemptAt },
            new SqlParameter("@lastError", SqlDbType.NVarChar, -1) { Value = ToDbValue(sanitizedLastError) }
        ]);

        try
        {
            var queueItemId = Convert.ToInt32(insertCommand.ExecuteScalar());
            return TryGetTelegramDeliveryQueueItemById(connection, queueItemId, out var insertedItem)
                ? insertedItem
                : throw new InvalidOperationException("Nao foi possivel recarregar o item da fila Telegram apos o insert.");
        }
        catch (SqlException ex) when (IsUniqueKeyViolation(ex) && TryGetTelegramDeliveryQueueItemByDirectionAndKey(connection, direction, deliveryKey, out existingItem))
        {
            return existingItem with { IsDuplicate = true };
        }
    }

    public IReadOnlyList<AdminKanbanTelegramDeliveryQueueItemRecord> AcquireDueTelegramDeliveryQueueItems(int batchSize, DateTime attemptStartedAtUtc, string workerInstance)
    {
        EnsureInitialized();

        var utcNow = attemptStartedAtUtc.Kind == DateTimeKind.Utc
            ? attemptStartedAtUtc
            : attemptStartedAtUtc.ToUniversalTime();
        var normalizedBatchSize = Math.Clamp(batchSize, 1, 500);

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
;WITH due_items AS (
    SELECT TOP (@batchSize) Id
    FROM dbo.{TablePrefix}telegram_delivery_queue WITH (READPAST, UPDLOCK, ROWLOCK)
    WHERE Status IN ('queued', 'retrying')
      AND NextAttemptAt <= @attemptStartedAtUtc
    ORDER BY NextAttemptAt, Id
)
UPDATE q
SET Status = @processingStatus,
    AttemptCount = AttemptCount + 1,
    LastAttemptAt = @attemptStartedAtUtc,
    WorkerInstance = @workerInstance,
    UpdatedAt = SYSUTCDATETIME()
OUTPUT
    inserted.Id,
    inserted.LeadId,
    inserted.Direction,
    inserted.DeliveryKey,
    inserted.PayloadJson,
    inserted.ChatwootConversationId,
    inserted.TelegramChatId,
    inserted.Status,
    inserted.AttemptCount,
    inserted.MaxAttempts,
    inserted.NextAttemptAt,
    inserted.LastAttemptAt,
    inserted.LastError,
    inserted.WorkerInstance,
    inserted.CreatedAt,
    inserted.UpdatedAt,
    inserted.ProcessedAt,
    inserted.DeadLetterAt
FROM dbo.{TablePrefix}telegram_delivery_queue q
INNER JOIN due_items d ON d.Id = q.Id;
""";
        command.Parameters.AddRange(
        [
            new SqlParameter("@batchSize", SqlDbType.Int) { Value = normalizedBatchSize },
            new SqlParameter("@attemptStartedAtUtc", SqlDbType.DateTime2) { Value = utcNow },
            new SqlParameter("@workerInstance", SqlDbType.NVarChar, 120) { Value = TrimTo(workerInstance, 120) },
            new SqlParameter("@processingStatus", SqlDbType.NVarChar, 30) { Value = TelegramDeliveryQueueStatuses.Processing }
        ]);

        var items = new List<AdminKanbanTelegramDeliveryQueueItemRecord>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            items.Add(ReadTelegramDeliveryQueueItem(reader));
        }

        return items;
    }

    public AdminKanbanTelegramDeliveryQueueItemRecord? FinalizeTelegramDeliveryQueueItem(AdminKanbanTelegramDeliveryQueueFinalizeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        EnsureInitialized();
        var sanitizedLastError = string.IsNullOrWhiteSpace(request.LastError)
            ? null
            : TelegramSecuritySanitizer.SanitizeMessage(request.LastError, 1000);

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
UPDATE dbo.{TablePrefix}telegram_delivery_queue
SET Status = @finalStatus,
    NextAttemptAt = COALESCE(@nextAttemptAt, NextAttemptAt),
    LastError = CASE
        WHEN @clearLastError = 1 THEN NULL
        WHEN @lastError IS NOT NULL THEN @lastError
        ELSE LastError
    END,
    WorkerInstance = @workerInstance,
    UpdatedAt = @finalizedAt,
    ProcessedAt = CASE
        WHEN @finalStatus IN ('processed', 'dead_letter') THEN @finalizedAt
        ELSE NULL
    END,
    DeadLetterAt = CASE
        WHEN @finalStatus = 'dead_letter' THEN @finalizedAt
        ELSE NULL
    END
WHERE Id = @queueItemId;
""";
        command.Parameters.AddRange(
        [
            new SqlParameter("@finalStatus", SqlDbType.NVarChar, 30) { Value = NormalizeTelegramDeliveryQueueStatus(request.FinalStatus) },
            new SqlParameter("@nextAttemptAt", SqlDbType.DateTime2) { Value = request.NextAttemptAt.HasValue ? request.NextAttemptAt.Value : DBNull.Value },
            new SqlParameter("@lastError", SqlDbType.NVarChar, -1) { Value = ToDbValue(sanitizedLastError) },
            new SqlParameter("@clearLastError", SqlDbType.Bit) { Value = request.ClearLastError },
            new SqlParameter("@workerInstance", SqlDbType.NVarChar, 120) { Value = ToDbValue(TrimTo(request.WorkerInstance, 120)) },
            new SqlParameter("@finalizedAt", SqlDbType.DateTime2) { Value = request.FinalizedAt.Kind == DateTimeKind.Utc ? request.FinalizedAt : request.FinalizedAt.ToUniversalTime() },
            new SqlParameter("@queueItemId", SqlDbType.Int) { Value = request.QueueItemId }
        ]);

        if (command.ExecuteNonQuery() <= 0)
        {
            return null;
        }

        return TryGetTelegramDeliveryQueueItemById(connection, request.QueueItemId, out var queueItem)
            ? queueItem
            : null;
    }

    public AdminKanbanTelegramDeliveryQueueItemRecord? RequeueTelegramDeliveryQueueItem(int queueItemId, DateTime nextAttemptAtUtc, string workerInstance)
    {
        EnsureInitialized();

        var normalizedNextAttemptAt = nextAttemptAtUtc.Kind == DateTimeKind.Utc
            ? nextAttemptAtUtc
            : nextAttemptAtUtc.ToUniversalTime();

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
UPDATE dbo.{TablePrefix}telegram_delivery_queue
SET Status = @retryingStatus,
    NextAttemptAt = @nextAttemptAtUtc,
    UpdatedAt = SYSUTCDATETIME(),
    WorkerInstance = @workerInstance
WHERE Id = @queueItemId
  AND Status IN ('queued', 'processing', 'retrying', 'dead_letter');
""";
        command.Parameters.AddRange(
        [
            new SqlParameter("@retryingStatus", SqlDbType.NVarChar, 30) { Value = TelegramDeliveryQueueStatuses.Retrying },
            new SqlParameter("@nextAttemptAtUtc", SqlDbType.DateTime2) { Value = normalizedNextAttemptAt },
            new SqlParameter("@workerInstance", SqlDbType.NVarChar, 120) { Value = ToDbValue(TrimTo(workerInstance, 120)) },
            new SqlParameter("@queueItemId", SqlDbType.Int) { Value = queueItemId }
        ]);

        if (command.ExecuteNonQuery() <= 0)
        {
            return null;
        }

        return TryGetTelegramDeliveryQueueItemById(connection, queueItemId, out var queueItem)
            ? queueItem
            : null;
    }

    public int PurgeTelegramDeliveryPayloads(DateTime createdBeforeUtc, DateTime purgedAtUtc)
    {
        EnsureInitialized();

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
UPDATE dbo.{TablePrefix}telegram_delivery_queue
SET PayloadJson = @payloadJson,
    PayloadPurgedAt = @purgedAtUtc
WHERE CreatedAt < @createdBeforeUtc
  AND PayloadPurgedAt IS NULL
  AND Status IN ('processed', 'dead_letter');
""";
        command.Parameters.AddRange(
        [
            new SqlParameter("@payloadJson", SqlDbType.NVarChar, -1) { Value = RedactedTelegramPayloadJson },
            new SqlParameter("@createdBeforeUtc", SqlDbType.DateTime2) { Value = createdBeforeUtc.Kind == DateTimeKind.Utc ? createdBeforeUtc : createdBeforeUtc.ToUniversalTime() },
            new SqlParameter("@purgedAtUtc", SqlDbType.DateTime2) { Value = purgedAtUtc.Kind == DateTimeKind.Utc ? purgedAtUtc : purgedAtUtc.ToUniversalTime() }
        ]);

        return command.ExecuteNonQuery();
    }

    public bool UpdateLead(int leadId, AdminKanbanLeadUpsertRequest request)
    {
        EnsureInitialized();
        var normalizedBoardType = AdminKanbanBoardTypes.Normalize(request.BoardType);

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        int currentStageId;
        using (var currentCommand = connection.CreateCommand())
        {
            currentCommand.Transaction = transaction;
            currentCommand.CommandText = $"""
SELECT TOP (1) StageId
FROM dbo.{TablePrefix}kanban_leads
WHERE Id = @leadId AND IsActive = 1 AND BoardType = @boardType;
""";
            currentCommand.Parameters.AddRange(
            [
                new SqlParameter("@leadId", SqlDbType.Int) { Value = leadId },
                new SqlParameter("@boardType", SqlDbType.NVarChar, 30) { Value = normalizedBoardType }
            ]);

            var stageObj = currentCommand.ExecuteScalar();
            if (stageObj is null)
            {
                return false;
            }

            currentStageId = Convert.ToInt32(stageObj);
        }

        var newStageId = ResolveStageId(connection, transaction, normalizedBoardType, request.StageId);
        var stageChanged = newStageId != currentStageId;
        var newSortOrder = stageChanged
            ? GetNextLeadSortOrder(connection, transaction, normalizedBoardType, newStageId)
            : (int?)null;

        using (var updateCommand = connection.CreateCommand())
        {
            updateCommand.Transaction = transaction;
            updateCommand.CommandText = $"""
UPDATE dbo.{TablePrefix}kanban_leads
SET StageId = @stageId,
    SortOrder = CASE WHEN @stageChanged = 1 THEN @newSortOrder ELSE SortOrder END,
    Name = @name,
    Phone = @phone,
    Email = @email,
    ServiceCategory = @serviceCategory,
    PostalCode = @postalCode,
    City = @city,
    Source = @source,
    Priority = @priority,
    StatusNote = @statusNote,
    InternalNotes = @internalNotes,
    LastContactAt = @lastContactAt,
    UpdatedAt = SYSUTCDATETIME()
WHERE Id = @leadId AND IsActive = 1 AND BoardType = @boardType;
""";
            updateCommand.Parameters.AddRange(
            [
                new SqlParameter("@stageId", SqlDbType.Int) { Value = newStageId },
                new SqlParameter("@stageChanged", SqlDbType.Bit) { Value = stageChanged },
                new SqlParameter("@newSortOrder", SqlDbType.Int) { Value = newSortOrder.HasValue ? newSortOrder.Value : DBNull.Value },
                new SqlParameter("@name", SqlDbType.NVarChar, 140) { Value = TrimTo(request.Name, 140) },
                new SqlParameter("@phone", SqlDbType.NVarChar, 30) { Value = ToDbValue(request.Phone) },
                new SqlParameter("@email", SqlDbType.NVarChar, 180) { Value = ToDbValue(request.Email) },
                new SqlParameter("@serviceCategory", SqlDbType.NVarChar, 140) { Value = ToDbValue(request.ServiceCategory) },
                new SqlParameter("@postalCode", SqlDbType.NVarChar, 9) { Value = ToDbValue(request.PostalCode) },
                new SqlParameter("@city", SqlDbType.NVarChar, 120) { Value = ToDbValue(request.City) },
                new SqlParameter("@source", SqlDbType.NVarChar, 120) { Value = ToDbValue(request.Source) },
                new SqlParameter("@priority", SqlDbType.NVarChar, 20) { Value = NormalizePriority(request.Priority) },
                new SqlParameter("@statusNote", SqlDbType.NVarChar, 500) { Value = ToDbValue(request.StatusNote) },
                new SqlParameter("@internalNotes", SqlDbType.NVarChar, -1) { Value = ToDbValue(request.InternalNotes) },
                new SqlParameter("@lastContactAt", SqlDbType.DateTime2) { Value = request.LastContactAt.HasValue ? request.LastContactAt.Value : DBNull.Value },
                new SqlParameter("@leadId", SqlDbType.Int) { Value = leadId },
                new SqlParameter("@boardType", SqlDbType.NVarChar, 30) { Value = normalizedBoardType }
            ]);

            var updatedRows = updateCommand.ExecuteNonQuery();
            if (updatedRows == 0)
            {
                return false;
            }
        }

        if (stageChanged)
        {
            InsertHistory(
                connection,
                transaction,
                leadId,
                eventType: "movido",
                fromStageId: currentStageId,
                toStageId: newStageId,
                description: "Lead movido manualmente de etapa."
            );
        }

        InsertHistory(
            connection,
            transaction,
            leadId,
            eventType: "atualizado",
            fromStageId: null,
            toStageId: null,
            description: "Dados do lead atualizados."
        );

        transaction.Commit();
        return true;
    }

    public bool UpdateLeadChatwootSync(int leadId, AdminKanbanLeadChatwootSyncUpdateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        EnsureInitialized();
        var sanitizedLastError = string.IsNullOrWhiteSpace(request.ChatwootLastError)
            ? null
            : ChatwootSecuritySanitizer.SanitizeMessage(request.ChatwootLastError, 1000);

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
UPDATE dbo.{TablePrefix}kanban_leads
SET ChatwootContactId = COALESCE(@chatwootContactId, ChatwootContactId),
    ChatwootConversationId = COALESCE(@chatwootConversationId, ChatwootConversationId),
    ChatwootInboxId = COALESCE(@chatwootInboxId, ChatwootInboxId),
    ChatwootSyncStatus = COALESCE(@chatwootSyncStatus, ChatwootSyncStatus),
    ChatwootLastSyncAt = COALESCE(@chatwootLastSyncAt, ChatwootLastSyncAt),
    ChatwootLastError = CASE
        WHEN @clearChatwootLastError = 1 THEN NULL
        WHEN @chatwootLastError IS NOT NULL THEN @chatwootLastError
        ELSE ChatwootLastError
    END,
    UpdatedAt = SYSUTCDATETIME()
WHERE Id = @leadId AND IsActive = 1;
""";
        command.Parameters.AddRange(
        [
            new SqlParameter("@chatwootContactId", SqlDbType.BigInt) { Value = request.ChatwootContactId.HasValue ? request.ChatwootContactId.Value : DBNull.Value },
            new SqlParameter("@chatwootConversationId", SqlDbType.BigInt) { Value = request.ChatwootConversationId.HasValue ? request.ChatwootConversationId.Value : DBNull.Value },
            new SqlParameter("@chatwootInboxId", SqlDbType.BigInt) { Value = request.ChatwootInboxId.HasValue ? request.ChatwootInboxId.Value : DBNull.Value },
            new SqlParameter("@chatwootSyncStatus", SqlDbType.NVarChar, 30) { Value = ToDbValue(NormalizeChatwootSyncStatus(request.ChatwootSyncStatus)) },
            new SqlParameter("@chatwootLastSyncAt", SqlDbType.DateTime2) { Value = request.ChatwootLastSyncAt.HasValue ? request.ChatwootLastSyncAt.Value : DBNull.Value },
            new SqlParameter("@chatwootLastError", SqlDbType.NVarChar, -1) { Value = ToDbValue(sanitizedLastError) },
            new SqlParameter("@clearChatwootLastError", SqlDbType.Bit) { Value = request.ClearChatwootLastError },
            new SqlParameter("@leadId", SqlDbType.Int) { Value = leadId }
        ]);

        return command.ExecuteNonQuery() > 0;
    }

    public int? FindLeadIdByChatwootConversationId(long conversationId)
    {
        EnsureInitialized();

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
SELECT TOP (1) Id
FROM dbo.{TablePrefix}kanban_leads
WHERE IsActive = 1 AND ChatwootConversationId = @conversationId
ORDER BY Id;
""";
        command.Parameters.Add(new SqlParameter("@conversationId", SqlDbType.BigInt) { Value = conversationId });

        var result = command.ExecuteScalar();
        return result is null ? null : Convert.ToInt32(result);
    }

    public bool ApplyChatwootWebhookLeadUpdate(int leadId, AdminKanbanLeadWebhookUpdateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        EnsureInitialized();

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        if (!ActiveLeadExists(connection, transaction, leadId))
        {
            return false;
        }

        if (request.LastContactAt.HasValue)
        {
            using var updateCommand = connection.CreateCommand();
            updateCommand.Transaction = transaction;
            updateCommand.CommandText = $"""
UPDATE dbo.{TablePrefix}kanban_leads
SET LastContactAt = CASE
        WHEN LastContactAt IS NULL OR @lastContactAt > LastContactAt THEN @lastContactAt
        ELSE LastContactAt
    END,
    UpdatedAt = SYSUTCDATETIME()
WHERE Id = @leadId AND IsActive = 1;
""";
            updateCommand.Parameters.AddRange(
            [
                new SqlParameter("@lastContactAt", SqlDbType.DateTime2) { Value = request.LastContactAt.Value },
                new SqlParameter("@leadId", SqlDbType.Int) { Value = leadId }
            ]);
            updateCommand.ExecuteNonQuery();
        }

        if (!string.IsNullOrWhiteSpace(request.HistoryEventType) && !string.IsNullOrWhiteSpace(request.HistoryDescription))
        {
            InsertHistory(
                connection,
                transaction,
                leadId,
                eventType: TrimTo(request.HistoryEventType, 40),
                fromStageId: null,
                toStageId: null,
                description: TrimTo(request.HistoryDescription, 3000));
        }

        transaction.Commit();
        return true;
    }

    public AdminKanbanChatwootWebhookEventRecord CreateOrGetChatwootWebhookEvent(AdminKanbanChatwootWebhookEventUpsertRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        EnsureInitialized();

        var providerEventId = string.IsNullOrWhiteSpace(request.ProviderEventId)
            ? null
            : TrimTo(request.ProviderEventId, 120);
        var eventType = TrimTo(request.EventType, 80);

        using var connection = OpenConnection();
        if (TryGetChatwootWebhookEventByProviderEventId(connection, providerEventId, out var existingWebhookEvent))
        {
            return existingWebhookEvent with { IsDuplicate = true };
        }

        using var insertCommand = connection.CreateCommand();
        insertCommand.CommandText = $"""
INSERT INTO dbo.{TablePrefix}chatwoot_webhook_events
(ProviderEventId, EventType, ConversationId, PayloadJson, Signature, ReceivedAt, ProcessStatus, ErrorMessage)
VALUES
(@providerEventId, @eventType, @conversationId, @payloadJson, @signature, @receivedAt, 'received', NULL);
SELECT CAST(SCOPE_IDENTITY() AS INT);
""";
        insertCommand.Parameters.AddRange(
        [
            new SqlParameter("@providerEventId", SqlDbType.NVarChar, 120) { Value = ToDbValue(providerEventId) },
            new SqlParameter("@eventType", SqlDbType.NVarChar, 80) { Value = eventType },
            new SqlParameter("@conversationId", SqlDbType.BigInt) { Value = request.ConversationId.HasValue ? request.ConversationId.Value : DBNull.Value },
            new SqlParameter("@payloadJson", SqlDbType.NVarChar, -1) { Value = request.PayloadJson },
            new SqlParameter("@signature", SqlDbType.NVarChar, 255) { Value = ToDbValue(request.Signature) },
            new SqlParameter("@receivedAt", SqlDbType.DateTime2) { Value = request.ReceivedAt }
        ]);

        int webhookEventId;
        try
        {
            webhookEventId = Convert.ToInt32(insertCommand.ExecuteScalar());
        }
        catch (SqlException ex) when (IsUniqueKeyViolation(ex) && TryGetChatwootWebhookEventByProviderEventId(connection, providerEventId, out existingWebhookEvent))
        {
            return existingWebhookEvent with { IsDuplicate = true };
        }

        return new AdminKanbanChatwootWebhookEventRecord
        {
            Id = webhookEventId,
            ProviderEventId = providerEventId ?? string.Empty,
            EventType = eventType,
            ConversationId = request.ConversationId,
            ProcessStatus = "received",
            ReceivedAt = request.ReceivedAt
        };
    }

    public bool CompleteChatwootWebhookEvent(int webhookEventId, string processStatus, string? errorMessage)
    {
        EnsureInitialized();
        var sanitizedErrorMessage = string.IsNullOrWhiteSpace(errorMessage)
            ? null
            : ChatwootSecuritySanitizer.SanitizeMessage(errorMessage, 1000);

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
UPDATE dbo.{TablePrefix}chatwoot_webhook_events
SET ProcessStatus = @processStatus,
    ProcessedAt = SYSUTCDATETIME(),
    ErrorMessage = @errorMessage
WHERE Id = @webhookEventId;
""";
        command.Parameters.AddRange(
        [
            new SqlParameter("@processStatus", SqlDbType.NVarChar, 30) { Value = NormalizeWebhookProcessStatus(processStatus) },
            new SqlParameter("@errorMessage", SqlDbType.NVarChar, -1) { Value = ToDbValue(sanitizedErrorMessage) },
            new SqlParameter("@webhookEventId", SqlDbType.Int) { Value = webhookEventId }
        ]);

        return command.ExecuteNonQuery() > 0;
    }

    public AdminKanbanChatwootSyncQueueItemRecord EnqueueChatwootSyncQueueItem(AdminKanbanChatwootSyncQueueEnqueueRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        EnsureInitialized();

        var operationType = NormalizeChatwootSyncOperationType(request.OperationType);
        var nextAttemptAt = request.NextAttemptAt.Kind == DateTimeKind.Utc
            ? request.NextAttemptAt
            : request.NextAttemptAt.ToUniversalTime();
        var maxAttempts = request.MaxAttempts > 0 ? request.MaxAttempts : 10;
        var sanitizedLastError = string.IsNullOrWhiteSpace(request.LastError)
            ? null
            : ChatwootSecuritySanitizer.SanitizeMessage(request.LastError, 1000);

        using var connection = OpenConnection();
        if (TryGetActiveChatwootSyncQueueItem(connection, request.LeadId, operationType, out var activeItem))
        {
            if (string.Equals(activeItem.Status, ChatwootSyncQueueStatuses.Processing, StringComparison.OrdinalIgnoreCase))
            {
                return activeItem;
            }

            using var updateCommand = connection.CreateCommand();
            updateCommand.CommandText = $"""
UPDATE dbo.{TablePrefix}chatwoot_sync_queue
SET Status = @status,
    NextAttemptAt = CASE
        WHEN NextAttemptAt > @nextAttemptAt THEN @nextAttemptAt
        ELSE NextAttemptAt
    END,
    MaxAttempts = CASE
        WHEN MaxAttempts < @maxAttempts THEN @maxAttempts
        ELSE MaxAttempts
    END,
    LastError = CASE
        WHEN @lastError IS NOT NULL THEN @lastError
        ELSE LastError
    END,
    UpdatedAt = SYSUTCDATETIME()
WHERE Id = @id AND Status IN ('queued', 'retrying');
""";
            updateCommand.Parameters.AddRange(
            [
                new SqlParameter("@status", SqlDbType.NVarChar, 30) { Value = ChatwootSyncQueueStatuses.Queued },
                new SqlParameter("@nextAttemptAt", SqlDbType.DateTime2) { Value = nextAttemptAt },
                new SqlParameter("@maxAttempts", SqlDbType.Int) { Value = maxAttempts },
                new SqlParameter("@lastError", SqlDbType.NVarChar, -1) { Value = ToDbValue(sanitizedLastError) },
                new SqlParameter("@id", SqlDbType.Int) { Value = activeItem.Id }
            ]);
            _ = updateCommand.ExecuteNonQuery();

            return TryGetChatwootSyncQueueItemById(connection, activeItem.Id, out var updatedItem)
                ? updatedItem
                : activeItem;
        }

        using var insertCommand = connection.CreateCommand();
        insertCommand.CommandText = $"""
INSERT INTO dbo.{TablePrefix}chatwoot_sync_queue
(LeadId, OperationType, Status, AttemptCount, MaxAttempts, NextAttemptAt, LastAttemptAt, LastError, WorkerInstance, CreatedAt, UpdatedAt, ProcessedAt, DeadLetterAt)
VALUES
(@leadId, @operationType, 'queued', 0, @maxAttempts, @nextAttemptAt, NULL, @lastError, NULL, SYSUTCDATETIME(), SYSUTCDATETIME(), NULL, NULL);
SELECT CAST(SCOPE_IDENTITY() AS INT);
""";
        insertCommand.Parameters.AddRange(
        [
            new SqlParameter("@leadId", SqlDbType.Int) { Value = request.LeadId },
            new SqlParameter("@operationType", SqlDbType.NVarChar, 40) { Value = operationType },
            new SqlParameter("@maxAttempts", SqlDbType.Int) { Value = maxAttempts },
            new SqlParameter("@nextAttemptAt", SqlDbType.DateTime2) { Value = nextAttemptAt },
            new SqlParameter("@lastError", SqlDbType.NVarChar, -1) { Value = ToDbValue(sanitizedLastError) }
        ]);

        try
        {
            var queueItemId = Convert.ToInt32(insertCommand.ExecuteScalar());
            if (TryGetChatwootSyncQueueItemById(connection, queueItemId, out var insertedItem))
            {
                return insertedItem;
            }

            throw new InvalidOperationException("Nao foi possivel recarregar o item da fila Chatwoot apos o insert.");
        }
        catch (SqlException ex) when (IsUniqueKeyViolation(ex) && TryGetActiveChatwootSyncQueueItem(connection, request.LeadId, operationType, out activeItem))
        {
            return activeItem;
        }
    }

    public IReadOnlyList<AdminKanbanChatwootSyncQueueItemRecord> AcquireDueChatwootSyncQueueItems(int batchSize, DateTime attemptStartedAtUtc, string workerInstance)
    {
        EnsureInitialized();

        var utcNow = attemptStartedAtUtc.Kind == DateTimeKind.Utc
            ? attemptStartedAtUtc
            : attemptStartedAtUtc.ToUniversalTime();
        var normalizedBatchSize = Math.Clamp(batchSize, 1, 500);

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
;WITH due_items AS (
    SELECT TOP (@batchSize) Id
    FROM dbo.{TablePrefix}chatwoot_sync_queue WITH (READPAST, UPDLOCK, ROWLOCK)
    WHERE Status IN ('queued', 'retrying')
      AND NextAttemptAt <= @attemptStartedAtUtc
    ORDER BY NextAttemptAt, Id
)
UPDATE q
SET Status = @processingStatus,
    AttemptCount = AttemptCount + 1,
    LastAttemptAt = @attemptStartedAtUtc,
    WorkerInstance = @workerInstance,
    UpdatedAt = SYSUTCDATETIME()
OUTPUT
    inserted.Id,
    inserted.LeadId,
    inserted.OperationType,
    inserted.Status,
    inserted.AttemptCount,
    inserted.MaxAttempts,
    inserted.NextAttemptAt,
    inserted.LastAttemptAt,
    inserted.LastError,
    inserted.WorkerInstance,
    inserted.CreatedAt,
    inserted.UpdatedAt,
    inserted.ProcessedAt,
    inserted.DeadLetterAt
FROM dbo.{TablePrefix}chatwoot_sync_queue q
INNER JOIN due_items d ON d.Id = q.Id;
""";
        command.Parameters.AddRange(
        [
            new SqlParameter("@batchSize", SqlDbType.Int) { Value = normalizedBatchSize },
            new SqlParameter("@attemptStartedAtUtc", SqlDbType.DateTime2) { Value = utcNow },
            new SqlParameter("@workerInstance", SqlDbType.NVarChar, 120) { Value = TrimTo(workerInstance, 120) },
            new SqlParameter("@processingStatus", SqlDbType.NVarChar, 30) { Value = ChatwootSyncQueueStatuses.Processing }
        ]);

        var items = new List<AdminKanbanChatwootSyncQueueItemRecord>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            items.Add(ReadChatwootSyncQueueItem(reader));
        }

        return items;
    }

    public AdminKanbanChatwootSyncQueueItemRecord? FinalizeChatwootSyncQueueItem(AdminKanbanChatwootSyncQueueFinalizeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        EnsureInitialized();
        var sanitizedLastError = string.IsNullOrWhiteSpace(request.LastError)
            ? null
            : ChatwootSecuritySanitizer.SanitizeMessage(request.LastError, 1000);

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
UPDATE dbo.{TablePrefix}chatwoot_sync_queue
SET Status = @finalStatus,
    NextAttemptAt = COALESCE(@nextAttemptAt, NextAttemptAt),
    LastError = CASE
        WHEN @clearLastError = 1 THEN NULL
        WHEN @lastError IS NOT NULL THEN @lastError
        ELSE LastError
    END,
    WorkerInstance = @workerInstance,
    UpdatedAt = @finalizedAt,
    ProcessedAt = CASE
        WHEN @finalStatus IN ('processed', 'dead_letter') THEN @finalizedAt
        ELSE NULL
    END,
    DeadLetterAt = CASE
        WHEN @finalStatus = 'dead_letter' THEN @finalizedAt
        ELSE NULL
    END
WHERE Id = @queueItemId;
""";
        command.Parameters.AddRange(
        [
            new SqlParameter("@finalStatus", SqlDbType.NVarChar, 30) { Value = NormalizeChatwootSyncQueueStatus(request.FinalStatus) },
            new SqlParameter("@nextAttemptAt", SqlDbType.DateTime2) { Value = request.NextAttemptAt.HasValue ? request.NextAttemptAt.Value : DBNull.Value },
            new SqlParameter("@lastError", SqlDbType.NVarChar, -1) { Value = ToDbValue(sanitizedLastError) },
            new SqlParameter("@clearLastError", SqlDbType.Bit) { Value = request.ClearLastError },
            new SqlParameter("@workerInstance", SqlDbType.NVarChar, 120) { Value = ToDbValue(TrimTo(request.WorkerInstance, 120)) },
            new SqlParameter("@finalizedAt", SqlDbType.DateTime2) { Value = request.FinalizedAt.Kind == DateTimeKind.Utc ? request.FinalizedAt : request.FinalizedAt.ToUniversalTime() },
            new SqlParameter("@queueItemId", SqlDbType.Int) { Value = request.QueueItemId }
        ]);

        if (command.ExecuteNonQuery() <= 0)
        {
            return null;
        }

        return TryGetChatwootSyncQueueItemById(connection, request.QueueItemId, out var queueItem)
            ? queueItem
            : null;
    }

    public int CompleteActiveChatwootSyncQueueItems(int leadId, string? operationType, string finalStatus, string? lastError, DateTime completedAtUtc)
    {
        EnsureInitialized();
        var sanitizedLastError = string.IsNullOrWhiteSpace(lastError)
            ? null
            : ChatwootSecuritySanitizer.SanitizeMessage(lastError, 1000);

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
UPDATE dbo.{TablePrefix}chatwoot_sync_queue
SET Status = @finalStatus,
    LastError = @lastError,
    UpdatedAt = @completedAtUtc,
    ProcessedAt = CASE
        WHEN @finalStatus IN ('processed', 'dead_letter') THEN @completedAtUtc
        ELSE NULL
    END,
    DeadLetterAt = CASE
        WHEN @finalStatus = 'dead_letter' THEN @completedAtUtc
        ELSE NULL
    END
WHERE LeadId = @leadId
  AND (@operationType IS NULL OR OperationType = @operationType)
  AND Status IN ('queued', 'retrying', 'processing');
""";
        command.Parameters.AddRange(
        [
            new SqlParameter("@finalStatus", SqlDbType.NVarChar, 30) { Value = NormalizeChatwootSyncQueueStatus(finalStatus) },
            new SqlParameter("@lastError", SqlDbType.NVarChar, -1) { Value = ToDbValue(sanitizedLastError) },
            new SqlParameter("@completedAtUtc", SqlDbType.DateTime2) { Value = completedAtUtc.Kind == DateTimeKind.Utc ? completedAtUtc : completedAtUtc.ToUniversalTime() },
            new SqlParameter("@leadId", SqlDbType.Int) { Value = leadId },
            new SqlParameter("@operationType", SqlDbType.NVarChar, 40) { Value = ToDbValue(string.IsNullOrWhiteSpace(operationType) ? null : NormalizeChatwootSyncOperationType(operationType)) }
        ]);

        return command.ExecuteNonQuery();
    }

    public int PurgeChatwootWebhookPayloads(DateTime receivedBeforeUtc, DateTime purgedAtUtc)
    {
        EnsureInitialized();

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
UPDATE dbo.{TablePrefix}chatwoot_webhook_events
SET PayloadJson = @payloadJson,
    Signature = NULL,
    PayloadPurgedAt = @purgedAtUtc
WHERE ReceivedAt < @receivedBeforeUtc
  AND PayloadPurgedAt IS NULL;
""";
        command.Parameters.AddRange(
        [
            new SqlParameter("@payloadJson", SqlDbType.NVarChar, -1) { Value = RedactedWebhookPayloadJson },
            new SqlParameter("@receivedBeforeUtc", SqlDbType.DateTime2) { Value = receivedBeforeUtc.Kind == DateTimeKind.Utc ? receivedBeforeUtc : receivedBeforeUtc.ToUniversalTime() },
            new SqlParameter("@purgedAtUtc", SqlDbType.DateTime2) { Value = purgedAtUtc.Kind == DateTimeKind.Utc ? purgedAtUtc : purgedAtUtc.ToUniversalTime() }
        ]);

        return command.ExecuteNonQuery();
    }

    public IReadOnlyList<AdminKanbanChatwootBackfillCandidateRecord> ListChatwootBackfillCandidates(string? boardType, int? startAfterLeadId, int batchSize)
    {
        EnsureInitialized();

        var normalizedBatchSize = Math.Clamp(batchSize, 1, 500);
        var normalizedBoardType = string.IsNullOrWhiteSpace(boardType)
            ? null
            : AdminKanbanBoardTypes.Normalize(boardType);

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
SELECT TOP (@batchSize)
    l.Id,
    l.BoardType,
    s.Name,
    l.Name,
    l.Phone,
    l.Email,
    l.Source,
    tl.ChatbotConversationId,
    tl.ChannelConversationId,
    tl.TelegramChatId,
    l.ChatwootContactId,
    l.ChatwootInboxId
FROM dbo.{TablePrefix}kanban_leads l
INNER JOIN dbo.{TablePrefix}kanban_stages s ON s.Id = l.StageId
OUTER APPLY
(
    SELECT TOP (1)
        link.ChatbotConversationId,
        link.ChannelConversationId,
        link.TelegramChatId
    FROM dbo.{TablePrefix}telegram_funil_links link
    WHERE link.LeadId = l.Id
    ORDER BY link.UpdatedAt DESC, link.Id DESC
) tl
WHERE l.IsActive = 1
  AND l.ChatwootConversationId IS NULL
  AND (@boardType IS NULL OR l.BoardType = @boardType)
  AND (@startAfterLeadId IS NULL OR l.Id > @startAfterLeadId)
ORDER BY l.Id;
""";
        command.Parameters.AddRange(
        [
            new SqlParameter("@batchSize", SqlDbType.Int) { Value = normalizedBatchSize },
            new SqlParameter("@boardType", SqlDbType.NVarChar, 30) { Value = ToDbValue(normalizedBoardType) },
            new SqlParameter("@startAfterLeadId", SqlDbType.Int) { Value = startAfterLeadId.HasValue ? startAfterLeadId.Value : DBNull.Value }
        ]);

        var items = new List<AdminKanbanChatwootBackfillCandidateRecord>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            items.Add(new AdminKanbanChatwootBackfillCandidateRecord
            {
                LeadId = reader.GetInt32(0),
                BoardType = reader.GetString(1),
                StageName = reader.GetString(2),
                LeadName = reader.GetString(3),
                Phone = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                Email = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                Source = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                TelegramChatbotConversationId = reader.IsDBNull(7) ? null : reader.GetGuid(7),
                TelegramChannelConversationId = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
                TelegramChatId = ReadNullableInt64(reader, 9),
                ChatwootContactId = ReadNullableInt64(reader, 10),
                ChatwootInboxId = ReadNullableInt64(reader, 11)
            });
        }

        return items;
    }

    public AdminKanbanChatwootBackfillCheckpointRecord? GetChatwootBackfillCheckpoint(string scopeKey)
    {
        EnsureInitialized();

        ArgumentException.ThrowIfNullOrWhiteSpace(scopeKey);

        using var connection = OpenConnection();
        return TryGetChatwootBackfillCheckpoint(connection, TrimTo(scopeKey, 80), out var checkpoint)
            ? checkpoint
            : null;
    }

    public AdminKanbanChatwootBackfillCheckpointRecord SaveChatwootBackfillCheckpoint(AdminKanbanChatwootBackfillCheckpointUpsertRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        EnsureInitialized();

        var scopeKey = TrimTo(request.ScopeKey, 80);
        if (string.IsNullOrWhiteSpace(scopeKey))
        {
            throw new InvalidOperationException("Checkpoint de backfill do Chatwoot requer escopo valido.");
        }

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
MERGE dbo.{TablePrefix}chatwoot_backfill_checkpoints AS target
USING (
    SELECT
        @scopeKey AS ScopeKey,
        @lastProcessedLeadId AS LastProcessedLeadId,
        @lastRunStartedAt AS LastRunStartedAt,
        @lastRunCompletedAt AS LastRunCompletedAt,
        @lastRunStatus AS LastRunStatus,
        @lastSummaryJson AS LastSummaryJson
) AS source
ON target.ScopeKey = source.ScopeKey
WHEN MATCHED THEN
    UPDATE SET
        LastProcessedLeadId = source.LastProcessedLeadId,
        LastRunStartedAt = source.LastRunStartedAt,
        LastRunCompletedAt = source.LastRunCompletedAt,
        LastRunStatus = source.LastRunStatus,
        LastSummaryJson = source.LastSummaryJson,
        UpdatedAt = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
    INSERT (ScopeKey, LastProcessedLeadId, LastRunStartedAt, LastRunCompletedAt, LastRunStatus, LastSummaryJson, UpdatedAt)
    VALUES (source.ScopeKey, source.LastProcessedLeadId, source.LastRunStartedAt, source.LastRunCompletedAt, source.LastRunStatus, source.LastSummaryJson, SYSUTCDATETIME());
""";
        command.Parameters.AddRange(
        [
            new SqlParameter("@scopeKey", SqlDbType.NVarChar, 80) { Value = scopeKey },
            new SqlParameter("@lastProcessedLeadId", SqlDbType.Int) { Value = request.LastProcessedLeadId.HasValue ? request.LastProcessedLeadId.Value : DBNull.Value },
            new SqlParameter("@lastRunStartedAt", SqlDbType.DateTime2) { Value = request.LastRunStartedAt.HasValue ? request.LastRunStartedAt.Value : DBNull.Value },
            new SqlParameter("@lastRunCompletedAt", SqlDbType.DateTime2) { Value = request.LastRunCompletedAt.HasValue ? request.LastRunCompletedAt.Value : DBNull.Value },
            new SqlParameter("@lastRunStatus", SqlDbType.NVarChar, 30) { Value = ToDbValue(TrimTo(request.LastRunStatus, 30)) },
            new SqlParameter("@lastSummaryJson", SqlDbType.NVarChar, -1) { Value = ToDbValue(request.LastSummaryJson) }
        ]);
        _ = command.ExecuteNonQuery();

        return TryGetChatwootBackfillCheckpoint(connection, scopeKey, out var checkpoint)
            ? checkpoint
            : throw new InvalidOperationException("Nao foi possivel recarregar o checkpoint de backfill do Chatwoot.");
    }

    public AdminKanbanTelegramDiagnosticsSnapshot GetTelegramDiagnostics(string? boardType, int issueLimit, int queueLimit)
    {
        EnsureInitialized();

        var normalizedBoardType = string.IsNullOrWhiteSpace(boardType)
            ? null
            : AdminKanbanBoardTypes.Normalize(boardType);
        var normalizedIssueLimit = Math.Clamp(issueLimit, 1, 100);
        var normalizedQueueLimit = Math.Clamp(queueLimit, 1, 100);

        using var connection = OpenConnection();

        var snapshot = new AdminKanbanTelegramDiagnosticsSnapshot
        {
            ScopeBoardType = normalizedBoardType ?? string.Empty,
            RecentIssues = [],
            RecentQueueItems = []
        };

        using (var summaryCommand = connection.CreateCommand())
        {
            summaryCommand.CommandText = $"""
SELECT
    COUNT(DISTINCT l.Id) AS TotalTelegramLeads,
    SUM(CASE WHEN tl.LastTelegramMessageSyncedAt IS NOT NULL THEN 1 ELSE 0 END) AS LeadsWithInboundMirror,
    SUM(CASE WHEN tl.LastChatwootMessageSyncedAt IS NOT NULL THEN 1 ELSE 0 END) AS LeadsWithOutboundMirror,
    SUM(CASE WHEN tl.HumanHandoffStartedAt IS NOT NULL THEN 1 ELSE 0 END) AS HumanHandoffCount
FROM dbo.{TablePrefix}kanban_leads l
INNER JOIN dbo.{TablePrefix}telegram_funil_links tl ON tl.LeadId = l.Id
WHERE l.IsActive = 1
  AND l.Source = 'Telegram'
  AND (@boardType IS NULL OR l.BoardType = @boardType);

SELECT
    SUM(CASE WHEN q.Status IN ('queued', 'processing', 'retrying') THEN 1 ELSE 0 END) AS ActiveQueueCount,
    SUM(CASE WHEN q.Status = 'dead_letter' THEN 1 ELSE 0 END) AS DeadLetterCount
FROM dbo.{TablePrefix}telegram_delivery_queue q
INNER JOIN dbo.{TablePrefix}kanban_leads l ON l.Id = q.LeadId
WHERE l.IsActive = 1
  AND (@boardType IS NULL OR l.BoardType = @boardType);
""";
            summaryCommand.Parameters.Add(new SqlParameter("@boardType", SqlDbType.NVarChar, 30) { Value = ToDbValue(normalizedBoardType) });

            using var summaryReader = summaryCommand.ExecuteReader();
            if (summaryReader.Read())
            {
                snapshot = snapshot with
                {
                    TotalTelegramLeads = summaryReader.IsDBNull(0) ? 0 : summaryReader.GetInt32(0),
                    LeadsWithInboundMirror = summaryReader.IsDBNull(1) ? 0 : summaryReader.GetInt32(1),
                    LeadsWithOutboundMirror = summaryReader.IsDBNull(2) ? 0 : summaryReader.GetInt32(2),
                    HumanHandoffCount = summaryReader.IsDBNull(3) ? 0 : summaryReader.GetInt32(3)
                };
            }

            if (summaryReader.NextResult() && summaryReader.Read())
            {
                snapshot = snapshot with
                {
                    ActiveQueueCount = summaryReader.IsDBNull(0) ? 0 : summaryReader.GetInt32(0),
                    DeadLetterCount = summaryReader.IsDBNull(1) ? 0 : summaryReader.GetInt32(1)
                };
            }
        }

        var issues = new List<AdminKanbanTelegramSyncIssueRecord>();
        using (var issuesCommand = connection.CreateCommand())
        {
            issuesCommand.CommandText = $"""
SELECT TOP (@issueLimit)
    q.Id,
    l.Id,
    l.BoardType,
    s.Name,
    l.Name,
    q.Direction,
    q.Status,
    q.AttemptCount,
    q.MaxAttempts,
    q.LastAttemptAt,
    q.LastError,
    q.ChatwootConversationId,
    q.TelegramChatId
FROM dbo.{TablePrefix}telegram_delivery_queue q
INNER JOIN dbo.{TablePrefix}kanban_leads l ON l.Id = q.LeadId
INNER JOIN dbo.{TablePrefix}kanban_stages s ON s.Id = l.StageId
WHERE l.IsActive = 1
  AND (@boardType IS NULL OR l.BoardType = @boardType)
  AND q.Status IN ('retrying', 'dead_letter')
ORDER BY
    CASE WHEN q.Status = 'dead_letter' THEN 0 ELSE 1 END,
    COALESCE(q.LastAttemptAt, q.UpdatedAt) DESC,
    q.Id DESC;
""";
            issuesCommand.Parameters.AddRange(
            [
                new SqlParameter("@issueLimit", SqlDbType.Int) { Value = normalizedIssueLimit },
                new SqlParameter("@boardType", SqlDbType.NVarChar, 30) { Value = ToDbValue(normalizedBoardType) }
            ]);

            using var issueReader = issuesCommand.ExecuteReader();
            while (issueReader.Read())
            {
                issues.Add(new AdminKanbanTelegramSyncIssueRecord
                {
                    QueueItemId = issueReader.GetInt32(0),
                    LeadId = issueReader.GetInt32(1),
                    BoardType = issueReader.GetString(2),
                    StageName = issueReader.GetString(3),
                    LeadName = issueReader.GetString(4),
                    Direction = issueReader.GetString(5),
                    Status = issueReader.GetString(6),
                    AttemptCount = issueReader.GetInt32(7),
                    MaxAttempts = issueReader.GetInt32(8),
                    LastAttemptAt = ReadNullableUtcDateTime(issueReader, 9),
                    LastError = issueReader.IsDBNull(10) ? string.Empty : issueReader.GetString(10),
                    ChatwootConversationId = ReadNullableInt64(issueReader, 11),
                    TelegramChatId = ReadNullableInt64(issueReader, 12)
                });
            }
        }

        var queueItems = new List<AdminKanbanTelegramQueueDiagnosticRecord>();
        using (var queueCommand = connection.CreateCommand())
        {
            queueCommand.CommandText = $"""
SELECT TOP (@queueLimit)
    q.Id,
    l.Id,
    l.BoardType,
    s.Name,
    l.Name,
    q.Direction,
    q.Status,
    q.AttemptCount,
    q.MaxAttempts,
    q.NextAttemptAt,
    q.LastAttemptAt,
    q.LastError,
    q.ChatwootConversationId,
    q.TelegramChatId
FROM dbo.{TablePrefix}telegram_delivery_queue q
INNER JOIN dbo.{TablePrefix}kanban_leads l ON l.Id = q.LeadId
INNER JOIN dbo.{TablePrefix}kanban_stages s ON s.Id = l.StageId
WHERE l.IsActive = 1
  AND (@boardType IS NULL OR l.BoardType = @boardType)
  AND q.Status IN ('queued', 'processing', 'retrying', 'dead_letter')
ORDER BY
    CASE WHEN q.Status = 'dead_letter' THEN 0 ELSE 1 END,
    q.NextAttemptAt,
    q.Id DESC;
""";
            queueCommand.Parameters.AddRange(
            [
                new SqlParameter("@queueLimit", SqlDbType.Int) { Value = normalizedQueueLimit },
                new SqlParameter("@boardType", SqlDbType.NVarChar, 30) { Value = ToDbValue(normalizedBoardType) }
            ]);

            using var queueReader = queueCommand.ExecuteReader();
            while (queueReader.Read())
            {
                queueItems.Add(new AdminKanbanTelegramQueueDiagnosticRecord
                {
                    QueueItemId = queueReader.GetInt32(0),
                    LeadId = queueReader.GetInt32(1),
                    BoardType = queueReader.GetString(2),
                    StageName = queueReader.GetString(3),
                    LeadName = queueReader.GetString(4),
                    Direction = queueReader.GetString(5),
                    Status = queueReader.GetString(6),
                    AttemptCount = queueReader.GetInt32(7),
                    MaxAttempts = queueReader.GetInt32(8),
                    NextAttemptAt = ReadAsUtcDateTime(queueReader, 9),
                    LastAttemptAt = ReadNullableUtcDateTime(queueReader, 10),
                    LastError = queueReader.IsDBNull(11) ? string.Empty : queueReader.GetString(11),
                    ChatwootConversationId = ReadNullableInt64(queueReader, 12),
                    TelegramChatId = ReadNullableInt64(queueReader, 13)
                });
            }
        }

        return snapshot with
        {
            RecentIssues = issues,
            RecentQueueItems = queueItems
        };
    }

    public AdminKanbanTelegramBusinessDashboardSnapshot GetTelegramBusinessDashboard(AdminKanbanTelegramBusinessDashboardFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);

        EnsureInitialized();

        if (filter.CreatedToUtcExclusive <= filter.CreatedFromUtc)
        {
            throw new InvalidOperationException("Periodo invalido para o painel Telegram.");
        }

        var normalizedBoardType = string.IsNullOrWhiteSpace(filter.BoardType)
            ? null
            : AdminKanbanBoardTypes.Normalize(filter.BoardType);
        var normalizedBreakdownLimit = Math.Clamp(filter.BreakdownLimit, 3, 20);

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
DECLARE @scoped TABLE
(
    LeadId INT NOT NULL,
    BoardType NVARCHAR(30) NOT NULL,
    StageName NVARCHAR(120) NOT NULL,
    Phone NVARCHAR(80) NULL,
    Email NVARCHAR(160) NULL,
    ServiceCategory NVARCHAR(120) NULL,
    City NVARCHAR(120) NULL,
    CreatedAt DATETIME2 NOT NULL,
    LastContactAt DATETIME2 NULL,
    ChatwootConversationId BIGINT NULL,
    ChatwootLastSyncAt DATETIME2 NULL,
    ClientPhone NVARCHAR(80) NULL,
    ClientEmail NVARCHAR(160) NULL,
    HumanHandoffStartedAt DATETIME2 NULL,
    HumanHandoffReason NVARCHAR(200) NULL
);

INSERT INTO @scoped
(
    LeadId,
    BoardType,
    StageName,
    Phone,
    Email,
    ServiceCategory,
    City,
    CreatedAt,
    LastContactAt,
    ChatwootConversationId,
    ChatwootLastSyncAt,
    ClientPhone,
    ClientEmail,
    HumanHandoffStartedAt,
    HumanHandoffReason
)
SELECT
    l.Id,
    l.BoardType,
    s.Name,
    l.Phone,
    l.Email,
    l.ServiceCategory,
    l.City,
    l.CreatedAt,
    l.LastContactAt,
    l.ChatwootConversationId,
    l.ChatwootLastSyncAt,
    tl.ClientPhone,
    tl.ClientEmail,
    tl.HumanHandoffStartedAt,
    tl.HumanHandoffReason
FROM dbo.{TablePrefix}kanban_leads l
INNER JOIN dbo.{TablePrefix}kanban_stages s ON s.Id = l.StageId
OUTER APPLY (
    SELECT TOP (1)
        link.ClientPhone,
        link.ClientEmail,
        link.HumanHandoffStartedAt,
        link.HumanHandoffReason
    FROM dbo.{TablePrefix}telegram_funil_links link
    WHERE link.LeadId = l.Id
    ORDER BY link.UpdatedAt DESC, link.Id DESC
) tl
WHERE l.IsActive = 1
  AND l.Source = 'Telegram'
  AND EXISTS (
        SELECT 1
        FROM dbo.{TablePrefix}telegram_funil_links existingLink
        WHERE existingLink.LeadId = l.Id
    )
  AND l.CreatedAt >= @createdFromUtc
  AND l.CreatedAt < @createdToUtcExclusive
  AND (@boardType IS NULL OR l.BoardType = @boardType);

SELECT
    COUNT(1) AS TotalTelegramLeads,
    SUM(CASE WHEN BoardType = @clientsBoardType THEN 1 ELSE 0 END) AS ClientsLeads,
    SUM(CASE WHEN BoardType = @providersBoardType THEN 1 ELSE 0 END) AS ProvidersLeads,
    SUM(CASE WHEN NULLIF(LTRIM(RTRIM(COALESCE(Phone, ClientPhone, ''))), '') IS NOT NULL THEN 1 ELSE 0 END) AS LeadsWithPhone,
    SUM(CASE WHEN NULLIF(LTRIM(RTRIM(COALESCE(Email, ClientEmail, ''))), '') IS NOT NULL THEN 1 ELSE 0 END) AS LeadsWithEmail,
    SUM(CASE WHEN
            NULLIF(LTRIM(RTRIM(COALESCE(Phone, ClientPhone, ''))), '') IS NOT NULL
            OR NULLIF(LTRIM(RTRIM(COALESCE(Email, ClientEmail, ''))), '') IS NOT NULL
        THEN 1 ELSE 0 END) AS LeadsWithContactInfo,
    SUM(CASE WHEN NULLIF(LTRIM(RTRIM(COALESCE(ServiceCategory, ''))), '') IS NOT NULL THEN 1 ELSE 0 END) AS LeadsWithQualifiedCategory,
    SUM(CASE WHEN NULLIF(LTRIM(RTRIM(COALESCE(City, ''))), '') IS NOT NULL THEN 1 ELSE 0 END) AS LeadsWithQualifiedCity,
    SUM(CASE WHEN ChatwootConversationId IS NOT NULL THEN 1 ELSE 0 END) AS LeadsWithChatwootConversation,
    SUM(CASE WHEN HumanHandoffStartedAt IS NOT NULL THEN 1 ELSE 0 END) AS LeadsWithHumanHandoff
FROM @scoped;

SELECT
    (
        SELECT CAST(ROUND(MAX(MedianValue), 0) AS INT)
        FROM (
            SELECT PERCENTILE_CONT(0.5) WITHIN GROUP (ORDER BY DATEDIFF(MINUTE, CreatedAt, ChatwootLastSyncAt)) OVER () AS MedianValue
            FROM @scoped
            WHERE ChatwootLastSyncAt IS NOT NULL
        ) chatwootMedian
    ) AS MedianMinutesToChatwoot,
    (
        SELECT CAST(ROUND(MAX(MedianValue), 0) AS INT)
        FROM (
            SELECT PERCENTILE_CONT(0.5) WITHIN GROUP (ORDER BY DATEDIFF(MINUTE, CreatedAt, HumanHandoffStartedAt)) OVER () AS MedianValue
            FROM @scoped
            WHERE HumanHandoffStartedAt IS NOT NULL
        ) handoffMedian
    ) AS MedianMinutesToHandoff;

SELECT
    BoardType,
    COUNT(1) AS TotalLeads,
    SUM(CASE WHEN
            NULLIF(LTRIM(RTRIM(COALESCE(ServiceCategory, ''))), '') IS NOT NULL
            AND NULLIF(LTRIM(RTRIM(COALESCE(City, ''))), '') IS NOT NULL
        THEN 1 ELSE 0 END) AS QualifiedLeadCount,
    SUM(CASE WHEN
            NULLIF(LTRIM(RTRIM(COALESCE(Phone, ClientPhone, ''))), '') IS NOT NULL
            OR NULLIF(LTRIM(RTRIM(COALESCE(Email, ClientEmail, ''))), '') IS NOT NULL
        THEN 1 ELSE 0 END) AS LeadsWithContactInfo,
    SUM(CASE WHEN ChatwootConversationId IS NOT NULL THEN 1 ELSE 0 END) AS LeadsWithChatwootConversation,
    SUM(CASE WHEN HumanHandoffStartedAt IS NOT NULL THEN 1 ELSE 0 END) AS LeadsWithHumanHandoff,
    CAST(AVG(CASE WHEN ChatwootLastSyncAt IS NOT NULL THEN CAST(DATEDIFF(MINUTE, CreatedAt, ChatwootLastSyncAt) AS DECIMAL(18, 2)) END) AS DECIMAL(18, 2)) AS AverageMinutesToChatwoot,
    CAST(AVG(CASE WHEN HumanHandoffStartedAt IS NOT NULL THEN CAST(DATEDIFF(MINUTE, CreatedAt, HumanHandoffStartedAt) AS DECIMAL(18, 2)) END) AS DECIMAL(18, 2)) AS AverageMinutesToHandoff
FROM @scoped
GROUP BY BoardType
ORDER BY CASE WHEN BoardType = @clientsBoardType THEN 0 ELSE 1 END, BoardType;

SELECT
    CAST(DATEADD(HOUR, -3, CreatedAt) AS date) AS ReferenceDateLocal,
    SUM(CASE WHEN BoardType = @clientsBoardType THEN 1 ELSE 0 END) AS ClientsLeads,
    SUM(CASE WHEN BoardType = @providersBoardType THEN 1 ELSE 0 END) AS ProvidersLeads,
    COUNT(1) AS TotalLeads
FROM @scoped
GROUP BY CAST(DATEADD(HOUR, -3, CreatedAt) AS date)
ORDER BY ReferenceDateLocal DESC;

SELECT TOP (@breakdownLimit)
    COALESCE(NULLIF(LTRIM(RTRIM(ServiceCategory)), ''), 'Nao informado') AS ServiceCategory,
    COUNT(1) AS TotalLeads,
    SUM(CASE WHEN ChatwootConversationId IS NOT NULL THEN 1 ELSE 0 END) AS LeadsWithChatwootConversation,
    SUM(CASE WHEN HumanHandoffStartedAt IS NOT NULL THEN 1 ELSE 0 END) AS LeadsWithHumanHandoff
FROM @scoped
GROUP BY COALESCE(NULLIF(LTRIM(RTRIM(ServiceCategory)), ''), 'Nao informado')
ORDER BY COUNT(1) DESC, ServiceCategory;

SELECT TOP (@breakdownLimit)
    COALESCE(NULLIF(LTRIM(RTRIM(City)), ''), 'Nao informada') AS City,
    COUNT(1) AS TotalLeads,
    SUM(CASE WHEN ChatwootConversationId IS NOT NULL THEN 1 ELSE 0 END) AS LeadsWithChatwootConversation,
    SUM(CASE WHEN HumanHandoffStartedAt IS NOT NULL THEN 1 ELSE 0 END) AS LeadsWithHumanHandoff
FROM @scoped
GROUP BY COALESCE(NULLIF(LTRIM(RTRIM(City)), ''), 'Nao informada')
ORDER BY COUNT(1) DESC, City;

SELECT TOP (@breakdownLimit)
    BoardType,
    StageName,
    COUNT(1) AS TotalLeads,
    SUM(CASE WHEN
            NULLIF(LTRIM(RTRIM(COALESCE(Phone, ClientPhone, ''))), '') IS NULL
            AND NULLIF(LTRIM(RTRIM(COALESCE(Email, ClientEmail, ''))), '') IS NULL
        THEN 1 ELSE 0 END) AS LeadsWithoutContactInfo,
    SUM(CASE WHEN ChatwootConversationId IS NULL THEN 1 ELSE 0 END) AS LeadsWithoutChatwootConversation,
    SUM(CASE WHEN LastContactAt IS NULL OR DATEDIFF(HOUR, LastContactAt, SYSUTCDATETIME()) >= 24 THEN 1 ELSE 0 END) AS LeadsWithoutRecentContact,
    CAST(AVG(CAST(DATEDIFF(HOUR, CreatedAt, SYSUTCDATETIME()) AS DECIMAL(18, 2))) AS DECIMAL(18, 2)) AS AverageLeadAgeHours
FROM @scoped
GROUP BY BoardType, StageName
ORDER BY
    COUNT(1) DESC,
    SUM(CASE WHEN ChatwootConversationId IS NULL THEN 1 ELSE 0 END) DESC,
    AVG(CAST(DATEDIFF(HOUR, CreatedAt, SYSUTCDATETIME()) AS DECIMAL(18, 2))) DESC,
    StageName;

SELECT TOP (@breakdownLimit)
    COALESCE(NULLIF(LTRIM(RTRIM(HumanHandoffReason)), ''), 'Sem motivo registrado') AS Reason,
    COUNT(1) AS TotalLeads
FROM @scoped
WHERE HumanHandoffStartedAt IS NOT NULL
GROUP BY COALESCE(NULLIF(LTRIM(RTRIM(HumanHandoffReason)), ''), 'Sem motivo registrado')
ORDER BY COUNT(1) DESC, Reason;
""";
        command.Parameters.AddRange(
        [
            new SqlParameter("@createdFromUtc", SqlDbType.DateTime2) { Value = filter.CreatedFromUtc },
            new SqlParameter("@createdToUtcExclusive", SqlDbType.DateTime2) { Value = filter.CreatedToUtcExclusive },
            new SqlParameter("@boardType", SqlDbType.NVarChar, 30) { Value = ToDbValue(normalizedBoardType) },
            new SqlParameter("@clientsBoardType", SqlDbType.NVarChar, 30) { Value = AdminKanbanBoardTypes.Clients },
            new SqlParameter("@providersBoardType", SqlDbType.NVarChar, 30) { Value = AdminKanbanBoardTypes.Providers },
            new SqlParameter("@breakdownLimit", SqlDbType.Int) { Value = normalizedBreakdownLimit }
        ]);

        var snapshot = new AdminKanbanTelegramBusinessDashboardSnapshot
        {
            ScopeBoardType = normalizedBoardType ?? string.Empty,
            CreatedFromUtc = filter.CreatedFromUtc,
            CreatedToUtcExclusive = filter.CreatedToUtcExclusive
        };

        using var reader = command.ExecuteReader();
        if (reader.Read())
        {
            snapshot = snapshot with
            {
                TotalTelegramLeads = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                ClientsLeads = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                ProvidersLeads = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                LeadsWithPhone = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                LeadsWithEmail = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                LeadsWithContactInfo = reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
                LeadsWithQualifiedCategory = reader.IsDBNull(6) ? 0 : reader.GetInt32(6),
                LeadsWithQualifiedCity = reader.IsDBNull(7) ? 0 : reader.GetInt32(7),
                LeadsWithChatwootConversation = reader.IsDBNull(8) ? 0 : reader.GetInt32(8),
                LeadsWithHumanHandoff = reader.IsDBNull(9) ? 0 : reader.GetInt32(9)
            };
        }

        if (reader.NextResult() && reader.Read())
        {
            snapshot = snapshot with
            {
                MedianMinutesToChatwoot = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                MedianMinutesToHandoff = reader.IsDBNull(1) ? 0 : reader.GetInt32(1)
            };
        }

        var boardBreakdown = new List<AdminKanbanTelegramBusinessBoardBreakdownRecord>();
        if (reader.NextResult())
        {
            while (reader.Read())
            {
                boardBreakdown.Add(new AdminKanbanTelegramBusinessBoardBreakdownRecord
                {
                    BoardType = reader.GetString(0),
                    TotalLeads = reader.GetInt32(1),
                    QualifiedLeadCount = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                    LeadsWithContactInfo = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                    LeadsWithChatwootConversation = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                    LeadsWithHumanHandoff = reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
                    AverageMinutesToChatwoot = reader.IsDBNull(6) ? null : reader.GetDecimal(6),
                    AverageMinutesToHandoff = reader.IsDBNull(7) ? null : reader.GetDecimal(7)
                });
            }
        }

        var dailyVolumes = new List<AdminKanbanTelegramBusinessDailyVolumeRecord>();
        if (reader.NextResult())
        {
            while (reader.Read())
            {
                dailyVolumes.Add(new AdminKanbanTelegramBusinessDailyVolumeRecord
                {
                    ReferenceDateLocal = reader.GetDateTime(0),
                    ClientsLeads = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                    ProvidersLeads = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                    TotalLeads = reader.IsDBNull(3) ? 0 : reader.GetInt32(3)
                });
            }
        }

        var topCategories = new List<AdminKanbanTelegramBusinessCategoryRecord>();
        if (reader.NextResult())
        {
            while (reader.Read())
            {
                topCategories.Add(new AdminKanbanTelegramBusinessCategoryRecord
                {
                    ServiceCategory = reader.GetString(0),
                    TotalLeads = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                    LeadsWithChatwootConversation = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                    LeadsWithHumanHandoff = reader.IsDBNull(3) ? 0 : reader.GetInt32(3)
                });
            }
        }

        var topCities = new List<AdminKanbanTelegramBusinessCityRecord>();
        if (reader.NextResult())
        {
            while (reader.Read())
            {
                topCities.Add(new AdminKanbanTelegramBusinessCityRecord
                {
                    City = reader.GetString(0),
                    TotalLeads = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                    LeadsWithChatwootConversation = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                    LeadsWithHumanHandoff = reader.IsDBNull(3) ? 0 : reader.GetInt32(3)
                });
            }
        }

        var stagePressures = new List<AdminKanbanTelegramBusinessStagePressureRecord>();
        if (reader.NextResult())
        {
            while (reader.Read())
            {
                stagePressures.Add(new AdminKanbanTelegramBusinessStagePressureRecord
                {
                    BoardType = reader.GetString(0),
                    StageName = reader.GetString(1),
                    TotalLeads = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                    LeadsWithoutContactInfo = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                    LeadsWithoutChatwootConversation = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                    LeadsWithoutRecentContact = reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
                    AverageLeadAgeHours = reader.IsDBNull(6) ? null : reader.GetDecimal(6)
                });
            }
        }

        var handoffReasons = new List<AdminKanbanTelegramBusinessHandoffReasonRecord>();
        if (reader.NextResult())
        {
            while (reader.Read())
            {
                handoffReasons.Add(new AdminKanbanTelegramBusinessHandoffReasonRecord
                {
                    Reason = reader.GetString(0),
                    TotalLeads = reader.IsDBNull(1) ? 0 : reader.GetInt32(1)
                });
            }
        }

        return snapshot with
        {
            BoardBreakdown = boardBreakdown,
            DailyVolumes = dailyVolumes,
            TopCategories = topCategories,
            TopCities = topCities,
            StagePressures = stagePressures,
            HandoffReasons = handoffReasons
        };
    }

    public AdminKanbanJourneyOperationsDashboardSnapshot GetJourneyOperationsDashboard(AdminKanbanJourneyOperationsDashboardFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);

        EnsureInitialized();
        EnsureJourneySchemaInitialized();

        if (filter.CreatedToUtcExclusive <= filter.CreatedFromUtc)
        {
            throw new InvalidOperationException("Periodo invalido para o painel da jornada.");
        }

        var normalizedBoardType = string.IsNullOrWhiteSpace(filter.BoardType)
            ? null
            : AdminKanbanBoardTypes.Normalize(filter.BoardType);
        var normalizedSourceChannel = string.IsNullOrWhiteSpace(filter.SourceChannel)
            ? null
            : AdminKanbanJourneySourceChannels.Normalize(filter.SourceChannel);
        var normalizedBreakdownLimit = Math.Clamp(filter.BreakdownLimit, 3, 20);

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
DECLARE @scoped TABLE
(
    JourneyId INT NOT NULL,
    LeadId INT NOT NULL,
    BoardType NVARCHAR(30) NOT NULL,
    StageName NVARCHAR(120) NOT NULL,
    SourceChannel NVARCHAR(40) NOT NULL,
    CurrentState NVARCHAR(40) NOT NULL,
    ServiceCategory NVARCHAR(160) NULL,
    City NVARCHAR(120) NULL,
    CreatedAt DATETIME2 NOT NULL,
    SchedulingStatus NVARCHAR(40) NULL,
    SchedulingConfirmedAtUtc DATETIME2 NULL,
    DispatchStatus NVARCHAR(40) NULL,
    DispatchCurrentWaveNumber INT NULL,
    DispatchReservedProviderId UNIQUEIDENTIFIER NULL,
    DispatchReservedAtUtc DATETIME2 NULL,
    ClosureStatus NVARCHAR(40) NULL,
    ClosureCompletedAtUtc DATETIME2 NULL,
    LastStageAutomationReason NVARCHAR(180) NULL,
    ActiveTimerDueAtUtc DATETIME2 NULL
);

INSERT INTO @scoped
(
    JourneyId,
    LeadId,
    BoardType,
    StageName,
    SourceChannel,
    CurrentState,
    ServiceCategory,
    City,
    CreatedAt,
    SchedulingStatus,
    SchedulingConfirmedAtUtc,
    DispatchStatus,
    DispatchCurrentWaveNumber,
    DispatchReservedProviderId,
    DispatchReservedAtUtc,
    ClosureStatus,
    ClosureCompletedAtUtc,
    LastStageAutomationReason,
    ActiveTimerDueAtUtc
)
SELECT
    j.Id,
    j.LeadId,
    j.BoardType,
    s.Name,
    j.SourceChannel,
    j.CurrentState,
    NULLIF(LTRIM(RTRIM(COALESCE(j.MatchingRequestedCategory, l.ServiceCategory))), ''),
    NULLIF(LTRIM(RTRIM(COALESCE(JSON_VALUE(j.QualificationJson, '$.city'), l.City))), ''),
    j.CreatedAt,
    j.SchedulingStatus,
    j.SchedulingConfirmedAtUtc,
    j.DispatchStatus,
    j.DispatchCurrentWaveNumber,
    j.DispatchReservedProviderId,
    j.DispatchReservedAtUtc,
    j.ClosureStatus,
    j.ClosureCompletedAtUtc,
    j.LastStageAutomationReason,
    j.ActiveTimerDueAtUtc
FROM dbo.{TablePrefix}journey_executions j
INNER JOIN dbo.{TablePrefix}kanban_leads l ON l.Id = j.LeadId AND l.IsActive = 1
INNER JOIN dbo.{TablePrefix}kanban_stages s ON s.Id = l.StageId
WHERE j.CreatedAt >= @createdFromUtc
  AND j.CreatedAt < @createdToUtcExclusive
  AND (@boardType IS NULL OR j.BoardType = @boardType)
  AND (@sourceChannel IS NULL OR j.SourceChannel = @sourceChannel);

SELECT
    COUNT(1) AS TotalJourneys,
    SUM(CASE WHEN CurrentState IN ('agendamento_confirmado', 'em_matching', 'disparo_prestadores', 'aguardando_aceite', 'prestador_conectado', 'servico_em_andamento', 'aguardando_confirmacao_conclusao', 'aguardando_avaliacao_cliente', 'aguardando_avaliacao_prestador', 'concluido') THEN 1 ELSE 0 END) AS ScheduledJourneys,
    SUM(CASE WHEN CurrentState = 'prestador_conectado' THEN 1 ELSE 0 END) AS ProviderConnectedJourneys,
    SUM(CASE WHEN CurrentState = 'concluido' THEN 1 ELSE 0 END) AS CompletedJourneys,
    SUM(CASE WHEN ClosureStatus = 'concluido' THEN 1 ELSE 0 END) AS ReviewCompletedJourneys,
    SUM(CASE WHEN CurrentState = 'excecao_operacional' THEN 1 ELSE 0 END) AS OperationalExceptionJourneys,
    SUM(CASE WHEN CurrentState = 'sem_match' THEN 1 ELSE 0 END) AS NoMatchJourneys,
    SUM(CASE WHEN SchedulingStatus IN ('sem_disponibilidade', 'cancelado') THEN 1 ELSE 0 END) AS ScheduleFailureJourneys,
    SUM(CASE WHEN DispatchStatus IN ('esgotado', 'cancelado') THEN 1 ELSE 0 END) AS DispatchFailureJourneys,
    SUM(CASE WHEN ClosureStatus = 'contestado' THEN 1 ELSE 0 END) AS ContestationJourneys,
    SUM(CASE WHEN ActiveTimerDueAtUtc IS NOT NULL AND ActiveTimerDueAtUtc <= SYSUTCDATETIME() THEN 1 ELSE 0 END) AS ActiveTimerOverdueJourneys,
    CAST(AVG(CASE WHEN SchedulingConfirmedAtUtc IS NOT NULL THEN CAST(DATEDIFF(MINUTE, CreatedAt, SchedulingConfirmedAtUtc) AS DECIMAL(18,2)) / 60.0 END) AS DECIMAL(18,2)) AS AverageHoursToSchedule,
    CAST(AVG(CASE WHEN DispatchReservedAtUtc IS NOT NULL THEN CAST(DATEDIFF(MINUTE, CreatedAt, DispatchReservedAtUtc) AS DECIMAL(18,2)) / 60.0 END) AS DECIMAL(18,2)) AS AverageHoursToReserve,
    CAST(AVG(CASE WHEN ClosureCompletedAtUtc IS NOT NULL THEN CAST(DATEDIFF(MINUTE, CreatedAt, ClosureCompletedAtUtc) AS DECIMAL(18,2)) / 60.0 END) AS DECIMAL(18,2)) AS AverageHoursToComplete
FROM @scoped;

SELECT
    SourceChannel,
    COUNT(1) AS TotalJourneys,
    SUM(CASE WHEN CurrentState = 'concluido' THEN 1 ELSE 0 END) AS CompletedJourneys,
    SUM(CASE WHEN CurrentState = 'excecao_operacional' THEN 1 ELSE 0 END) AS OperationalExceptionJourneys,
    SUM(CASE WHEN CurrentState = 'sem_match' THEN 1 ELSE 0 END) AS NoMatchJourneys
FROM @scoped
GROUP BY SourceChannel
ORDER BY COUNT(1) DESC, SourceChannel;

SELECT
    CurrentState,
    COUNT(1) AS TotalJourneys,
    SUM(CASE WHEN CurrentState = 'concluido' THEN 1 ELSE 0 END) AS CompletedJourneys,
    SUM(CASE WHEN CurrentState = 'excecao_operacional' THEN 1 ELSE 0 END) AS OperationalExceptionJourneys,
    SUM(CASE WHEN CurrentState = 'sem_match' THEN 1 ELSE 0 END) AS NoMatchJourneys,
    CAST(AVG(CAST(DATEDIFF(MINUTE, CreatedAt, SYSUTCDATETIME()) AS DECIMAL(18,2)) / 60.0) AS DECIMAL(18,2)) AS AverageJourneyAgeHours
FROM @scoped
GROUP BY CurrentState
ORDER BY COUNT(1) DESC, CurrentState;

SELECT TOP (@breakdownLimit)
    COALESCE(ServiceCategory, 'Nao informada') AS Category,
    COUNT(1) AS TotalJourneys,
    SUM(CASE WHEN CurrentState = 'concluido' THEN 1 ELSE 0 END) AS CompletedJourneys,
    SUM(CASE WHEN CurrentState = 'sem_match' THEN 1 ELSE 0 END) AS NoMatchJourneys,
    SUM(CASE WHEN CurrentState = 'excecao_operacional' THEN 1 ELSE 0 END) AS OperationalExceptionJourneys
FROM @scoped
GROUP BY COALESCE(ServiceCategory, 'Nao informada')
ORDER BY COUNT(1) DESC, Category;

SELECT TOP (@breakdownLimit)
    COALESCE(City, 'Nao informada') AS City,
    COUNT(1) AS TotalJourneys,
    SUM(CASE WHEN CurrentState = 'concluido' THEN 1 ELSE 0 END) AS CompletedJourneys,
    SUM(CASE WHEN CurrentState = 'sem_match' THEN 1 ELSE 0 END) AS NoMatchJourneys,
    SUM(CASE WHEN CurrentState = 'excecao_operacional' THEN 1 ELSE 0 END) AS OperationalExceptionJourneys
FROM @scoped
GROUP BY COALESCE(City, 'Nao informada')
ORDER BY COUNT(1) DESC, City;

SELECT TOP (@breakdownLimit)
    COALESCE(NULLIF(LTRIM(RTRIM(LastStageAutomationReason)), ''), 'Sem motivo registrado') AS Reason,
    COUNT(1) AS TotalJourneys
FROM @scoped
WHERE CurrentState = 'excecao_operacional'
GROUP BY COALESCE(NULLIF(LTRIM(RTRIM(LastStageAutomationReason)), ''), 'Sem motivo registrado')
ORDER BY COUNT(1) DESC, Reason;

SELECT
    COALESCE(DispatchCurrentWaveNumber, 0) AS WaveNumber,
    COUNT(1) AS TotalJourneys,
    SUM(CASE WHEN DispatchReservedProviderId IS NOT NULL THEN 1 ELSE 0 END) AS ReservedJourneys,
    SUM(CASE WHEN CurrentState = 'concluido' THEN 1 ELSE 0 END) AS CompletedJourneys,
    SUM(CASE WHEN CurrentState = 'excecao_operacional' THEN 1 ELSE 0 END) AS OperationalExceptionJourneys
FROM @scoped
WHERE COALESCE(DispatchCurrentWaveNumber, 0) > 0
GROUP BY COALESCE(DispatchCurrentWaveNumber, 0)
ORDER BY WaveNumber;

SELECT TOP (@breakdownLimit)
    BoardType,
    StageName,
    COUNT(1) AS TotalLeads,
    SUM(CASE WHEN ActiveTimerDueAtUtc IS NOT NULL AND ActiveTimerDueAtUtc <= SYSUTCDATETIME() THEN 1 ELSE 0 END) AS OverdueTimerJourneys,
    SUM(CASE WHEN CurrentState = 'excecao_operacional' THEN 1 ELSE 0 END) AS OperationalExceptionJourneys,
    SUM(CASE WHEN CurrentState = 'sem_match' THEN 1 ELSE 0 END) AS NoMatchJourneys,
    CAST(AVG(CAST(DATEDIFF(MINUTE, CreatedAt, SYSUTCDATETIME()) AS DECIMAL(18,2)) / 60.0) AS DECIMAL(18,2)) AS AverageLeadAgeHours
FROM @scoped
GROUP BY BoardType, StageName
ORDER BY COUNT(1) DESC, StageName;
""";
        command.Parameters.AddRange(
        [
            new SqlParameter("@createdFromUtc", SqlDbType.DateTime2) { Value = filter.CreatedFromUtc },
            new SqlParameter("@createdToUtcExclusive", SqlDbType.DateTime2) { Value = filter.CreatedToUtcExclusive },
            new SqlParameter("@boardType", SqlDbType.NVarChar, 30) { Value = ToDbValue(normalizedBoardType) },
            new SqlParameter("@sourceChannel", SqlDbType.NVarChar, 40) { Value = ToDbValue(normalizedSourceChannel) },
            new SqlParameter("@breakdownLimit", SqlDbType.Int) { Value = normalizedBreakdownLimit }
        ]);

        var snapshot = new AdminKanbanJourneyOperationsDashboardSnapshot
        {
            ScopeBoardType = normalizedBoardType ?? string.Empty,
            ScopeSourceChannel = normalizedSourceChannel ?? string.Empty,
            CreatedFromUtc = filter.CreatedFromUtc,
            CreatedToUtcExclusive = filter.CreatedToUtcExclusive
        };

        using var reader = command.ExecuteReader();
        if (reader.Read())
        {
            snapshot = snapshot with
            {
                TotalJourneys = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                ScheduledJourneys = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                ProviderConnectedJourneys = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                CompletedJourneys = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                ReviewCompletedJourneys = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                OperationalExceptionJourneys = reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
                NoMatchJourneys = reader.IsDBNull(6) ? 0 : reader.GetInt32(6),
                ScheduleFailureJourneys = reader.IsDBNull(7) ? 0 : reader.GetInt32(7),
                DispatchFailureJourneys = reader.IsDBNull(8) ? 0 : reader.GetInt32(8),
                ContestationJourneys = reader.IsDBNull(9) ? 0 : reader.GetInt32(9),
                ActiveTimerOverdueJourneys = reader.IsDBNull(10) ? 0 : reader.GetInt32(10),
                AverageHoursToSchedule = reader.IsDBNull(11) ? null : reader.GetDecimal(11),
                AverageHoursToReserve = reader.IsDBNull(12) ? null : reader.GetDecimal(12),
                AverageHoursToComplete = reader.IsDBNull(13) ? null : reader.GetDecimal(13)
            };
        }

        var sourceBreakdown = new List<AdminKanbanJourneyOperationsSourceBreakdownRecord>();
        if (reader.NextResult())
        {
            while (reader.Read())
            {
                sourceBreakdown.Add(new AdminKanbanJourneyOperationsSourceBreakdownRecord
                {
                    SourceChannel = reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                    TotalJourneys = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                    CompletedJourneys = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                    OperationalExceptionJourneys = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                    NoMatchJourneys = reader.IsDBNull(4) ? 0 : reader.GetInt32(4)
                });
            }
        }

        var stateBreakdown = new List<AdminKanbanJourneyOperationsStateBreakdownRecord>();
        if (reader.NextResult())
        {
            while (reader.Read())
            {
                stateBreakdown.Add(new AdminKanbanJourneyOperationsStateBreakdownRecord
                {
                    CurrentState = reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                    TotalJourneys = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                    CompletedJourneys = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                    OperationalExceptionJourneys = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                    NoMatchJourneys = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                    AverageJourneyAgeHours = reader.IsDBNull(5) ? null : reader.GetDecimal(5)
                });
            }
        }

        var categories = new List<AdminKanbanJourneyOperationsCategoryBreakdownRecord>();
        if (reader.NextResult())
        {
            while (reader.Read())
            {
                categories.Add(new AdminKanbanJourneyOperationsCategoryBreakdownRecord
                {
                    Category = reader.IsDBNull(0) ? "Nao informada" : reader.GetString(0),
                    TotalJourneys = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                    CompletedJourneys = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                    NoMatchJourneys = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                    OperationalExceptionJourneys = reader.IsDBNull(4) ? 0 : reader.GetInt32(4)
                });
            }
        }

        var cities = new List<AdminKanbanJourneyOperationsCityBreakdownRecord>();
        if (reader.NextResult())
        {
            while (reader.Read())
            {
                cities.Add(new AdminKanbanJourneyOperationsCityBreakdownRecord
                {
                    City = reader.IsDBNull(0) ? "Nao informada" : reader.GetString(0),
                    TotalJourneys = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                    CompletedJourneys = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                    NoMatchJourneys = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                    OperationalExceptionJourneys = reader.IsDBNull(4) ? 0 : reader.GetInt32(4)
                });
            }
        }

        var exceptionReasons = new List<AdminKanbanJourneyOperationsExceptionReasonRecord>();
        if (reader.NextResult())
        {
            while (reader.Read())
            {
                exceptionReasons.Add(new AdminKanbanJourneyOperationsExceptionReasonRecord
                {
                    Reason = reader.IsDBNull(0) ? "Sem motivo registrado" : reader.GetString(0),
                    TotalJourneys = reader.IsDBNull(1) ? 0 : reader.GetInt32(1)
                });
            }
        }

        var waveBreakdown = new List<AdminKanbanJourneyOperationsWaveBreakdownRecord>();
        if (reader.NextResult())
        {
            while (reader.Read())
            {
                waveBreakdown.Add(new AdminKanbanJourneyOperationsWaveBreakdownRecord
                {
                    WaveNumber = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                    TotalJourneys = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                    ReservedJourneys = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                    CompletedJourneys = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                    OperationalExceptionJourneys = reader.IsDBNull(4) ? 0 : reader.GetInt32(4)
                });
            }
        }

        var stageBacklog = new List<AdminKanbanJourneyOperationsStageBacklogRecord>();
        if (reader.NextResult())
        {
            while (reader.Read())
            {
                stageBacklog.Add(new AdminKanbanJourneyOperationsStageBacklogRecord
                {
                    BoardType = reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                    StageName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    TotalLeads = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                    OverdueTimerJourneys = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                    OperationalExceptionJourneys = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                    NoMatchJourneys = reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
                    AverageLeadAgeHours = reader.IsDBNull(6) ? null : reader.GetDecimal(6)
                });
            }
        }

        return snapshot with
        {
            SourceBreakdown = sourceBreakdown,
            StateBreakdown = stateBreakdown,
            TopCategories = categories,
            TopCities = cities,
            ExceptionReasons = exceptionReasons,
            WaveBreakdown = waveBreakdown,
            StageBacklog = stageBacklog
        };
    }

    public AdminKanbanChatwootDiagnosticsSnapshot GetChatwootDiagnostics(string? boardType, int issueLimit, int queueLimit)
    {
        EnsureInitialized();

        var normalizedBoardType = string.IsNullOrWhiteSpace(boardType)
            ? null
            : AdminKanbanBoardTypes.Normalize(boardType);
        var normalizedIssueLimit = Math.Clamp(issueLimit, 1, 100);
        var normalizedQueueLimit = Math.Clamp(queueLimit, 1, 100);

        using var connection = OpenConnection();
        var snapshot = new AdminKanbanChatwootDiagnosticsSnapshot
        {
            ScopeBoardType = normalizedBoardType ?? string.Empty,
            RecentIssues = [],
            RecentQueueItems = []
        };

        using (var summaryCommand = connection.CreateCommand())
        {
            summaryCommand.CommandText = $"""
SELECT
    COUNT(1) AS TotalLeads,
    SUM(CASE WHEN LOWER(ISNULL(ChatwootSyncStatus, '')) = 'synced' THEN 1 ELSE 0 END) AS SyncedCount,
    SUM(CASE WHEN LOWER(ISNULL(ChatwootSyncStatus, '')) = 'failed' THEN 1 ELSE 0 END) AS FailedCount,
    SUM(CASE WHEN LOWER(ISNULL(ChatwootSyncStatus, '')) IN ('', 'pending', 'skipped', 'disabled', 'not_found') THEN 1 ELSE 0 END) AS PendingCount
FROM dbo.{TablePrefix}kanban_leads
WHERE IsActive = 1
  AND (@boardType IS NULL OR BoardType = @boardType);
""";
            summaryCommand.Parameters.Add(new SqlParameter("@boardType", SqlDbType.NVarChar, 30) { Value = ToDbValue(normalizedBoardType) });

            using var reader = summaryCommand.ExecuteReader();
            if (reader.Read())
            {
                snapshot = snapshot with
                {
                    TotalLeads = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                    SyncedCount = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                    FailedCount = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                    PendingCount = reader.IsDBNull(3) ? 0 : reader.GetInt32(3)
                };
            }
        }

        using (var queueSummaryCommand = connection.CreateCommand())
        {
            queueSummaryCommand.CommandText = $"""
SELECT
    SUM(CASE WHEN q.Status IN ('queued', 'retrying', 'processing') THEN 1 ELSE 0 END) AS ActiveQueueCount,
    SUM(CASE WHEN q.Status = 'dead_letter' THEN 1 ELSE 0 END) AS DeadLetterCount
FROM dbo.{TablePrefix}chatwoot_sync_queue q
INNER JOIN dbo.{TablePrefix}kanban_leads l ON l.Id = q.LeadId
WHERE l.IsActive = 1
  AND (@boardType IS NULL OR l.BoardType = @boardType);
""";
            queueSummaryCommand.Parameters.Add(new SqlParameter("@boardType", SqlDbType.NVarChar, 30) { Value = ToDbValue(normalizedBoardType) });

            using var reader = queueSummaryCommand.ExecuteReader();
            if (reader.Read())
            {
                snapshot = snapshot with
                {
                    ActiveQueueCount = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                    DeadLetterCount = reader.IsDBNull(1) ? 0 : reader.GetInt32(1)
                };
            }
        }

        var issues = new List<AdminKanbanChatwootSyncIssueRecord>();
        using (var issuesCommand = connection.CreateCommand())
        {
            issuesCommand.CommandText = $"""
SELECT TOP (@issueLimit)
    l.Id,
    l.BoardType,
    s.Name,
    l.Name,
    l.ChatwootSyncStatus,
    l.ChatwootLastSyncAt,
    l.ChatwootLastError,
    l.ChatwootContactId,
    l.ChatwootConversationId,
    l.ChatwootInboxId
FROM dbo.{TablePrefix}kanban_leads l
INNER JOIN dbo.{TablePrefix}kanban_stages s ON s.Id = l.StageId
WHERE l.IsActive = 1
  AND (@boardType IS NULL OR l.BoardType = @boardType)
  AND (
        LOWER(ISNULL(l.ChatwootSyncStatus, '')) = 'failed'
        OR NULLIF(LTRIM(RTRIM(ISNULL(l.ChatwootLastError, ''))), '') IS NOT NULL
      )
ORDER BY COALESCE(l.ChatwootLastSyncAt, l.UpdatedAt, l.CreatedAt) DESC, l.Id DESC;
""";
            issuesCommand.Parameters.AddRange(
            [
                new SqlParameter("@issueLimit", SqlDbType.Int) { Value = normalizedIssueLimit },
                new SqlParameter("@boardType", SqlDbType.NVarChar, 30) { Value = ToDbValue(normalizedBoardType) }
            ]);

            using var reader = issuesCommand.ExecuteReader();
            while (reader.Read())
            {
                issues.Add(new AdminKanbanChatwootSyncIssueRecord
                {
                    LeadId = reader.GetInt32(0),
                    BoardType = reader.GetString(1),
                    StageName = reader.GetString(2),
                    LeadName = reader.GetString(3),
                    SyncStatus = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                    LastSyncAt = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                    LastError = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                    ContactId = ReadNullableInt64(reader, 7),
                    ConversationId = ReadNullableInt64(reader, 8),
                    InboxId = ReadNullableInt64(reader, 9)
                });
            }
        }

        var queueItems = new List<AdminKanbanChatwootQueueDiagnosticRecord>();
        using (var queueCommand = connection.CreateCommand())
        {
            queueCommand.CommandText = $"""
SELECT TOP (@queueLimit)
    q.Id,
    q.LeadId,
    l.BoardType,
    s.Name,
    l.Name,
    q.OperationType,
    q.Status,
    q.AttemptCount,
    q.MaxAttempts,
    q.NextAttemptAt,
    q.LastAttemptAt,
    q.LastError,
    l.ChatwootConversationId
FROM dbo.{TablePrefix}chatwoot_sync_queue q
INNER JOIN dbo.{TablePrefix}kanban_leads l ON l.Id = q.LeadId
INNER JOIN dbo.{TablePrefix}kanban_stages s ON s.Id = l.StageId
WHERE l.IsActive = 1
  AND (@boardType IS NULL OR l.BoardType = @boardType)
  AND q.Status IN ('queued', 'retrying', 'processing', 'dead_letter')
ORDER BY q.UpdatedAt DESC, q.Id DESC;
""";
            queueCommand.Parameters.AddRange(
            [
                new SqlParameter("@queueLimit", SqlDbType.Int) { Value = normalizedQueueLimit },
                new SqlParameter("@boardType", SqlDbType.NVarChar, 30) { Value = ToDbValue(normalizedBoardType) }
            ]);

            using var reader = queueCommand.ExecuteReader();
            while (reader.Read())
            {
                queueItems.Add(new AdminKanbanChatwootQueueDiagnosticRecord
                {
                    QueueItemId = reader.GetInt32(0),
                    LeadId = reader.GetInt32(1),
                    BoardType = reader.GetString(2),
                    StageName = reader.GetString(3),
                    LeadName = reader.GetString(4),
                    OperationType = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                    Status = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                    AttemptCount = reader.GetInt32(7),
                    MaxAttempts = reader.GetInt32(8),
                    NextAttemptAt = reader.GetDateTime(9),
                    LastAttemptAt = reader.IsDBNull(10) ? null : reader.GetDateTime(10),
                    LastError = reader.IsDBNull(11) ? string.Empty : reader.GetString(11),
                    ConversationId = ReadNullableInt64(reader, 12)
                });
            }
        }

        return snapshot with
        {
            RecentIssues = issues,
            RecentQueueItems = queueItems
        };
    }

    public bool SaveBoardOrder(AdminKanbanBoardOrderUpdateRequest request)
    {
        EnsureInitialized();
        var normalizedBoardType = AdminKanbanBoardTypes.Normalize(request.BoardType);

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        foreach (var stage in request.Stages)
        {
            for (var i = 0; i < stage.LeadIds.Count; i++)
            {
                using var updateCommand = connection.CreateCommand();
                updateCommand.Transaction = transaction;
                updateCommand.CommandText = $"""
UPDATE dbo.{TablePrefix}kanban_leads
SET StageId = @stageId,
    SortOrder = @sortOrder,
    UpdatedAt = SYSUTCDATETIME()
WHERE Id = @leadId AND IsActive = 1 AND BoardType = @boardType;
""";
                updateCommand.Parameters.AddRange(
                [
                    new SqlParameter("@stageId", SqlDbType.Int) { Value = stage.StageId },
                    new SqlParameter("@sortOrder", SqlDbType.Int) { Value = i + 1 },
                    new SqlParameter("@leadId", SqlDbType.Int) { Value = stage.LeadIds[i] },
                    new SqlParameter("@boardType", SqlDbType.NVarChar, 30) { Value = normalizedBoardType }
                ]);
                updateCommand.ExecuteNonQuery();
            }
        }

        if (request.ChangedLeadId.HasValue && request.FromStageId.HasValue && request.ToStageId.HasValue)
        {
            if (request.FromStageId.Value != request.ToStageId.Value)
            {
                InsertHistory(
                    connection,
                    transaction,
                    request.ChangedLeadId.Value,
                    eventType: "movido",
                    fromStageId: request.FromStageId.Value,
                    toStageId: request.ToStageId.Value,
                    description: "Lead movido por arrastar e soltar no kanban."
                );
            }
        }

        transaction.Commit();
        return true;
    }

    public bool AddHistoryNote(int leadId, string note)
    {
        EnsureInitialized();
        if (string.IsNullOrWhiteSpace(note))
        {
            return false;
        }

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        if (!ActiveLeadExists(connection, transaction, leadId))
        {
            return false;
        }

        InsertHistory(
            connection,
            transaction,
            leadId,
            eventType: "nota",
            fromStageId: null,
            toStageId: null,
            description: TrimTo(note, 3000)
        );

        transaction.Commit();
        return true;
    }

    public bool AddHistoryEvent(int leadId, string eventType, string description)
    {
        EnsureInitialized();
        if (string.IsNullOrWhiteSpace(eventType) || string.IsNullOrWhiteSpace(description))
        {
            return false;
        }

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        if (!ActiveLeadExists(connection, transaction, leadId))
        {
            return false;
        }

        InsertHistory(
            connection,
            transaction,
            leadId,
            eventType: TrimTo(eventType, 40),
            fromStageId: null,
            toStageId: null,
            description: TrimTo(description, 3000)
        );

        transaction.Commit();
        return true;
    }

    private SqlConnection OpenConnection()
    {
        var connection = new SqlConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }

        lock (_initLock)
        {
            if (_initialized)
            {
                return;
            }

            using var connection = OpenConnection();
            using var transaction = connection.BeginTransaction();

            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = $"""
IF OBJECT_ID('dbo.{TablePrefix}kanban_stages', 'U') IS NULL
CREATE TABLE dbo.{TablePrefix}kanban_stages
(
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    BoardType NVARCHAR(30) NOT NULL,
    Name NVARCHAR(120) NOT NULL,
    Color NVARCHAR(20) NULL,
    SortOrder INT NOT NULL DEFAULT(0),
    IsActive BIT NOT NULL DEFAULT(1),
    CreatedAt DATETIME2 NOT NULL DEFAULT(SYSUTCDATETIME()),
    UpdatedAt DATETIME2 NULL
);

IF OBJECT_ID('dbo.{TablePrefix}kanban_leads', 'U') IS NULL
CREATE TABLE dbo.{TablePrefix}kanban_leads
(
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    BoardType NVARCHAR(30) NOT NULL,
    StageId INT NOT NULL,
    SortOrder INT NOT NULL DEFAULT(0),
    Name NVARCHAR(140) NOT NULL,
    Phone NVARCHAR(30) NULL,
    Email NVARCHAR(180) NULL,
    ServiceCategory NVARCHAR(140) NULL,
    PostalCode NVARCHAR(9) NULL,
    City NVARCHAR(120) NULL,
    Source NVARCHAR(120) NULL,
    Priority NVARCHAR(20) NOT NULL DEFAULT('normal'),
    StatusNote NVARCHAR(500) NULL,
    InternalNotes NVARCHAR(MAX) NULL,
    LastContactAt DATETIME2 NULL,
    ChatwootContactId BIGINT NULL,
    ChatwootConversationId BIGINT NULL,
    ChatwootInboxId BIGINT NULL,
    ChatwootSyncStatus NVARCHAR(30) NULL,
    ChatwootLastSyncAt DATETIME2 NULL,
    ChatwootLastError NVARCHAR(MAX) NULL,
    IsActive BIT NOT NULL DEFAULT(1),
    CreatedAt DATETIME2 NOT NULL DEFAULT(SYSUTCDATETIME()),
    UpdatedAt DATETIME2 NULL
);

IF OBJECT_ID('dbo.{TablePrefix}kanban_lead_history', 'U') IS NULL
CREATE TABLE dbo.{TablePrefix}kanban_lead_history
(
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    LeadId INT NOT NULL,
    EventType NVARCHAR(40) NOT NULL,
    FromStageId INT NULL,
    ToStageId INT NULL,
    Description NVARCHAR(MAX) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT(SYSUTCDATETIME())
);

IF OBJECT_ID('dbo.{TablePrefix}chatwoot_webhook_events', 'U') IS NULL
CREATE TABLE dbo.{TablePrefix}chatwoot_webhook_events
(
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    ProviderEventId NVARCHAR(120) NULL,
    EventType NVARCHAR(80) NOT NULL,
    ConversationId BIGINT NULL,
    PayloadJson NVARCHAR(MAX) NOT NULL,
    Signature NVARCHAR(255) NULL,
    PayloadPurgedAt DATETIME2 NULL,
    ReceivedAt DATETIME2 NOT NULL DEFAULT(SYSUTCDATETIME()),
    ProcessedAt DATETIME2 NULL,
    ProcessStatus NVARCHAR(30) NOT NULL DEFAULT('received'),
    ErrorMessage NVARCHAR(MAX) NULL
);

IF OBJECT_ID('dbo.{TablePrefix}chatwoot_sync_queue', 'U') IS NULL
CREATE TABLE dbo.{TablePrefix}chatwoot_sync_queue
(
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    LeadId INT NOT NULL,
    OperationType NVARCHAR(40) NOT NULL,
    Status NVARCHAR(30) NOT NULL DEFAULT('queued'),
    AttemptCount INT NOT NULL DEFAULT(0),
    MaxAttempts INT NOT NULL DEFAULT(10),
    NextAttemptAt DATETIME2 NOT NULL DEFAULT(SYSUTCDATETIME()),
    LastAttemptAt DATETIME2 NULL,
    LastError NVARCHAR(MAX) NULL,
    WorkerInstance NVARCHAR(120) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT(SYSUTCDATETIME()),
    UpdatedAt DATETIME2 NOT NULL DEFAULT(SYSUTCDATETIME()),
    ProcessedAt DATETIME2 NULL,
    DeadLetterAt DATETIME2 NULL
);

IF OBJECT_ID('dbo.{TablePrefix}chatwoot_backfill_checkpoints', 'U') IS NULL
CREATE TABLE dbo.{TablePrefix}chatwoot_backfill_checkpoints
(
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    ScopeKey NVARCHAR(80) NOT NULL,
    LastProcessedLeadId INT NULL,
    LastRunStartedAt DATETIME2 NULL,
    LastRunCompletedAt DATETIME2 NULL,
    LastRunStatus NVARCHAR(30) NULL,
    LastSummaryJson NVARCHAR(MAX) NULL,
    UpdatedAt DATETIME2 NOT NULL DEFAULT(SYSUTCDATETIME())
);

IF OBJECT_ID('dbo.{TablePrefix}telegram_funil_links', 'U') IS NULL
CREATE TABLE dbo.{TablePrefix}telegram_funil_links
(
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    ChatbotConversationId UNIQUEIDENTIFIER NOT NULL,
    LeadId INT NOT NULL,
    BoardType NVARCHAR(30) NOT NULL,
    ChannelConversationId NVARCHAR(128) NOT NULL,
    TelegramChatId BIGINT NOT NULL,
    ClientId UNIQUEIDENTIFIER NULL,
    ClientPhone NVARCHAR(30) NULL,
    ClientEmail NVARCHAR(180) NULL,
    ServiceRequestId UNIQUEIDENTIFIER NULL,
    HumanHandoffStartedAt DATETIME2 NULL,
    HumanHandoffStatus NVARCHAR(40) NULL,
    HumanHandoffReason NVARCHAR(180) NULL,
    HumanHandoffUpdatedAt DATETIME2 NULL,
    LastTelegramMessageSyncedAt DATETIME2 NULL,
    LastChatwootMessageSyncedAt DATETIME2 NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT(SYSUTCDATETIME()),
    UpdatedAt DATETIME2 NOT NULL DEFAULT(SYSUTCDATETIME())
);

IF OBJECT_ID('dbo.{TablePrefix}telegram_delivery_queue', 'U') IS NULL
CREATE TABLE dbo.{TablePrefix}telegram_delivery_queue
(
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    LeadId INT NOT NULL,
    Direction NVARCHAR(40) NOT NULL,
    DeliveryKey NVARCHAR(180) NOT NULL,
    PayloadJson NVARCHAR(MAX) NOT NULL,
    ChatwootConversationId BIGINT NULL,
    TelegramChatId BIGINT NULL,
    Status NVARCHAR(30) NOT NULL DEFAULT('queued'),
    AttemptCount INT NOT NULL DEFAULT(0),
    MaxAttempts INT NOT NULL DEFAULT(10),
    NextAttemptAt DATETIME2 NOT NULL DEFAULT(SYSUTCDATETIME()),
    LastAttemptAt DATETIME2 NULL,
    LastError NVARCHAR(MAX) NULL,
    WorkerInstance NVARCHAR(120) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT(SYSUTCDATETIME()),
    UpdatedAt DATETIME2 NOT NULL DEFAULT(SYSUTCDATETIME()),
    ProcessedAt DATETIME2 NULL,
    DeadLetterAt DATETIME2 NULL,
    PayloadPurgedAt DATETIME2 NULL
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.{TablePrefix}kanban_stages') AND name = 'IX_{TablePrefix}kanban_stages_board')
CREATE INDEX IX_{TablePrefix}kanban_stages_board
    ON dbo.{TablePrefix}kanban_stages(BoardType, SortOrder, Id);

IF COL_LENGTH('dbo.{TablePrefix}kanban_leads', 'ChatwootContactId') IS NULL
ALTER TABLE dbo.{TablePrefix}kanban_leads ADD ChatwootContactId BIGINT NULL;

IF COL_LENGTH('dbo.{TablePrefix}kanban_leads', 'ChatwootConversationId') IS NULL
ALTER TABLE dbo.{TablePrefix}kanban_leads ADD ChatwootConversationId BIGINT NULL;

IF COL_LENGTH('dbo.{TablePrefix}kanban_leads', 'ChatwootInboxId') IS NULL
ALTER TABLE dbo.{TablePrefix}kanban_leads ADD ChatwootInboxId BIGINT NULL;

IF COL_LENGTH('dbo.{TablePrefix}kanban_leads', 'ChatwootSyncStatus') IS NULL
ALTER TABLE dbo.{TablePrefix}kanban_leads ADD ChatwootSyncStatus NVARCHAR(30) NULL;

IF COL_LENGTH('dbo.{TablePrefix}kanban_leads', 'ChatwootLastSyncAt') IS NULL
ALTER TABLE dbo.{TablePrefix}kanban_leads ADD ChatwootLastSyncAt DATETIME2 NULL;

IF COL_LENGTH('dbo.{TablePrefix}kanban_leads', 'ChatwootLastError') IS NULL
ALTER TABLE dbo.{TablePrefix}kanban_leads ADD ChatwootLastError NVARCHAR(MAX) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.{TablePrefix}kanban_leads') AND name = 'IX_{TablePrefix}kanban_leads_board_stage')
CREATE INDEX IX_{TablePrefix}kanban_leads_board_stage
    ON dbo.{TablePrefix}kanban_leads(BoardType, StageId, SortOrder, Id);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.{TablePrefix}kanban_leads') AND name = 'IX_{TablePrefix}kanban_leads_chatwoot_conversation')
CREATE INDEX IX_{TablePrefix}kanban_leads_chatwoot_conversation
    ON dbo.{TablePrefix}kanban_leads(ChatwootConversationId);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.{TablePrefix}kanban_lead_history') AND name = 'IX_{TablePrefix}kanban_history_lead')
CREATE INDEX IX_{TablePrefix}kanban_history_lead
    ON dbo.{TablePrefix}kanban_lead_history(LeadId, CreatedAt DESC, Id DESC);

IF COL_LENGTH('dbo.{TablePrefix}chatwoot_webhook_events', 'ConversationId') IS NULL
ALTER TABLE dbo.{TablePrefix}chatwoot_webhook_events ADD ConversationId BIGINT NULL;

IF COL_LENGTH('dbo.{TablePrefix}chatwoot_webhook_events', 'PayloadPurgedAt') IS NULL
ALTER TABLE dbo.{TablePrefix}chatwoot_webhook_events ADD PayloadPurgedAt DATETIME2 NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.{TablePrefix}chatwoot_webhook_events') AND name = 'IX_{TablePrefix}chatwoot_webhook_events_provider_event')
CREATE UNIQUE INDEX IX_{TablePrefix}chatwoot_webhook_events_provider_event
    ON dbo.{TablePrefix}chatwoot_webhook_events(ProviderEventId)
    WHERE ProviderEventId IS NOT NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.{TablePrefix}chatwoot_webhook_events') AND name = 'IX_{TablePrefix}chatwoot_webhook_events_conversation')
CREATE INDEX IX_{TablePrefix}chatwoot_webhook_events_conversation
    ON dbo.{TablePrefix}chatwoot_webhook_events(ConversationId, ReceivedAt DESC, Id DESC);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.{TablePrefix}chatwoot_sync_queue') AND name = 'IX_{TablePrefix}chatwoot_sync_queue_due')
CREATE INDEX IX_{TablePrefix}chatwoot_sync_queue_due
    ON dbo.{TablePrefix}chatwoot_sync_queue(Status, NextAttemptAt, Id);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.{TablePrefix}chatwoot_sync_queue') AND name = 'UX_{TablePrefix}chatwoot_sync_queue_active')
CREATE UNIQUE INDEX UX_{TablePrefix}chatwoot_sync_queue_active
    ON dbo.{TablePrefix}chatwoot_sync_queue(LeadId, OperationType)
    WHERE Status IN ('queued', 'retrying', 'processing');

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.{TablePrefix}chatwoot_backfill_checkpoints') AND name = 'UX_{TablePrefix}chatwoot_backfill_checkpoints_scope')
CREATE UNIQUE INDEX UX_{TablePrefix}chatwoot_backfill_checkpoints_scope
    ON dbo.{TablePrefix}chatwoot_backfill_checkpoints(ScopeKey);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.{TablePrefix}telegram_funil_links') AND name = 'UX_{TablePrefix}telegram_funil_links_conversation')
CREATE UNIQUE INDEX UX_{TablePrefix}telegram_funil_links_conversation
    ON dbo.{TablePrefix}telegram_funil_links(ChatbotConversationId);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.{TablePrefix}telegram_funil_links') AND name = 'IX_{TablePrefix}telegram_funil_links_lead')
CREATE INDEX IX_{TablePrefix}telegram_funil_links_lead
    ON dbo.{TablePrefix}telegram_funil_links(LeadId, UpdatedAt DESC, Id DESC);

IF COL_LENGTH('dbo.{TablePrefix}telegram_funil_links', 'HumanHandoffStartedAt') IS NULL
ALTER TABLE dbo.{TablePrefix}telegram_funil_links ADD HumanHandoffStartedAt DATETIME2 NULL;

IF COL_LENGTH('dbo.{TablePrefix}telegram_funil_links', 'HumanHandoffStatus') IS NULL
ALTER TABLE dbo.{TablePrefix}telegram_funil_links ADD HumanHandoffStatus NVARCHAR(40) NULL;

IF COL_LENGTH('dbo.{TablePrefix}telegram_funil_links', 'HumanHandoffReason') IS NULL
ALTER TABLE dbo.{TablePrefix}telegram_funil_links ADD HumanHandoffReason NVARCHAR(180) NULL;

IF COL_LENGTH('dbo.{TablePrefix}telegram_funil_links', 'HumanHandoffUpdatedAt') IS NULL
ALTER TABLE dbo.{TablePrefix}telegram_funil_links ADD HumanHandoffUpdatedAt DATETIME2 NULL;

IF COL_LENGTH('dbo.{TablePrefix}telegram_funil_links', 'ClientPhone') IS NULL
ALTER TABLE dbo.{TablePrefix}telegram_funil_links ADD ClientPhone NVARCHAR(30) NULL;

IF COL_LENGTH('dbo.{TablePrefix}telegram_funil_links', 'LastTelegramMessageSyncedAt') IS NULL
ALTER TABLE dbo.{TablePrefix}telegram_funil_links ADD LastTelegramMessageSyncedAt DATETIME2 NULL;

IF COL_LENGTH('dbo.{TablePrefix}telegram_funil_links', 'LastChatwootMessageSyncedAt') IS NULL
ALTER TABLE dbo.{TablePrefix}telegram_funil_links ADD LastChatwootMessageSyncedAt DATETIME2 NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.{TablePrefix}telegram_funil_links') AND name = 'IX_{TablePrefix}telegram_funil_links_chat')
CREATE INDEX IX_{TablePrefix}telegram_funil_links_chat
    ON dbo.{TablePrefix}telegram_funil_links(TelegramChatId, UpdatedAt DESC, Id DESC);

IF COL_LENGTH('dbo.{TablePrefix}telegram_delivery_queue', 'PayloadPurgedAt') IS NULL
ALTER TABLE dbo.{TablePrefix}telegram_delivery_queue ADD PayloadPurgedAt DATETIME2 NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.{TablePrefix}telegram_delivery_queue') AND name = 'UX_{TablePrefix}telegram_delivery_queue_key')
CREATE UNIQUE INDEX UX_{TablePrefix}telegram_delivery_queue_key
    ON dbo.{TablePrefix}telegram_delivery_queue(Direction, DeliveryKey);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.{TablePrefix}telegram_delivery_queue') AND name = 'IX_{TablePrefix}telegram_delivery_queue_due')
CREATE INDEX IX_{TablePrefix}telegram_delivery_queue_due
    ON dbo.{TablePrefix}telegram_delivery_queue(Status, NextAttemptAt, Id);
""";
                command.ExecuteNonQuery();
            }

            SeedStages(connection, transaction, AdminKanbanBoardTypes.Clients, ClientJourneyStages);
            SeedStages(connection, transaction, AdminKanbanBoardTypes.Providers, ProviderDefaultStages);
            MigrateLegacyClientStagesToJourneyFlow(connection, transaction);
            SeedSampleLeads(connection, transaction);

            transaction.Commit();
            _initialized = true;
        }
    }

    private static void SeedStages(
        SqlConnection connection,
        SqlTransaction transaction,
        string boardType,
        IReadOnlyList<(string Name, string Color)> stages)
    {
        for (var i = 0; i < stages.Count; i++)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = $"""
IF NOT EXISTS (
    SELECT 1
    FROM dbo.{TablePrefix}kanban_stages
    WHERE BoardType = @boardType AND Name = @name
)
BEGIN
    INSERT INTO dbo.{TablePrefix}kanban_stages (BoardType, Name, Color, SortOrder, IsActive)
    VALUES (@boardType, @name, @color, @sortOrder, 1);
END;
ELSE
BEGIN
    UPDATE dbo.{TablePrefix}kanban_stages
    SET Color = @color,
        SortOrder = @sortOrder,
        IsActive = 1,
        UpdatedAt = SYSUTCDATETIME()
    WHERE BoardType = @boardType
      AND Name = @name;
END;
""";
            command.Parameters.AddRange(
            [
                new SqlParameter("@boardType", SqlDbType.NVarChar, 30) { Value = boardType },
                new SqlParameter("@name", SqlDbType.NVarChar, 120) { Value = stages[i].Name },
                new SqlParameter("@color", SqlDbType.NVarChar, 20) { Value = stages[i].Color },
                new SqlParameter("@sortOrder", SqlDbType.Int) { Value = i + 1 }
            ]);
            command.ExecuteNonQuery();
        }
    }

    private static void MigrateLegacyClientStagesToJourneyFlow(SqlConnection connection, SqlTransaction transaction)
    {
        RenameLegacyClientStageIfNeeded(connection, transaction, "Tentativa de contato", AdminKanbanJourneyClientStageNames.AutomatedTriage);
        RenameLegacyClientStageIfNeeded(connection, transaction, "Agendado", AdminKanbanJourneyClientStageNames.AppointmentConfirmed);
        RenameLegacyClientStageIfNeeded(connection, transaction, "Em atendimento", AdminKanbanJourneyClientStageNames.ServiceInProgress);
        RenameLegacyClientStageIfNeeded(connection, transaction, "Perdido", AdminKanbanJourneyClientStageNames.OperationalException);
    }

    private static void RenameLegacyClientStageIfNeeded(SqlConnection connection, SqlTransaction transaction, string legacyStageName, string targetStageName)
    {
        if (string.Equals(legacyStageName, targetStageName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
IF EXISTS (
    SELECT 1
    FROM dbo.{TablePrefix}kanban_stages
    WHERE BoardType = @boardType
      AND Name = @legacyStageName
      AND IsActive = 1
)
AND NOT EXISTS (
    SELECT 1
    FROM dbo.{TablePrefix}kanban_stages
    WHERE BoardType = @boardType
      AND Name = @targetStageName
      AND IsActive = 1
)
BEGIN
    UPDATE dbo.{TablePrefix}kanban_stages
    SET Name = @targetStageName,
        UpdatedAt = SYSUTCDATETIME()
    WHERE BoardType = @boardType
      AND Name = @legacyStageName
      AND IsActive = 1;
END;
""";
        command.Parameters.AddRange(
        [
            new SqlParameter("@boardType", SqlDbType.NVarChar, 30) { Value = AdminKanbanBoardTypes.Clients },
            new SqlParameter("@legacyStageName", SqlDbType.NVarChar, 120) { Value = legacyStageName },
            new SqlParameter("@targetStageName", SqlDbType.NVarChar, 120) { Value = targetStageName }
        ]);
        command.ExecuteNonQuery();
    }

    private static void SeedSampleLeads(SqlConnection connection, SqlTransaction transaction)
    {
        SeedClientExamples(connection, transaction);
        SeedProviderExamples(connection, transaction);
        SyncActiveProfessionalsToProviderKanban(connection, transaction);
    }

    private static void SeedClientExamples(SqlConnection connection, SqlTransaction transaction)
    {
        var boardType = AdminKanbanBoardTypes.Clients;
        if (!HasAnyLead(connection, transaction, boardType))
        {
            var novoLeadStageId = GetStageIdByName(connection, transaction, boardType, "Novo lead");
            var triagemStageId = GetStageIdByName(connection, transaction, boardType, "Triagem automatica");
            var agendadoStageId = GetStageIdByName(connection, transaction, boardType, "Agendamento confirmado");
            var emAtendimentoStageId = GetStageIdByName(connection, transaction, boardType, "Servico em andamento");
            var concluidoStageId = GetStageIdByName(connection, transaction, boardType, "Concluido");
            var excecaoStageId = GetStageIdByName(connection, transaction, boardType, "Excecao operacional");

            UpsertSeedLeadBySource(connection, transaction, boardType, "seed.cliente.1", novoLeadStageId, "Mariana Souza", "(13) 99877-1100", "mariana@email.com", "Encanador", "Padrao", "Vazamento na pia da cozinha", null, "11700-130", "Praia Grande");
            UpsertSeedLeadBySource(connection, transaction, boardType, "seed.cliente.2", triagemStageId, "Ricardo Almeida", "(13) 99711-4422", "ricardo@email.com", "Eletricista", "WhatsApp", "Aguardando retorno do cliente", DateTime.UtcNow.AddHours(-5), "11701-200", "Praia Grande");
            UpsertSeedLeadBySource(connection, transaction, boardType, "seed.cliente.3", agendadoStageId, "Carla Nunes", "(13) 99655-8822", "carla@email.com", "Ar-condicionado", "Formulario", "Visita agendada para amanha 14h", DateTime.UtcNow.AddHours(-2), "11702-330", "Praia Grande");
            UpsertSeedLeadBySource(connection, transaction, boardType, "seed.cliente.4", emAtendimentoStageId, "Fernando Lima", "(13) 99122-7600", "fernando@email.com", "Pedreiro", "Indicacao", "Reforma em andamento", DateTime.UtcNow.AddDays(-1), "11703-040", "Sao Vicente");
            UpsertSeedLeadBySource(connection, transaction, boardType, "seed.cliente.5", concluidoStageId, "Luciana Prado", "(13) 99966-1200", "luciana@email.com", "Pintor", "Formulario", "Servico finalizado e cliente satisfeito", DateTime.UtcNow.AddDays(-2), "11704-900", "Praia Grande");
            UpsertSeedLeadBySource(connection, transaction, boardType, "seed.cliente.6", excecaoStageId, "Bruno Castro", "(13) 99221-4567", "bruno@email.com", "Chaveiro", "Ligacao", "Cliente fechou com concorrente", DateTime.UtcNow.AddDays(-3), "11705-010", "Praia Grande");
        }

        if (TableExists(connection, transaction, $"{TablePrefix}service_requests"))
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = $"""
SELECT TOP (30) Id, CategoryName, Description, Location, Name, Phone, SubmittedAt
FROM dbo.{TablePrefix}service_requests
ORDER BY Id DESC;
""";

            using var reader = command.ExecuteReader();
            var rows = new List<(int Id, string CategoryName, string Description, string Location, string Name, string Phone, DateTime SubmittedAt)>();
            while (reader.Read())
            {
                rows.Add((
                    reader.GetInt32(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetString(4),
                    reader.GetString(5),
                    ReadAsUtcDateTime(reader, 6)
                ));
            }
            reader.Close();

            var newLeadStageId = GetStageIdByName(connection, transaction, boardType, "Novo lead");
            foreach (var row in rows)
            {
                var source = $"Solicitacao site #{row.Id}";
                var status = row.Description.Length > 140 ? $"{row.Description[..140]}..." : row.Description;
                UpsertSeedLeadBySource(
                    connection,
                    transaction,
                    boardType,
                    source,
                    newLeadStageId,
                    row.Name,
                    row.Phone,
                    null,
                    row.CategoryName,
                    source,
                    status,
                    row.SubmittedAt,
                    null,
                    row.Location
                );
            }
        }
    }

    private static void SeedProviderExamples(SqlConnection connection, SqlTransaction transaction)
    {
        var boardType = AdminKanbanBoardTypes.Providers;
        if (!HasAnyLead(connection, transaction, boardType))
        {
            var novoCadastroStageId = GetStageIdByName(connection, transaction, boardType, "Novo cadastro");
            var primeiroContatoStageId = GetStageIdByName(connection, transaction, boardType, "Primeiro contato");
            var docPendenteStageId = GetStageIdByName(connection, transaction, boardType, "Documentacao pendente");
            var validacaoStageId = GetStageIdByName(connection, transaction, boardType, "Validacao tecnica");
            var inativoStageId = GetStageIdByName(connection, transaction, boardType, "Inativo/Recusado");

            UpsertSeedLeadBySource(connection, transaction, boardType, "seed.prestador.1", novoCadastroStageId, "Paulo Mendes", "(13) 99880-2200", "paulo@email.com", "Eletricista", "Cadastro manual", "Aguardando contato inicial", null, "11700-500", "Praia Grande");
            UpsertSeedLeadBySource(connection, transaction, boardType, "seed.prestador.2", primeiroContatoStageId, "Juliana Ferreira", "(13) 99720-8833", "juliana@email.com", "Pintora", "Landing page", "Primeiro contato realizado", DateTime.UtcNow.AddDays(-1), "11701-600", "Praia Grande");
            UpsertSeedLeadBySource(connection, transaction, boardType, "seed.prestador.3", docPendenteStageId, "Rafael Gomes", "(13) 99612-7001", "rafael@email.com", "Encanador", "Formulario", "Faltando comprovante de endereco", DateTime.UtcNow.AddDays(-2), "11702-700", "Praia Grande");
            UpsertSeedLeadBySource(connection, transaction, boardType, "seed.prestador.4", validacaoStageId, "Silvia Costa", "(13) 99219-4400", "silvia@email.com", "Ar-condicionado", "WhatsApp", "Analise de perfil tecnico em andamento", DateTime.UtcNow.AddDays(-3), "11703-800", "Sao Vicente");
            UpsertSeedLeadBySource(connection, transaction, boardType, "seed.prestador.5", inativoStageId, "Andre Nogueira", "(13) 99170-1212", "andre@email.com", "Pedreiro", "Indicacao", "Cadastro pausado por falta de retorno", DateTime.UtcNow.AddDays(-10), "11704-900", "Praia Grande");
        }

        if (TableExists(connection, transaction, $"{TablePrefix}professional_registrations"))
        {
            var newStageId = GetStageIdByName(connection, transaction, boardType, "Novo cadastro");

            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = $"""
SELECT TOP (30) Id, Name, Profession, PostalCode, Phone, Services, SubmittedAt
FROM dbo.{TablePrefix}professional_registrations
ORDER BY Id DESC;
""";

            using var reader = command.ExecuteReader();
            var rows = new List<(int Id, string Name, string Profession, string PostalCode, string Phone, string Services, DateTime SubmittedAt)>();
            while (reader.Read())
            {
                rows.Add((
                    reader.GetInt32(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetString(4),
                    reader.GetString(5),
                    ReadAsUtcDateTime(reader, 6)
                ));
            }
            reader.Close();

            foreach (var row in rows)
            {
                var source = $"Cadastro profissional #{row.Id}";
                var status = row.Services.Length > 140 ? $"{row.Services[..140]}..." : row.Services;
                UpsertSeedLeadBySource(
                    connection,
                    transaction,
                    boardType,
                    source,
                    newStageId,
                    row.Name,
                    row.Phone,
                    null,
                    row.Profession,
                    source,
                    status,
                    row.SubmittedAt,
                    row.PostalCode,
                    null
                );
            }
        }
    }

    private static void SyncActiveProfessionalsToProviderKanban(SqlConnection connection, SqlTransaction transaction)
    {
        if (!TableExists(connection, transaction, $"{TablePrefix}professionals"))
        {
            return;
        }

        var boardType = AdminKanbanBoardTypes.Providers;
        var activeStageId = GetStageIdByName(connection, transaction, boardType, "Ativo na plataforma");

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
SELECT Id, Name, Profession, Description
FROM dbo.{TablePrefix}professionals
WHERE IsActive = 1
ORDER BY SortOrder, Name;
""";

        using var reader = command.ExecuteReader();
        var rows = new List<(int Id, string Name, string Profession, string Description)>();
        while (reader.Read())
        {
            rows.Add((
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? string.Empty : reader.GetString(3)
            ));
        }
        reader.Close();

        foreach (var row in rows)
        {
            var source = $"Profissional ativo #{row.Id}";
            var status = string.IsNullOrWhiteSpace(row.Description)
                ? "Profissional ativo na vitrine."
                : (row.Description.Length > 180 ? $"{row.Description[..180]}..." : row.Description);

            UpsertSeedLeadBySource(
                connection,
                transaction,
                boardType,
                source,
                activeStageId,
                row.Name,
                null,
                null,
                row.Profession,
                source,
                status,
                DateTime.UtcNow,
                null,
                null
            );
        }
    }

    private static bool TableExists(SqlConnection connection, SqlTransaction transaction, string tableName)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
SELECT CASE WHEN OBJECT_ID(@tableName, 'U') IS NULL THEN 0 ELSE 1 END;
""";
        command.Parameters.Add(new SqlParameter("@tableName", SqlDbType.NVarChar, 260) { Value = $"dbo.{tableName}" });
        return Convert.ToInt32(command.ExecuteScalar()) == 1;
    }

    private static bool HasAnyLead(SqlConnection connection, SqlTransaction transaction, string boardType)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
SELECT CASE WHEN EXISTS (
    SELECT 1
    FROM dbo.{TablePrefix}kanban_leads
    WHERE BoardType = @boardType AND IsActive = 1
) THEN 1 ELSE 0 END;
""";
        command.Parameters.Add(new SqlParameter("@boardType", SqlDbType.NVarChar, 30) { Value = boardType });
        return Convert.ToInt32(command.ExecuteScalar()) == 1;
    }

    private static bool ActiveLeadExists(SqlConnection connection, SqlTransaction transaction, int leadId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
SELECT CASE WHEN EXISTS (
    SELECT 1
    FROM dbo.{TablePrefix}kanban_leads
    WHERE Id = @leadId AND IsActive = 1
) THEN 1 ELSE 0 END;
""";
        command.Parameters.Add(new SqlParameter("@leadId", SqlDbType.Int) { Value = leadId });
        return Convert.ToInt32(command.ExecuteScalar()) == 1;
    }

    private static int GetStageIdByName(SqlConnection connection, SqlTransaction transaction, string boardType, string stageName)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
SELECT TOP (1) Id
FROM dbo.{TablePrefix}kanban_stages
WHERE BoardType = @boardType AND Name = @stageName AND IsActive = 1
ORDER BY SortOrder, Id;
""";
        command.Parameters.AddRange(
        [
            new SqlParameter("@boardType", SqlDbType.NVarChar, 30) { Value = boardType },
            new SqlParameter("@stageName", SqlDbType.NVarChar, 120) { Value = stageName }
        ]);
        var result = command.ExecuteScalar();
        if (result is null)
        {
            throw new InvalidOperationException($"Etapa '{stageName}' nao encontrada para o funil {boardType}.");
        }

        return Convert.ToInt32(result);
    }

    private static int UpsertSeedLeadBySource(
        SqlConnection connection,
        SqlTransaction transaction,
        string boardType,
        string sourceKey,
        int stageId,
        string name,
        string? phone,
        string? email,
        string? serviceCategory,
        string? sourceLabel,
        string? statusNote,
        DateTime? lastContactAt,
        string? postalCode,
        string? city)
    {
        sourceKey = TrimTo(sourceKey, 120);
        int? existingLeadId = null;
        int? existingStageId = null;

        using (var checkCommand = connection.CreateCommand())
        {
            checkCommand.Transaction = transaction;
            checkCommand.CommandText = $"""
SELECT TOP (1) Id, StageId
FROM dbo.{TablePrefix}kanban_leads
WHERE BoardType = @boardType AND Source = @sourceKey AND IsActive = 1
ORDER BY Id;
""";
            checkCommand.Parameters.AddRange(
            [
                new SqlParameter("@boardType", SqlDbType.NVarChar, 30) { Value = boardType },
                new SqlParameter("@sourceKey", SqlDbType.NVarChar, 120) { Value = sourceKey }
            ]);

            using var reader = checkCommand.ExecuteReader();
            if (reader.Read())
            {
                existingLeadId = reader.GetInt32(0);
                existingStageId = reader.GetInt32(1);
            }
        }

        if (existingLeadId.HasValue)
        {
            using var updateCommand = connection.CreateCommand();
            updateCommand.Transaction = transaction;
            updateCommand.CommandText = $"""
UPDATE dbo.{TablePrefix}kanban_leads
SET StageId = @stageId,
    Name = @name,
    Phone = @phone,
    Email = @email,
    ServiceCategory = @serviceCategory,
    PostalCode = @postalCode,
    City = @city,
    Source = @sourceLabel,
    Priority = 'normal',
    StatusNote = @statusNote,
    UpdatedAt = SYSUTCDATETIME(),
    LastContactAt = @lastContactAt
WHERE Id = @leadId;
""";
            updateCommand.Parameters.AddRange(
            [
                new SqlParameter("@stageId", SqlDbType.Int) { Value = stageId },
                new SqlParameter("@name", SqlDbType.NVarChar, 140) { Value = TrimTo(name, 140) },
                new SqlParameter("@phone", SqlDbType.NVarChar, 30) { Value = ToDbValue(phone) },
                new SqlParameter("@email", SqlDbType.NVarChar, 180) { Value = ToDbValue(email) },
                new SqlParameter("@serviceCategory", SqlDbType.NVarChar, 140) { Value = ToDbValue(serviceCategory) },
                new SqlParameter("@postalCode", SqlDbType.NVarChar, 9) { Value = ToDbValue(postalCode) },
                new SqlParameter("@city", SqlDbType.NVarChar, 120) { Value = ToDbValue(city) },
                new SqlParameter("@sourceLabel", SqlDbType.NVarChar, 120) { Value = ToDbValue(sourceLabel ?? sourceKey) },
                new SqlParameter("@statusNote", SqlDbType.NVarChar, 500) { Value = ToDbValue(statusNote) },
                new SqlParameter("@lastContactAt", SqlDbType.DateTime2) { Value = lastContactAt.HasValue ? lastContactAt.Value : DBNull.Value },
                new SqlParameter("@leadId", SqlDbType.Int) { Value = existingLeadId.Value }
            ]);
            updateCommand.ExecuteNonQuery();

            if (existingStageId.HasValue && existingStageId.Value != stageId)
            {
                InsertHistory(
                    connection,
                    transaction,
                    existingLeadId.Value,
                    eventType: "movido",
                    fromStageId: existingStageId.Value,
                    toStageId: stageId,
                    description: "Lead reposicionado automaticamente pelo seed do funil."
                );
            }

            return existingLeadId.Value;
        }

        var sortOrder = GetNextLeadSortOrder(connection, transaction, boardType, stageId);
        using (var insertCommand = connection.CreateCommand())
        {
            insertCommand.Transaction = transaction;
            insertCommand.CommandText = $"""
INSERT INTO dbo.{TablePrefix}kanban_leads
(BoardType, StageId, SortOrder, Name, Phone, Email, ServiceCategory, PostalCode, City, Source, Priority, StatusNote, InternalNotes, LastContactAt, IsActive, CreatedAt, UpdatedAt)
VALUES
(@boardType, @stageId, @sortOrder, @name, @phone, @email, @serviceCategory, @postalCode, @city, @sourceLabel, 'normal', @statusNote, NULL, @lastContactAt, 1, SYSUTCDATETIME(), NULL);
SELECT CAST(SCOPE_IDENTITY() AS INT);
""";
            insertCommand.Parameters.AddRange(
            [
                new SqlParameter("@boardType", SqlDbType.NVarChar, 30) { Value = boardType },
                new SqlParameter("@stageId", SqlDbType.Int) { Value = stageId },
                new SqlParameter("@sortOrder", SqlDbType.Int) { Value = sortOrder },
                new SqlParameter("@name", SqlDbType.NVarChar, 140) { Value = TrimTo(name, 140) },
                new SqlParameter("@phone", SqlDbType.NVarChar, 30) { Value = ToDbValue(phone) },
                new SqlParameter("@email", SqlDbType.NVarChar, 180) { Value = ToDbValue(email) },
                new SqlParameter("@serviceCategory", SqlDbType.NVarChar, 140) { Value = ToDbValue(serviceCategory) },
                new SqlParameter("@postalCode", SqlDbType.NVarChar, 9) { Value = ToDbValue(postalCode) },
                new SqlParameter("@city", SqlDbType.NVarChar, 120) { Value = ToDbValue(city) },
                new SqlParameter("@sourceLabel", SqlDbType.NVarChar, 120) { Value = ToDbValue(sourceLabel ?? sourceKey) },
                new SqlParameter("@statusNote", SqlDbType.NVarChar, 500) { Value = ToDbValue(statusNote) },
                new SqlParameter("@lastContactAt", SqlDbType.DateTime2) { Value = lastContactAt.HasValue ? lastContactAt.Value : DBNull.Value }
            ]);

            var leadId = Convert.ToInt32(insertCommand.ExecuteScalar());
            InsertHistory(
                connection,
                transaction,
                leadId,
                eventType: "seed",
                fromStageId: null,
                toStageId: stageId,
                description: "Lead de exemplo criado automaticamente no funil."
            );
            return leadId;
        }
    }

    private static int ResolveStageId(SqlConnection connection, SqlTransaction transaction, string boardType, int requestedStageId)
    {
        if (requestedStageId > 0)
        {
            using var validStageCommand = connection.CreateCommand();
            validStageCommand.Transaction = transaction;
            validStageCommand.CommandText = $"""
SELECT TOP (1) Id
FROM dbo.{TablePrefix}kanban_stages
WHERE Id = @stageId AND BoardType = @boardType AND IsActive = 1;
""";
            validStageCommand.Parameters.AddRange(
            [
                new SqlParameter("@stageId", SqlDbType.Int) { Value = requestedStageId },
                new SqlParameter("@boardType", SqlDbType.NVarChar, 30) { Value = boardType }
            ]);
            var validStageId = validStageCommand.ExecuteScalar();
            if (validStageId is not null)
            {
                return Convert.ToInt32(validStageId);
            }
        }

        using var firstStageCommand = connection.CreateCommand();
        firstStageCommand.Transaction = transaction;
        firstStageCommand.CommandText = $"""
SELECT TOP (1) Id
FROM dbo.{TablePrefix}kanban_stages
WHERE BoardType = @boardType AND IsActive = 1
ORDER BY SortOrder, Id;
""";
        firstStageCommand.Parameters.Add(new SqlParameter("@boardType", SqlDbType.NVarChar, 30) { Value = boardType });
        var firstStageId = firstStageCommand.ExecuteScalar();
        if (firstStageId is null)
        {
            throw new InvalidOperationException("Nenhuma etapa ativa encontrada para o funil informado.");
        }

        return Convert.ToInt32(firstStageId);
    }

    private static int GetNextLeadSortOrder(SqlConnection connection, SqlTransaction transaction, string boardType, int stageId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
SELECT ISNULL(MAX(SortOrder), 0) + 1
FROM dbo.{TablePrefix}kanban_leads
WHERE BoardType = @boardType AND StageId = @stageId AND IsActive = 1;
""";
        command.Parameters.AddRange(
        [
            new SqlParameter("@boardType", SqlDbType.NVarChar, 30) { Value = boardType },
            new SqlParameter("@stageId", SqlDbType.Int) { Value = stageId }
        ]);
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private static int CreateTelegramLead(
        SqlConnection connection,
        SqlTransaction transaction,
        string boardType,
        int stageId,
        AdminKanbanTelegramLeadUpsertRequest request)
    {
        var nextSortOrder = GetNextLeadSortOrder(connection, transaction, boardType, stageId);

        using var insertCommand = connection.CreateCommand();
        insertCommand.Transaction = transaction;
        insertCommand.CommandText = $"""
INSERT INTO dbo.{TablePrefix}kanban_leads
(BoardType, StageId, SortOrder, Name, Phone, Email, ServiceCategory, PostalCode, City, Source, Priority, StatusNote, InternalNotes, LastContactAt, IsActive, CreatedAt, UpdatedAt)
VALUES
(@boardType, @stageId, @sortOrder, @name, @phone, @email, @serviceCategory, @postalCode, @city, 'Telegram', 'normal', @statusNote, @internalNotes, @lastContactAt, 1, SYSUTCDATETIME(), NULL);
SELECT CAST(SCOPE_IDENTITY() AS INT);
""";
        insertCommand.Parameters.AddRange(
        [
            new SqlParameter("@boardType", SqlDbType.NVarChar, 30) { Value = boardType },
            new SqlParameter("@stageId", SqlDbType.Int) { Value = stageId },
            new SqlParameter("@sortOrder", SqlDbType.Int) { Value = nextSortOrder },
            new SqlParameter("@name", SqlDbType.NVarChar, 140) { Value = TrimTo(request.ClientName, 140) },
            new SqlParameter("@phone", SqlDbType.NVarChar, 30) { Value = ToDbValue(request.ClientPhone) },
            new SqlParameter("@email", SqlDbType.NVarChar, 180) { Value = ToDbValue(request.ClientEmail) },
            new SqlParameter("@serviceCategory", SqlDbType.NVarChar, 140) { Value = ToDbValue(request.ServiceCategory) },
            new SqlParameter("@postalCode", SqlDbType.NVarChar, 9) { Value = ToDbValue(request.PostalCode) },
            new SqlParameter("@city", SqlDbType.NVarChar, 120) { Value = ToDbValue(request.City) },
            new SqlParameter("@statusNote", SqlDbType.NVarChar, 500) { Value = ToDbValue(TrimTo(request.StatusNote, 500)) },
            new SqlParameter("@internalNotes", SqlDbType.NVarChar, -1) { Value = ToDbValue(request.InternalNotes) },
            new SqlParameter("@lastContactAt", SqlDbType.DateTime2) { Value = request.LastContactAt.HasValue ? request.LastContactAt.Value : DBNull.Value }
        ]);

        var leadId = Convert.ToInt32(insertCommand.ExecuteScalar());
        InsertHistory(
            connection,
            transaction,
            leadId,
            eventType: "telegram_lead_criado",
            fromStageId: null,
            toStageId: stageId,
            description: "Lead criado automaticamente a partir da conversa do bot Telegram."
        );

        return leadId;
    }

    private static void UpdateTelegramLead(
        SqlConnection connection,
        SqlTransaction transaction,
        int leadId,
        int stageId,
        AdminKanbanTelegramLeadUpsertRequest request)
    {
        using var updateCommand = connection.CreateCommand();
        updateCommand.Transaction = transaction;
        updateCommand.CommandText = $"""
UPDATE dbo.{TablePrefix}kanban_leads
SET StageId = @stageId,
    Name = @name,
    Phone = COALESCE(@phone, Phone),
    Email = COALESCE(@email, Email),
    ServiceCategory = COALESCE(@serviceCategory, ServiceCategory),
    PostalCode = COALESCE(@postalCode, PostalCode),
    City = COALESCE(@city, City),
    Source = 'Telegram',
    Priority = 'normal',
    StatusNote = CASE WHEN @statusNote IS NULL THEN StatusNote ELSE @statusNote END,
    InternalNotes = CASE WHEN @internalNotes IS NULL THEN InternalNotes ELSE @internalNotes END,
    LastContactAt = COALESCE(@lastContactAt, LastContactAt),
    UpdatedAt = SYSUTCDATETIME()
WHERE Id = @leadId AND IsActive = 1;
""";
        updateCommand.Parameters.AddRange(
        [
            new SqlParameter("@stageId", SqlDbType.Int) { Value = stageId },
            new SqlParameter("@name", SqlDbType.NVarChar, 140) { Value = TrimTo(request.ClientName, 140) },
            new SqlParameter("@phone", SqlDbType.NVarChar, 30) { Value = ToDbValue(request.ClientPhone) },
            new SqlParameter("@email", SqlDbType.NVarChar, 180) { Value = ToDbValue(request.ClientEmail) },
            new SqlParameter("@serviceCategory", SqlDbType.NVarChar, 140) { Value = ToDbValue(request.ServiceCategory) },
            new SqlParameter("@postalCode", SqlDbType.NVarChar, 9) { Value = ToDbValue(request.PostalCode) },
            new SqlParameter("@city", SqlDbType.NVarChar, 120) { Value = ToDbValue(request.City) },
            new SqlParameter("@statusNote", SqlDbType.NVarChar, 500) { Value = ToDbValue(TrimTo(request.StatusNote, 500)) },
            new SqlParameter("@internalNotes", SqlDbType.NVarChar, -1) { Value = ToDbValue(request.InternalNotes) },
            new SqlParameter("@lastContactAt", SqlDbType.DateTime2) { Value = request.LastContactAt.HasValue ? request.LastContactAt.Value : DBNull.Value },
            new SqlParameter("@leadId", SqlDbType.Int) { Value = leadId }
        ]);
        updateCommand.ExecuteNonQuery();
    }

    private static void SaveTelegramLeadLink(
        SqlConnection connection,
        SqlTransaction transaction,
        int leadId,
        string boardType,
        AdminKanbanTelegramLeadUpsertRequest request)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
MERGE dbo.{TablePrefix}telegram_funil_links AS target
USING (
    SELECT
        @chatbotConversationId AS ChatbotConversationId,
        @leadId AS LeadId,
        @boardType AS BoardType,
        @channelConversationId AS ChannelConversationId,
        @telegramChatId AS TelegramChatId,
        @clientId AS ClientId,
        @clientPhone AS ClientPhone,
        @clientEmail AS ClientEmail,
        @serviceRequestId AS ServiceRequestId
) AS source
ON target.ChatbotConversationId = source.ChatbotConversationId
WHEN MATCHED THEN
    UPDATE SET
        LeadId = source.LeadId,
        BoardType = source.BoardType,
        ChannelConversationId = source.ChannelConversationId,
        TelegramChatId = source.TelegramChatId,
        ClientId = source.ClientId,
        ClientPhone = COALESCE(source.ClientPhone, target.ClientPhone),
        ClientEmail = COALESCE(source.ClientEmail, target.ClientEmail),
        ServiceRequestId = COALESCE(source.ServiceRequestId, target.ServiceRequestId),
        UpdatedAt = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
    INSERT (ChatbotConversationId, LeadId, BoardType, ChannelConversationId, TelegramChatId, ClientId, ClientPhone, ClientEmail, ServiceRequestId, CreatedAt, UpdatedAt)
    VALUES (source.ChatbotConversationId, source.LeadId, source.BoardType, source.ChannelConversationId, source.TelegramChatId, source.ClientId, source.ClientPhone, source.ClientEmail, source.ServiceRequestId, SYSUTCDATETIME(), SYSUTCDATETIME());
""";
        command.Parameters.AddRange(
        [
            new SqlParameter("@chatbotConversationId", SqlDbType.UniqueIdentifier) { Value = request.ChatbotConversationId },
            new SqlParameter("@leadId", SqlDbType.Int) { Value = leadId },
            new SqlParameter("@boardType", SqlDbType.NVarChar, 30) { Value = boardType },
            new SqlParameter("@channelConversationId", SqlDbType.NVarChar, 128) { Value = TrimTo(request.ChannelConversationId, 128) },
            new SqlParameter("@telegramChatId", SqlDbType.BigInt) { Value = request.TelegramChatId },
            new SqlParameter("@clientId", SqlDbType.UniqueIdentifier) { Value = request.ClientId },
            new SqlParameter("@clientPhone", SqlDbType.NVarChar, 30) { Value = ToDbValue(request.ClientPhone) },
            new SqlParameter("@clientEmail", SqlDbType.NVarChar, 180) { Value = ToDbValue(request.ClientEmail) },
            new SqlParameter("@serviceRequestId", SqlDbType.UniqueIdentifier) { Value = request.ServiceRequestId.HasValue ? request.ServiceRequestId.Value : DBNull.Value }
        ]);
        command.ExecuteNonQuery();
    }

    private static void InsertHistory(
        SqlConnection connection,
        SqlTransaction transaction,
        int leadId,
        string eventType,
        int? fromStageId,
        int? toStageId,
        string? description)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
INSERT INTO dbo.{TablePrefix}kanban_lead_history
(LeadId, EventType, FromStageId, ToStageId, Description, CreatedAt)
VALUES
(@leadId, @eventType, @fromStageId, @toStageId, @description, SYSUTCDATETIME());
""";
        command.Parameters.AddRange(
        [
            new SqlParameter("@leadId", SqlDbType.Int) { Value = leadId },
            new SqlParameter("@eventType", SqlDbType.NVarChar, 40) { Value = TrimTo(eventType, 40) },
            new SqlParameter("@fromStageId", SqlDbType.Int) { Value = fromStageId.HasValue ? fromStageId.Value : DBNull.Value },
            new SqlParameter("@toStageId", SqlDbType.Int) { Value = toStageId.HasValue ? toStageId.Value : DBNull.Value },
            new SqlParameter("@description", SqlDbType.NVarChar, -1) { Value = ToDbValue(description) }
        ]);
        command.ExecuteNonQuery();
    }

    private static object ToDbValue(string? value) =>
        string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();

    private static long? ReadNullableInt64(SqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetInt64(ordinal);

    private static DateTime? ReadNullableUtcDateTime(SqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : ReadAsUtcDateTime(reader, ordinal);

    private static DateTime ReadAsUtcDateTime(SqlDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
        {
            return DateTime.UtcNow;
        }

        var fieldType = reader.GetFieldType(ordinal);
        if (fieldType == typeof(DateTimeOffset))
        {
            return reader.GetDateTimeOffset(ordinal).UtcDateTime;
        }

        return reader.GetDateTime(ordinal);
    }

    private static string TrimTo(string? value, int maxLength)
    {
        var normalized = (value ?? string.Empty).Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private static string? NormalizeChatwootSyncStatus(string? syncStatus)
    {
        if (string.IsNullOrWhiteSpace(syncStatus))
        {
            return null;
        }

        return TrimTo(syncStatus, 30).ToLowerInvariant();
    }

    private static string NormalizeChatwootSyncQueueStatus(string? status)
    {
        var normalized = TrimTo(status, 30).ToLowerInvariant();
        return normalized switch
        {
            ChatwootSyncQueueStatuses.Queued => ChatwootSyncQueueStatuses.Queued,
            ChatwootSyncQueueStatuses.Processing => ChatwootSyncQueueStatuses.Processing,
            ChatwootSyncQueueStatuses.Retrying => ChatwootSyncQueueStatuses.Retrying,
            ChatwootSyncQueueStatuses.Processed => ChatwootSyncQueueStatuses.Processed,
            ChatwootSyncQueueStatuses.DeadLetter => ChatwootSyncQueueStatuses.DeadLetter,
            _ => ChatwootSyncQueueStatuses.Queued
        };
    }

    private static string NormalizeChatwootSyncOperationType(string? operationType)
    {
        var normalized = TrimTo(operationType, 40).ToLowerInvariant();
        return normalized switch
        {
            ChatwootSyncOperationTypes.LeadSync => ChatwootSyncOperationTypes.LeadSync,
            ChatwootSyncOperationTypes.StageSync => ChatwootSyncOperationTypes.StageSync,
            _ => throw new InvalidOperationException($"Tipo de operacao Chatwoot nao suportado: '{operationType}'.")
        };
    }

    private static string NormalizeWebhookProcessStatus(string? processStatus)
    {
        if (string.IsNullOrWhiteSpace(processStatus))
        {
            return "received";
        }

        return TrimTo(processStatus, 30).ToLowerInvariant();
    }

    private static bool TryGetChatwootBackfillCheckpoint(
        SqlConnection connection,
        string scopeKey,
        out AdminKanbanChatwootBackfillCheckpointRecord checkpoint)
    {
        checkpoint = null!;

        using var command = connection.CreateCommand();
        command.CommandText = $"""
SELECT TOP (1)
    ScopeKey,
    LastProcessedLeadId,
    LastRunStartedAt,
    LastRunCompletedAt,
    LastRunStatus,
    LastSummaryJson,
    UpdatedAt
FROM dbo.{TablePrefix}chatwoot_backfill_checkpoints
WHERE ScopeKey = @scopeKey
ORDER BY Id DESC;
""";
        command.Parameters.Add(new SqlParameter("@scopeKey", SqlDbType.NVarChar, 80) { Value = scopeKey });

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return false;
        }

        checkpoint = new AdminKanbanChatwootBackfillCheckpointRecord
        {
            ScopeKey = reader.GetString(0),
            LastProcessedLeadId = reader.IsDBNull(1) ? null : reader.GetInt32(1),
            LastRunStartedAt = reader.IsDBNull(2) ? null : reader.GetDateTime(2),
            LastRunCompletedAt = reader.IsDBNull(3) ? null : reader.GetDateTime(3),
            LastRunStatus = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
            LastSummaryJson = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
            UpdatedAt = reader.GetDateTime(6)
        };
        return true;
    }

    private static bool TryGetChatwootWebhookEventByProviderEventId(
        SqlConnection connection,
        string? providerEventId,
        out AdminKanbanChatwootWebhookEventRecord webhookEvent)
    {
        webhookEvent = null!;
        if (string.IsNullOrWhiteSpace(providerEventId))
        {
            return false;
        }

        using var command = connection.CreateCommand();
        command.CommandText = $"""
SELECT TOP (1) Id, ProviderEventId, EventType, ConversationId, ProcessStatus, ReceivedAt, ProcessedAt, ErrorMessage
FROM dbo.{TablePrefix}chatwoot_webhook_events
WHERE ProviderEventId = @providerEventId
ORDER BY Id DESC;
""";
        command.Parameters.Add(new SqlParameter("@providerEventId", SqlDbType.NVarChar, 120) { Value = providerEventId });

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return false;
        }

        webhookEvent = new AdminKanbanChatwootWebhookEventRecord
        {
            Id = reader.GetInt32(0),
            ProviderEventId = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
            EventType = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
            ConversationId = ReadNullableInt64(reader, 3),
            ProcessStatus = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
            ReceivedAt = reader.GetDateTime(5),
            ProcessedAt = reader.IsDBNull(6) ? null : reader.GetDateTime(6),
            ErrorMessage = reader.IsDBNull(7) ? string.Empty : reader.GetString(7)
        };
        return true;
    }

    private static bool TryGetActiveChatwootSyncQueueItem(
        SqlConnection connection,
        int leadId,
        string operationType,
        out AdminKanbanChatwootSyncQueueItemRecord queueItem)
    {
        queueItem = null!;

        using var command = connection.CreateCommand();
        command.CommandText = $"""
SELECT TOP (1)
    Id,
    LeadId,
    OperationType,
    Status,
    AttemptCount,
    MaxAttempts,
    NextAttemptAt,
    LastAttemptAt,
    LastError,
    WorkerInstance,
    CreatedAt,
    UpdatedAt,
    ProcessedAt,
    DeadLetterAt
FROM dbo.{TablePrefix}chatwoot_sync_queue
WHERE LeadId = @leadId
  AND OperationType = @operationType
  AND Status IN ('queued', 'retrying', 'processing')
ORDER BY Id DESC;
""";
        command.Parameters.AddRange(
        [
            new SqlParameter("@leadId", SqlDbType.Int) { Value = leadId },
            new SqlParameter("@operationType", SqlDbType.NVarChar, 40) { Value = operationType }
        ]);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return false;
        }

        queueItem = ReadChatwootSyncQueueItem(reader);
        return true;
    }

    private static bool TryGetChatwootSyncQueueItemById(
        SqlConnection connection,
        int queueItemId,
        out AdminKanbanChatwootSyncQueueItemRecord queueItem)
    {
        queueItem = null!;

        using var command = connection.CreateCommand();
        command.CommandText = $"""
SELECT TOP (1)
    Id,
    LeadId,
    OperationType,
    Status,
    AttemptCount,
    MaxAttempts,
    NextAttemptAt,
    LastAttemptAt,
    LastError,
    WorkerInstance,
    CreatedAt,
    UpdatedAt,
    ProcessedAt,
    DeadLetterAt
FROM dbo.{TablePrefix}chatwoot_sync_queue
WHERE Id = @queueItemId;
""";
        command.Parameters.Add(new SqlParameter("@queueItemId", SqlDbType.Int) { Value = queueItemId });

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return false;
        }

        queueItem = ReadChatwootSyncQueueItem(reader);
        return true;
    }

    private static AdminKanbanChatwootSyncQueueItemRecord ReadChatwootSyncQueueItem(SqlDataReader reader) =>
        new()
        {
            Id = reader.GetInt32(0),
            LeadId = reader.GetInt32(1),
            OperationType = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
            Status = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
            AttemptCount = reader.GetInt32(4),
            MaxAttempts = reader.GetInt32(5),
            NextAttemptAt = reader.GetDateTime(6),
            LastAttemptAt = reader.IsDBNull(7) ? null : reader.GetDateTime(7),
            LastError = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
            WorkerInstance = reader.IsDBNull(9) ? string.Empty : reader.GetString(9),
            CreatedAt = reader.GetDateTime(10),
            UpdatedAt = reader.GetDateTime(11),
            ProcessedAt = reader.IsDBNull(12) ? null : reader.GetDateTime(12),
            DeadLetterAt = reader.IsDBNull(13) ? null : reader.GetDateTime(13)
        };

    private static bool TryGetTelegramDeliveryQueueItemByDirectionAndKey(
        SqlConnection connection,
        string direction,
        string deliveryKey,
        out AdminKanbanTelegramDeliveryQueueItemRecord queueItem)
    {
        queueItem = null!;

        using var command = connection.CreateCommand();
        command.CommandText = $"""
SELECT TOP (1)
    Id,
    LeadId,
    Direction,
    DeliveryKey,
    PayloadJson,
    ChatwootConversationId,
    TelegramChatId,
    Status,
    AttemptCount,
    MaxAttempts,
    NextAttemptAt,
    LastAttemptAt,
    LastError,
    WorkerInstance,
    CreatedAt,
    UpdatedAt,
    ProcessedAt,
    DeadLetterAt
FROM dbo.{TablePrefix}telegram_delivery_queue
WHERE Direction = @direction
  AND DeliveryKey = @deliveryKey
ORDER BY Id DESC;
""";
        command.Parameters.AddRange(
        [
            new SqlParameter("@direction", SqlDbType.NVarChar, 40) { Value = direction },
            new SqlParameter("@deliveryKey", SqlDbType.NVarChar, 180) { Value = deliveryKey }
        ]);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return false;
        }

        queueItem = ReadTelegramDeliveryQueueItem(reader);
        return true;
    }

    private static bool TryGetTelegramDeliveryQueueItemById(
        SqlConnection connection,
        int queueItemId,
        out AdminKanbanTelegramDeliveryQueueItemRecord queueItem)
    {
        queueItem = null!;

        using var command = connection.CreateCommand();
        command.CommandText = $"""
SELECT TOP (1)
    Id,
    LeadId,
    Direction,
    DeliveryKey,
    PayloadJson,
    ChatwootConversationId,
    TelegramChatId,
    Status,
    AttemptCount,
    MaxAttempts,
    NextAttemptAt,
    LastAttemptAt,
    LastError,
    WorkerInstance,
    CreatedAt,
    UpdatedAt,
    ProcessedAt,
    DeadLetterAt
FROM dbo.{TablePrefix}telegram_delivery_queue
WHERE Id = @queueItemId;
""";
        command.Parameters.Add(new SqlParameter("@queueItemId", SqlDbType.Int) { Value = queueItemId });

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return false;
        }

        queueItem = ReadTelegramDeliveryQueueItem(reader);
        return true;
    }

    private static AdminKanbanTelegramDeliveryQueueItemRecord ReadTelegramDeliveryQueueItem(SqlDataReader reader) =>
        new()
        {
            Id = reader.GetInt32(0),
            LeadId = reader.GetInt32(1),
            Direction = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
            DeliveryKey = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
            PayloadJson = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
            ChatwootConversationId = ReadNullableInt64(reader, 5),
            TelegramChatId = ReadNullableInt64(reader, 6),
            Status = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
            AttemptCount = reader.GetInt32(8),
            MaxAttempts = reader.GetInt32(9),
            NextAttemptAt = reader.GetDateTime(10),
            LastAttemptAt = reader.IsDBNull(11) ? null : reader.GetDateTime(11),
            LastError = reader.IsDBNull(12) ? string.Empty : reader.GetString(12),
            WorkerInstance = reader.IsDBNull(13) ? string.Empty : reader.GetString(13),
            CreatedAt = reader.GetDateTime(14),
            UpdatedAt = reader.GetDateTime(15),
            ProcessedAt = reader.IsDBNull(16) ? null : reader.GetDateTime(16),
            DeadLetterAt = reader.IsDBNull(17) ? null : reader.GetDateTime(17)
        };

    private static bool IsUniqueKeyViolation(SqlException ex) => ex.Number is 2601 or 2627;

    private static string NormalizeTelegramDeliveryDirection(string? direction)
    {
        var normalized = (direction ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            TelegramDeliveryDirections.TelegramToChatwoot => TelegramDeliveryDirections.TelegramToChatwoot,
            TelegramDeliveryDirections.ChatwootToTelegram => TelegramDeliveryDirections.ChatwootToTelegram,
            _ => throw new InvalidOperationException($"Direcao da fila Telegram nao suportada: '{direction}'.")
        };
    }

    private static string NormalizeTelegramDeliveryQueueStatus(string? status)
    {
        var normalized = (status ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            TelegramDeliveryQueueStatuses.Queued => TelegramDeliveryQueueStatuses.Queued,
            TelegramDeliveryQueueStatuses.Processing => TelegramDeliveryQueueStatuses.Processing,
            TelegramDeliveryQueueStatuses.Retrying => TelegramDeliveryQueueStatuses.Retrying,
            TelegramDeliveryQueueStatuses.Processed => TelegramDeliveryQueueStatuses.Processed,
            TelegramDeliveryQueueStatuses.DeadLetter => TelegramDeliveryQueueStatuses.DeadLetter,
            _ => throw new InvalidOperationException($"Status da fila Telegram nao suportado: '{status}'.")
        };
    }

    private static string NormalizePriority(string? priority)
    {
        if (string.Equals(priority, "alta", StringComparison.OrdinalIgnoreCase))
        {
            return "alta";
        }

        if (string.Equals(priority, "baixa", StringComparison.OrdinalIgnoreCase))
        {
            return "baixa";
        }

        return "normal";
    }
}
