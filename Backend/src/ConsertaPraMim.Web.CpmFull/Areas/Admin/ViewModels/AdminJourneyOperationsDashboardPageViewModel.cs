using AppMobileCPM.Integrations.Journey;
using AppMobileCPM.Services;

namespace AppMobileCPM.Areas.Admin.ViewModels;

public sealed class AdminJourneyOperationsDashboardFilterInputModel
{
    public string? BoardType { get; init; }
    public string? SourceChannel { get; init; }
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
}

public sealed class AdminJourneyGovernanceSnapshotViewModel
{
    public bool Enabled { get; init; }
    public int RolloutPercentage { get; init; }
    public string AllowedChannelsLabel { get; init; } = string.Empty;
    public bool IntakeEnabled { get; init; }
    public bool StageAutomationEnabled { get; init; }
    public bool MatchingEnabled { get; init; }
    public bool DispatchEnabled { get; init; }
    public bool ConnectionEnabled { get; init; }
    public bool ClosureEnabled { get; init; }
    public bool RouteOperationalExceptionsToHandoff { get; init; }

    public static AdminJourneyGovernanceSnapshotViewModel FromOptions(JourneyGovernanceOptions options)
    {
        var labels = (options.AllowedSourceChannels ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(AdminKanbanJourneySourceChannels.GetLabel)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        return new AdminJourneyGovernanceSnapshotViewModel
        {
            Enabled = options.Enabled,
            RolloutPercentage = options.RolloutPercentage,
            AllowedChannelsLabel = string.Join(", ", labels),
            IntakeEnabled = options.IntakeEnabled,
            StageAutomationEnabled = options.StageAutomationEnabled,
            MatchingEnabled = options.MatchingEnabled,
            DispatchEnabled = options.DispatchEnabled,
            ConnectionEnabled = options.ConnectionEnabled,
            ClosureEnabled = options.ClosureEnabled,
            RouteOperationalExceptionsToHandoff = options.RouteOperationalExceptionsToHandoff
        };
    }
}

public sealed class AdminJourneyOperationsDashboardPageViewModel
{
    public required string Title { get; init; }
    public required string Subtitle { get; init; }
    public required string SelectedBoardType { get; init; }
    public required string SelectedBoardLabel { get; init; }
    public required string SelectedSourceChannel { get; init; }
    public required string SelectedSourceLabel { get; init; }
    public required string PeriodStartValue { get; init; }
    public required string PeriodEndValue { get; init; }
    public required string PeriodLabel { get; init; }
    public required AdminKanbanJourneyOperationsDashboardSnapshot Snapshot { get; init; }
    public required AdminJourneyGovernanceSnapshotViewModel Governance { get; init; }
    public string DashboardUrl { get; init; } = "/admin";
    public string ClientsBoardUrl { get; init; } = "/admin/funil/clientes";
    public string ProvidersBoardUrl { get; init; } = "/admin/funil/prestadores";
    public string TelegramDashboardUrl { get; init; } = "/admin/telegram/painel";
    public string FiltersClearUrl { get; init; } = "/admin/jornada/painel";

    public bool HasData => Snapshot.TotalJourneys > 0;
}
