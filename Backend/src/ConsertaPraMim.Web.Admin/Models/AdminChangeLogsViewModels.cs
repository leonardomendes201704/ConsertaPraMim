namespace ConsertaPraMim.Web.Admin.Models;

public sealed class AdminChangeLogsViewModel
{
    public string ChangelogFilePath { get; init; } = string.Empty;
    public string? SearchTerm { get; init; }
    public DateOnly? FromDate { get; init; }
    public DateOnly? ToDate { get; init; }
    public int TotalEntries { get; init; }
    public int FilteredEntries { get; init; }
    public DateTimeOffset GeneratedAtUtc { get; init; }
    public string? ErrorMessage { get; init; }
    public IReadOnlyList<AdminChangeLogEntryViewModel> Entries { get; init; } = Array.Empty<AdminChangeLogEntryViewModel>();
}

public sealed class AdminChangeLogEntryViewModel
{
    public DateOnly Date { get; init; }
    public string StoryId { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public string MainFiles { get; init; } = string.Empty;
    public string RiskImpact { get; init; } = string.Empty;
    public string Section { get; init; } = string.Empty;
}
