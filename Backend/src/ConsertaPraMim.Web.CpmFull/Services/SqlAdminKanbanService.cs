using System.Data;
using Microsoft.Data.SqlClient;

namespace AppMobileCPM.Services;

public sealed class SqlAdminKanbanService : IAdminKanbanService
{
    private const string TablePrefix = "cpm_web_";

    private static readonly IReadOnlyList<(string Name, string Color)> ClientDefaultStages =
    [
        ("Novo lead", "#0d6efd"),
        ("Tentativa de contato", "#fd7e14"),
        ("Agendado", "#6f42c1"),
        ("Em atendimento", "#0dcaf0"),
        ("Concluido", "#198754"),
        ("Perdido", "#dc3545")
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
                    CreatedAt = reader.GetDateTime(10),
                    UpdatedAt = reader.IsDBNull(11) ? null : reader.GetDateTime(11),
                    LastContactAt = reader.IsDBNull(12) ? null : reader.GetDateTime(12),
                    StageEnteredAt = reader.GetDateTime(13)
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
    l.Source, l.Priority, l.StatusNote, l.InternalNotes, l.CreatedAt, l.UpdatedAt, l.LastContactAt
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
                    History = []
                };
            }
        }

        if (details is null)
        {
            return null;
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

        using (var checkCommand = connection.CreateCommand())
        {
            checkCommand.Transaction = transaction;
            checkCommand.CommandText = $"""
SELECT TOP (1) Id
FROM dbo.{TablePrefix}kanban_leads
WHERE Id = @leadId AND IsActive = 1;
""";
            checkCommand.Parameters.Add(new SqlParameter("@leadId", SqlDbType.Int) { Value = leadId });
            var exists = checkCommand.ExecuteScalar();
            if (exists is null)
            {
                return false;
            }
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

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.{TablePrefix}kanban_stages') AND name = 'IX_{TablePrefix}kanban_stages_board')
CREATE INDEX IX_{TablePrefix}kanban_stages_board
    ON dbo.{TablePrefix}kanban_stages(BoardType, SortOrder, Id);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.{TablePrefix}kanban_leads') AND name = 'IX_{TablePrefix}kanban_leads_board_stage')
CREATE INDEX IX_{TablePrefix}kanban_leads_board_stage
    ON dbo.{TablePrefix}kanban_leads(BoardType, StageId, SortOrder, Id);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.{TablePrefix}kanban_lead_history') AND name = 'IX_{TablePrefix}kanban_history_lead')
CREATE INDEX IX_{TablePrefix}kanban_history_lead
    ON dbo.{TablePrefix}kanban_lead_history(LeadId, CreatedAt DESC, Id DESC);
""";
                command.ExecuteNonQuery();
            }

            SeedStages(connection, transaction, AdminKanbanBoardTypes.Clients, ClientDefaultStages);
            SeedStages(connection, transaction, AdminKanbanBoardTypes.Providers, ProviderDefaultStages);
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
            var tentativaContatoStageId = GetStageIdByName(connection, transaction, boardType, "Tentativa de contato");
            var agendadoStageId = GetStageIdByName(connection, transaction, boardType, "Agendado");
            var emAtendimentoStageId = GetStageIdByName(connection, transaction, boardType, "Em atendimento");
            var concluidoStageId = GetStageIdByName(connection, transaction, boardType, "Concluido");
            var perdidoStageId = GetStageIdByName(connection, transaction, boardType, "Perdido");

            UpsertSeedLeadBySource(connection, transaction, boardType, "seed.cliente.1", novoLeadStageId, "Mariana Souza", "(13) 99877-1100", "mariana@email.com", "Encanador", "Padrao", "Vazamento na pia da cozinha", null, "11700-130", "Praia Grande");
            UpsertSeedLeadBySource(connection, transaction, boardType, "seed.cliente.2", tentativaContatoStageId, "Ricardo Almeida", "(13) 99711-4422", "ricardo@email.com", "Eletricista", "WhatsApp", "Aguardando retorno do cliente", DateTime.UtcNow.AddHours(-5), "11701-200", "Praia Grande");
            UpsertSeedLeadBySource(connection, transaction, boardType, "seed.cliente.3", agendadoStageId, "Carla Nunes", "(13) 99655-8822", "carla@email.com", "Ar-condicionado", "Formulario", "Visita agendada para amanha 14h", DateTime.UtcNow.AddHours(-2), "11702-330", "Praia Grande");
            UpsertSeedLeadBySource(connection, transaction, boardType, "seed.cliente.4", emAtendimentoStageId, "Fernando Lima", "(13) 99122-7600", "fernando@email.com", "Pedreiro", "Indicacao", "Reforma em andamento", DateTime.UtcNow.AddDays(-1), "11703-040", "Sao Vicente");
            UpsertSeedLeadBySource(connection, transaction, boardType, "seed.cliente.5", concluidoStageId, "Luciana Prado", "(13) 99966-1200", "luciana@email.com", "Pintor", "Formulario", "Servico finalizado e cliente satisfeito", DateTime.UtcNow.AddDays(-2), "11704-900", "Praia Grande");
            UpsertSeedLeadBySource(connection, transaction, boardType, "seed.cliente.6", perdidoStageId, "Bruno Castro", "(13) 99221-4567", "bruno@email.com", "Chaveiro", "Ligacao", "Cliente fechou com concorrente", DateTime.UtcNow.AddDays(-3), "11705-010", "Praia Grande");
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
