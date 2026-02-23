using System.Text.RegularExpressions;
using ConsertaPraMim.Web.Admin.Models;

namespace ConsertaPraMim.Web.Admin.Services;

public sealed class AdminRoadmapService : IAdminRoadmapService
{
    private static readonly Regex EpicHeadingRegex = new(
        @"^(?<id>EPIC-\d+)\s*-\s*(?<title>.+)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex StoryHeadingRegex = new(
        @"^(?<id>ST-\d+)\s*-\s*(?<title>.+)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _hostEnvironment;
    private readonly ILogger<AdminRoadmapService> _logger;

    public AdminRoadmapService(
        IConfiguration configuration,
        IWebHostEnvironment hostEnvironment,
        ILogger<AdminRoadmapService> logger)
    {
        _configuration = configuration;
        _hostEnvironment = hostEnvironment;
        _logger = logger;
    }

    public async Task<AdminRoadmapViewModel> BuildViewModelAsync(
        string? searchTerm,
        string? epicFilter,
        string? trackFilter,
        string? statusFilter,
        CancellationToken cancellationToken = default)
    {
        var rootPath = ResolveAdminPortalDocumentationPath();
        if (!Directory.Exists(rootPath))
        {
            return new AdminRoadmapViewModel
            {
                DocumentationRootPath = rootPath,
                SearchTerm = NormalizeInput(searchTerm),
                EpicFilter = NormalizeInput(epicFilter),
                TrackFilter = NormalizeInput(trackFilter),
                StatusFilter = NormalizeInput(statusFilter),
                ErrorMessage = "Pasta de documentacao ADMIN_PORTAL nao encontrada.",
                GeneratedAtUtc = DateTimeOffset.UtcNow
            };
        }

        var epics = await LoadEpicsAsync(rootPath, cancellationToken);
        var stories = await LoadStoriesAsync(rootPath, cancellationToken);

        var epicById = epics
            .Where(epic => !string.IsNullOrWhiteSpace(epic.EpicId))
            .ToDictionary(epic => epic.EpicId, StringComparer.OrdinalIgnoreCase);

        var storyCards = stories
            .Select(story => BuildStoryCard(story, epicById))
            .ToArray();

        var normalizedSearch = NormalizeInput(searchTerm);
        var normalizedEpic = NormalizeInput(epicFilter);
        var normalizedTrack = NormalizeInput(trackFilter);
        var normalizedStatus = NormalizeStatusFilter(statusFilter);

        var filteredStories = storyCards
            .Where(story => MatchesSearch(story, normalizedSearch))
            .Where(story => string.IsNullOrWhiteSpace(normalizedEpic)
                || story.EpicId.Equals(normalizedEpic, StringComparison.OrdinalIgnoreCase))
            .Where(story => string.IsNullOrWhiteSpace(normalizedTrack)
                || story.Track.Equals(normalizedTrack, StringComparison.OrdinalIgnoreCase))
            .Where(story => string.IsNullOrWhiteSpace(normalizedStatus)
                || story.Status.Equals(normalizedStatus, StringComparison.OrdinalIgnoreCase))
            .OrderBy(story => story.EpicId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(story => story.StoryId, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var filteredEpicIds = filteredStories
            .Select(story => story.EpicId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var epicsForView = epics
            .Where(epic => MatchesSearch(epic, normalizedSearch)
                           || filteredEpicIds.Contains(epic.EpicId)
                           || (string.IsNullOrWhiteSpace(normalizedSearch)
                               && string.IsNullOrWhiteSpace(normalizedEpic)
                               && string.IsNullOrWhiteSpace(normalizedTrack)
                               && string.IsNullOrWhiteSpace(normalizedStatus)))
            .Where(epic => string.IsNullOrWhiteSpace(normalizedEpic)
                           || epic.EpicId.Equals(normalizedEpic, StringComparison.OrdinalIgnoreCase))
            .Where(epic => string.IsNullOrWhiteSpace(normalizedTrack)
                           || epic.Track.Equals(normalizedTrack, StringComparison.OrdinalIgnoreCase))
            .OrderBy(epic => epic.EpicId, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var epicCards = epicsForView
            .Select(epic =>
            {
                var storiesByEpic = storyCards.Where(story => story.EpicId.Equals(epic.EpicId, StringComparison.OrdinalIgnoreCase)).ToArray();
                return new AdminRoadmapEpicCardViewModel
                {
                    EpicId = epic.EpicId,
                    Title = epic.Title,
                    Status = epic.Status,
                    Track = epic.Track,
                    Objective = epic.Objective,
                    StoriesTotal = storiesByEpic.Length,
                    StoriesBacklog = storiesByEpic.Count(story => story.Status.Equals("Backlog", StringComparison.OrdinalIgnoreCase)),
                    StoriesInProgress = storiesByEpic.Count(story => story.Status.Equals("In Progress", StringComparison.OrdinalIgnoreCase)),
                    StoriesDone = storiesByEpic.Count(story => story.Status.Equals("Done", StringComparison.OrdinalIgnoreCase)),
                    WikiRelativePath = epic.WikiRelativePath,
                    LastModifiedUtc = epic.LastModifiedUtc
                };
            })
            .ToArray();

        var epicOptions = epics
            .OrderBy(epic => epic.EpicId, StringComparer.OrdinalIgnoreCase)
            .Select(epic => new AdminRoadmapFilterOptionViewModel
            {
                Value = epic.EpicId,
                Label = $"{epic.EpicId} - {epic.Title}"
            })
            .ToArray();

        var trackOptions = epics
            .Select(epic => epic.Track)
            .Where(track => !string.IsNullOrWhiteSpace(track))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(track => track, StringComparer.OrdinalIgnoreCase)
            .Select(track => new AdminRoadmapFilterOptionViewModel
            {
                Value = track,
                Label = track
            })
            .ToArray();

        var statusOptions = new[]
        {
            new AdminRoadmapFilterOptionViewModel { Value = "Backlog", Label = "Backlog" },
            new AdminRoadmapFilterOptionViewModel { Value = "In Progress", Label = "In Progress" },
            new AdminRoadmapFilterOptionViewModel { Value = "Done", Label = "Done" }
        };

        return new AdminRoadmapViewModel
        {
            DocumentationRootPath = rootPath,
            SearchTerm = normalizedSearch,
            EpicFilter = normalizedEpic,
            TrackFilter = normalizedTrack,
            StatusFilter = normalizedStatus,
            TotalEpics = epics.Count,
            FilteredEpics = epicCards.Length,
            TotalStories = storyCards.Length,
            FilteredStories = filteredStories.Length,
            BacklogStories = filteredStories.Count(story => story.Status.Equals("Backlog", StringComparison.OrdinalIgnoreCase)),
            InProgressStories = filteredStories.Count(story => story.Status.Equals("In Progress", StringComparison.OrdinalIgnoreCase)),
            DoneStories = filteredStories.Count(story => story.Status.Equals("Done", StringComparison.OrdinalIgnoreCase)),
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            Epics = epicCards,
            StoriesBacklog = filteredStories.Where(story => story.Status.Equals("Backlog", StringComparison.OrdinalIgnoreCase)).ToArray(),
            StoriesInProgress = filteredStories.Where(story => story.Status.Equals("In Progress", StringComparison.OrdinalIgnoreCase)).ToArray(),
            StoriesDone = filteredStories.Where(story => story.Status.Equals("Done", StringComparison.OrdinalIgnoreCase)).ToArray(),
            EpicOptions = epicOptions,
            TrackOptions = trackOptions,
            StatusOptions = statusOptions
        };
    }

    private async Task<IReadOnlyList<EpicDescriptor>> LoadEpicsAsync(string rootPath, CancellationToken cancellationToken)
    {
        var epicsPath = Path.Combine(rootPath, "EPICS");
        if (!Directory.Exists(epicsPath))
        {
            return Array.Empty<EpicDescriptor>();
        }

        var files = Directory
            .EnumerateFiles(epicsPath, "EPIC-*.md", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var epics = new List<EpicDescriptor>(files.Length);
        foreach (var file in files)
        {
            var lines = await File.ReadAllLinesAsync(file, cancellationToken);
            var heading = ExtractHeading(lines);
            var parsed = ParseHeading(heading, EpicHeadingRegex, "EPIC", Path.GetFileNameWithoutExtension(file));

            var info = new FileInfo(file);
            epics.Add(new EpicDescriptor
            {
                EpicId = parsed.Id,
                Title = parsed.Title,
                Status = NormalizeEpicStatus(ReadMetadata(lines, "Status") ?? "Backlog"),
                Track = NormalizeTrack(ReadMetadata(lines, "Trilha") ?? "N/A"),
                Objective = ExtractObjective(lines),
                WikiRelativePath = BuildWikiRelativePath("EPICS", Path.GetFileName(file)),
                LastModifiedUtc = new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero)
            });
        }

        return epics;
    }

    private async Task<IReadOnlyList<StoryDescriptor>> LoadStoriesAsync(string rootPath, CancellationToken cancellationToken)
    {
        var storiesRoot = Path.Combine(rootPath, "STORIES");
        if (!Directory.Exists(storiesRoot))
        {
            return Array.Empty<StoryDescriptor>();
        }

        var statusFolders = new[] { "BACKLOG", "IN_PROGRESS", "DONE" };
        var stories = new List<StoryDescriptor>();

        foreach (var folder in statusFolders)
        {
            var folderPath = Path.Combine(storiesRoot, folder);
            if (!Directory.Exists(folderPath))
            {
                continue;
            }

            var files = Directory
                .EnumerateFiles(folderPath, "ST-*.md", SearchOption.TopDirectoryOnly)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            foreach (var file in files)
            {
                var lines = await File.ReadAllLinesAsync(file, cancellationToken);
                var heading = ExtractHeading(lines);
                var parsed = ParseHeading(heading, StoryHeadingRegex, "ST", Path.GetFileNameWithoutExtension(file));
                var taskProgress = CountTasks(lines);
                var info = new FileInfo(file);

                stories.Add(new StoryDescriptor
                {
                    StoryId = parsed.Id,
                    Title = parsed.Title,
                    Status = NormalizeStoryStatus(ReadMetadata(lines, "Status"), folder),
                    EpicId = NormalizeInput(ReadMetadata(lines, "Epic")) ?? string.Empty,
                    Objective = ExtractObjective(lines),
                    TasksDone = taskProgress.TasksDone,
                    TasksTotal = taskProgress.TasksTotal,
                    WikiRelativePath = BuildWikiRelativePath(Path.Combine("STORIES", folder), Path.GetFileName(file)),
                    LastModifiedUtc = new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero)
                });
            }
        }

        return stories;
    }

    private static AdminRoadmapStoryCardViewModel BuildStoryCard(
        StoryDescriptor story,
        IReadOnlyDictionary<string, EpicDescriptor> epicById)
    {
        var epicTitle = string.Empty;
        var track = "N/A";

        if (!string.IsNullOrWhiteSpace(story.EpicId)
            && epicById.TryGetValue(story.EpicId, out var epicDescriptor))
        {
            epicTitle = epicDescriptor.Title;
            track = epicDescriptor.Track;
        }

        return new AdminRoadmapStoryCardViewModel
        {
            StoryId = story.StoryId,
            Title = story.Title,
            Status = story.Status,
            EpicId = story.EpicId,
            EpicTitle = epicTitle,
            Track = track,
            Objective = story.Objective,
            TasksDone = story.TasksDone,
            TasksTotal = story.TasksTotal,
            WikiRelativePath = story.WikiRelativePath,
            LastModifiedUtc = story.LastModifiedUtc
        };
    }

    private static bool MatchesSearch(AdminRoadmapStoryCardViewModel story, string? searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return true;
        }

        return story.StoryId.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
               || story.Title.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
               || story.Status.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
               || story.EpicId.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
               || story.EpicTitle.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
               || story.Track.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
               || story.Objective.Contains(searchTerm, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesSearch(EpicDescriptor epic, string? searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return true;
        }

        return epic.EpicId.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
               || epic.Title.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
               || epic.Status.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
               || epic.Track.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
               || epic.Objective.Contains(searchTerm, StringComparison.OrdinalIgnoreCase);
    }

    private string ResolveAdminPortalDocumentationPath()
    {
        var configured = _configuration["AdminRoadmap:BoardRootPath"]?.Trim();
        if (!string.IsNullOrWhiteSpace(configured))
        {
            var configuredPath = Path.IsPathRooted(configured)
                ? configured
                : Path.GetFullPath(Path.Combine(_hostEnvironment.ContentRootPath, configured));

            if (Directory.Exists(configuredPath))
            {
                return configuredPath;
            }

            _logger.LogWarning(
                "AdminRoadmap:BoardRootPath configurado, mas pasta nao encontrada. Path={ConfiguredPath}",
                configuredPath);
        }

        var current = new DirectoryInfo(_hostEnvironment.ContentRootPath);
        while (current != null)
        {
            var candidate = Path.Combine(current.FullName, "Documentacao", "ADMIN_PORTAL");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        return Path.Combine(_hostEnvironment.ContentRootPath, "Documentacao", "ADMIN_PORTAL");
    }

    private static string ExtractHeading(IReadOnlyList<string> lines)
    {
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            var heading = trimmed.TrimStart('#').Trim();
            if (!string.IsNullOrWhiteSpace(heading))
            {
                return heading;
            }
        }

        return string.Empty;
    }

    private static (string Id, string Title) ParseHeading(string heading, Regex regex, string prefix, string fallback)
    {
        var match = regex.Match(heading);
        if (match.Success)
        {
            return (
                Id: NormalizeInput(match.Groups["id"].Value) ?? fallback,
                Title: NormalizeInput(match.Groups["title"].Value) ?? fallback);
        }

        var fallbackId = NormalizeInput(ExtractIdFromFallback(fallback, prefix)) ?? fallback;
        return (fallbackId, heading);
    }

    private static string ExtractIdFromFallback(string fallback, string prefix)
    {
        var token = fallback.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (token.Length < 2)
        {
            return fallback;
        }

        var id = $"{prefix}-{token[1]}";
        return id.ToUpperInvariant();
    }

    private static string? ReadMetadata(IReadOnlyList<string> lines, string key)
    {
        var prefix = key + ":";
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return NormalizeInput(trimmed[prefix.Length..]);
        }

        return null;
    }

    private static string ExtractObjective(IReadOnlyList<string> lines)
    {
        var startIndex = -1;
        for (var index = 0; index < lines.Count; index++)
        {
            if (lines[index].Trim().Equals("## Objetivo", StringComparison.OrdinalIgnoreCase))
            {
                startIndex = index + 1;
                break;
            }
        }

        if (startIndex < 0)
        {
            return "Sem objetivo descrito.";
        }

        var chunks = new List<string>();
        for (var index = startIndex; index < lines.Count; index++)
        {
            var line = lines[index].Trim();
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var normalized = line.TrimStart('-', '*').Trim();
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                chunks.Add(normalized);
            }
        }

        if (chunks.Count == 0)
        {
            return "Sem objetivo descrito.";
        }

        var value = string.Join(' ', chunks);
        if (value.Length <= 260)
        {
            return value;
        }

        return value[..257] + "...";
    }

    private static (int TasksDone, int TasksTotal) CountTasks(IReadOnlyList<string> lines)
    {
        var done = 0;
        var total = 0;

        foreach (var line in lines)
        {
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("- [x]", StringComparison.OrdinalIgnoreCase))
            {
                done++;
                total++;
                continue;
            }

            if (trimmed.StartsWith("- [ ]", StringComparison.OrdinalIgnoreCase))
            {
                total++;
            }
        }

        return (done, total);
    }

    private static string NormalizeEpicStatus(string status)
    {
        var normalized = NormalizeInput(status) ?? "Backlog";
        if (normalized.Equals("Done", StringComparison.OrdinalIgnoreCase))
        {
            return "Done";
        }

        if (normalized.Equals("In Progress", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("InProgress", StringComparison.OrdinalIgnoreCase))
        {
            return "In Progress";
        }

        return normalized;
    }

    private static string NormalizeStoryStatus(string? status, string folder)
    {
        var normalized = (NormalizeInput(status) ?? string.Empty).ToLowerInvariant();

        if (normalized.Contains("done", StringComparison.Ordinal)
            || normalized.Contains("conclu", StringComparison.Ordinal)
            || folder.Equals("DONE", StringComparison.OrdinalIgnoreCase))
        {
            return "Done";
        }

        if (normalized.Contains("progress", StringComparison.Ordinal)
            || normalized.Equals("in_progress", StringComparison.Ordinal)
            || folder.Equals("IN_PROGRESS", StringComparison.OrdinalIgnoreCase))
        {
            return "In Progress";
        }

        return "Backlog";
    }

    private static string NormalizeTrack(string value)
    {
        return NormalizeInput(value)?.ToUpperInvariant() ?? "N/A";
    }

    private static string? NormalizeStatusFilter(string? value)
    {
        var normalized = NormalizeInput(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return NormalizeStoryStatus(normalized, string.Empty);
    }

    private static string? NormalizeInput(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private static string BuildWikiRelativePath(string segment, string fileName)
    {
        var joined = Path.Combine("ADMIN_PORTAL", segment, fileName);
        return joined.Replace('\\', '/');
    }

    private sealed class EpicDescriptor
    {
        public string EpicId { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public string Track { get; init; } = string.Empty;
        public string Objective { get; init; } = string.Empty;
        public string WikiRelativePath { get; init; } = string.Empty;
        public DateTimeOffset LastModifiedUtc { get; init; }
    }

    private sealed class StoryDescriptor
    {
        public string StoryId { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public string EpicId { get; init; } = string.Empty;
        public string Objective { get; init; } = string.Empty;
        public int TasksDone { get; init; }
        public int TasksTotal { get; init; }
        public string WikiRelativePath { get; init; } = string.Empty;
        public DateTimeOffset LastModifiedUtc { get; init; }
    }
}
