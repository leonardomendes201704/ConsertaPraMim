namespace AppMobileCPM.Services;

public static class AdminKanbanBoardTypes
{
    public const string Clients = "clientes";
    public const string Providers = "prestadores";

    public static bool IsValid(string? boardType) =>
        string.Equals(boardType, Clients, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(boardType, Providers, StringComparison.OrdinalIgnoreCase);

    public static string Normalize(string? boardType)
    {
        if (string.Equals(boardType, Clients, StringComparison.OrdinalIgnoreCase))
        {
            return Clients;
        }

        if (string.Equals(boardType, Providers, StringComparison.OrdinalIgnoreCase))
        {
            return Providers;
        }

        throw new ArgumentException("Tipo de funil invalido.", nameof(boardType));
    }

    public static string GetTitle(string boardType) =>
        Normalize(boardType) switch
        {
            Clients => "Funil de Atendimento - Clientes",
            Providers => "Onboarding e Contato - Prestadores",
            _ => "Funil"
        };

    public static string GetSubtitle(string boardType) =>
        Normalize(boardType) switch
        {
            Clients => "Gerencie o ciclo de atendimento desde o primeiro contato ate a conclusao.",
            Providers => "Acompanhe o onboarding, validacao e ativacao de prestadores na plataforma.",
            _ => "Gerencie seu funil"
        };
}

public sealed class AdminKanbanBoardData
{
    public required string BoardType { get; init; }
    public required IReadOnlyList<AdminKanbanStageRecord> Stages { get; init; }
}

public sealed class AdminKanbanStageRecord
{
    public int Id { get; init; }
    public required string BoardType { get; init; }
    public required string Name { get; init; }
    public string Color { get; init; } = "#0d6efd";
    public int SortOrder { get; init; }
    public required IReadOnlyList<AdminKanbanLeadCardRecord> Leads { get; init; }
}

public sealed class AdminKanbanLeadCardRecord
{
    public int Id { get; init; }
    public int StageId { get; init; }
    public required string BoardType { get; init; }
    public required string Name { get; init; }
    public string Phone { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string ServiceCategory { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public string Priority { get; init; } = "normal";
    public string StatusNote { get; init; } = string.Empty;
    public DateTime StageEnteredAt { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public DateTime? LastContactAt { get; init; }
}

public sealed class AdminKanbanLeadDetailsRecord
{
    public int Id { get; init; }
    public int StageId { get; init; }
    public required string StageName { get; init; }
    public required string BoardType { get; init; }
    public required string Name { get; init; }
    public string Phone { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string ServiceCategory { get; init; } = string.Empty;
    public string PostalCode { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public string Priority { get; init; } = "normal";
    public string StatusNote { get; init; } = string.Empty;
    public string InternalNotes { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public DateTime? LastContactAt { get; init; }
    public required IReadOnlyList<AdminKanbanLeadHistoryRecord> History { get; init; }
}

public sealed class AdminKanbanLeadHistoryRecord
{
    public int Id { get; init; }
    public required string EventType { get; init; }
    public int? FromStageId { get; init; }
    public string FromStageName { get; init; } = string.Empty;
    public int? ToStageId { get; init; }
    public string ToStageName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}

public sealed class AdminKanbanLeadUpsertRequest
{
    public required string BoardType { get; init; }
    public int StageId { get; init; }
    public required string Name { get; init; }
    public string Phone { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string ServiceCategory { get; init; } = string.Empty;
    public string PostalCode { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public string Priority { get; init; } = "normal";
    public string StatusNote { get; init; } = string.Empty;
    public string InternalNotes { get; init; } = string.Empty;
    public DateTime? LastContactAt { get; init; }
}

public sealed class AdminKanbanBoardOrderUpdateRequest
{
    public required string BoardType { get; init; }
    public int? ChangedLeadId { get; init; }
    public int? FromStageId { get; init; }
    public int? ToStageId { get; init; }
    public required IReadOnlyList<AdminKanbanStageOrderUpdateItem> Stages { get; init; }
}

public sealed class AdminKanbanStageOrderUpdateItem
{
    public int StageId { get; init; }
    public required IReadOnlyList<int> LeadIds { get; init; }
}
