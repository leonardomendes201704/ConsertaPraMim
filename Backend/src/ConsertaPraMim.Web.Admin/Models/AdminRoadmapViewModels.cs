namespace ConsertaPraMim.Web.Admin.Models;

public sealed class AdminRoadmapViewModel
{
    public string DocumentationRootPath { get; init; } = string.Empty;
    public string? SearchTerm { get; init; }
    public string? EpicFilter { get; init; }
    public string? TrackFilter { get; init; }
    public string? StatusFilter { get; init; }
    public int TotalEpics { get; init; }
    public int FilteredEpics { get; init; }
    public int TotalStories { get; init; }
    public int FilteredStories { get; init; }
    public int BacklogStories { get; init; }
    public int InProgressStories { get; init; }
    public int DoneStories { get; init; }
    public DateTimeOffset GeneratedAtUtc { get; init; }
    public string? ErrorMessage { get; init; }
    public IReadOnlyList<AdminRoadmapEpicCardViewModel> Epics { get; init; } = Array.Empty<AdminRoadmapEpicCardViewModel>();
    public IReadOnlyList<AdminRoadmapStoryCardViewModel> StoriesBacklog { get; init; } = Array.Empty<AdminRoadmapStoryCardViewModel>();
    public IReadOnlyList<AdminRoadmapStoryCardViewModel> StoriesInProgress { get; init; } = Array.Empty<AdminRoadmapStoryCardViewModel>();
    public IReadOnlyList<AdminRoadmapStoryCardViewModel> StoriesDone { get; init; } = Array.Empty<AdminRoadmapStoryCardViewModel>();
    public IReadOnlyList<AdminRoadmapFilterOptionViewModel> EpicOptions { get; init; } = Array.Empty<AdminRoadmapFilterOptionViewModel>();
    public IReadOnlyList<AdminRoadmapFilterOptionViewModel> TrackOptions { get; init; } = Array.Empty<AdminRoadmapFilterOptionViewModel>();
    public IReadOnlyList<AdminRoadmapFilterOptionViewModel> StatusOptions { get; init; } = Array.Empty<AdminRoadmapFilterOptionViewModel>();
}

public sealed class AdminRoadmapEpicCardViewModel
{
    public string EpicId { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Track { get; init; } = string.Empty;
    public string Objective { get; init; } = string.Empty;
    public int StoriesTotal { get; init; }
    public int StoriesBacklog { get; init; }
    public int StoriesInProgress { get; init; }
    public int StoriesDone { get; init; }
    public string WikiRelativePath { get; init; } = string.Empty;
    public DateTimeOffset LastModifiedUtc { get; init; }
}

public sealed class AdminRoadmapStoryCardViewModel
{
    public string StoryId { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string EpicId { get; init; } = string.Empty;
    public string EpicTitle { get; init; } = string.Empty;
    public string Track { get; init; } = string.Empty;
    public string Objective { get; init; } = string.Empty;
    public int TasksDone { get; init; }
    public int TasksTotal { get; init; }
    public string WikiRelativePath { get; init; } = string.Empty;
    public DateTimeOffset LastModifiedUtc { get; init; }
}

public sealed class AdminRoadmapFilterOptionViewModel
{
    public string Value { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
}
