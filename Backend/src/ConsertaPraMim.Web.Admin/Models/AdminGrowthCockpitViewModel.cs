using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Web.Admin.Models;

public sealed class AdminGrowthCockpitViewModel
{
    public AdminGrowthCockpitFilterModel Filters { get; set; } = new();
    public AdminGrowthExecutiveCockpitDto? Cockpit { get; set; }
    public AdminGrowthWeeklyRitualSnapshotDto? WeeklyRitualSnapshot { get; set; }
    public AdminGrowthRoadmapSnapshotViewModel? RoadmapSnapshot { get; set; }
    public string? WeeklyRitualErrorMessage { get; set; }
    public string? RoadmapErrorMessage { get; set; }
    public string? WeeklyRitualFeedbackMessage { get; set; }
    public bool WeeklyRitualFeedbackSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
}

public sealed class AdminGrowthCockpitFilterModel
{
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public string? Category { get; set; }
    public string? City { get; set; }
    public int ProposalSlaMinutes { get; set; } = 30;
    public int AcceptanceSlaHours { get; set; } = 24;
    public int NorthStarResolutionHours { get; set; } = 72;
}

public sealed class AdminGrowthRoadmapSnapshotViewModel
{
    public int TotalStories { get; init; }
    public int BacklogStories { get; init; }
    public int InProgressStories { get; init; }
    public int DoneStories { get; init; }
    public double DeliveryRatePercent { get; init; }
    public double InProgressRatePercent { get; init; }
    public IReadOnlyList<AdminGrowthRoadmapStorySummaryViewModel> PriorityStories { get; init; }
        = Array.Empty<AdminGrowthRoadmapStorySummaryViewModel>();
}

public sealed class AdminGrowthRoadmapStorySummaryViewModel
{
    public string StoryId { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string EpicId { get; init; } = string.Empty;
    public string Track { get; init; } = string.Empty;
    public int TasksDone { get; init; }
    public int TasksTotal { get; init; }
    public string WikiRelativePath { get; init; } = string.Empty;
}
