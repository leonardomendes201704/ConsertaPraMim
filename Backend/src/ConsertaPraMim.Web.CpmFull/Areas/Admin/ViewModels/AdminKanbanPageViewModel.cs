namespace AppMobileCPM.Areas.Admin.ViewModels;

public sealed class AdminKanbanPageViewModel
{
    public required string BoardType { get; init; }
    public required string BoardTitle { get; init; }
    public required string BoardSubtitle { get; init; }
    public required string AlternateBoardUrl { get; init; }
    public required string AlternateBoardLabel { get; init; }
    public required IReadOnlyList<AdminKanbanStageViewModel> Stages { get; init; }
    public int TotalLeads => Stages.Sum(stage => stage.Leads.Count);
}

public sealed class AdminKanbanStageViewModel
{
    public int Id { get; init; }
    public required string Name { get; init; }
    public required string Color { get; init; }
    public int SortOrder { get; init; }
    public required IReadOnlyList<AdminKanbanLeadCardViewModel> Leads { get; init; }
}

public sealed class AdminKanbanLeadCardViewModel
{
    public int Id { get; init; }
    public int StageId { get; init; }
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
