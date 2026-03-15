using AppMobileCPM.Services;

namespace AppMobileCPM.Areas.Admin.ViewModels;

public sealed class AdminTelegramBusinessDashboardFilterInputModel
{
    public string? BoardType { get; init; }
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
}

public sealed class AdminTelegramBusinessDashboardPageViewModel
{
    public required string Title { get; init; }
    public required string Subtitle { get; init; }
    public required string SelectedBoardType { get; init; }
    public required string SelectedBoardLabel { get; init; }
    public required string PeriodStartValue { get; init; }
    public required string PeriodEndValue { get; init; }
    public required string PeriodLabel { get; init; }
    public required AdminKanbanTelegramBusinessDashboardSnapshot Snapshot { get; init; }
    public string DashboardUrl { get; init; } = "/admin";
    public string ClientsBoardUrl { get; init; } = "/admin/funil/clientes";
    public string ProvidersBoardUrl { get; init; } = "/admin/funil/prestadores";
    public string FiltersClearUrl { get; init; } = "/admin/telegram/painel";

    public bool HasData => Snapshot.TotalTelegramLeads > 0;
}
