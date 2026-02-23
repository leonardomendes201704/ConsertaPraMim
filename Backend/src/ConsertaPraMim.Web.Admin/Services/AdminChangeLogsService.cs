using System.Globalization;
using System.Text.RegularExpressions;
using ConsertaPraMim.Web.Admin.Models;

namespace ConsertaPraMim.Web.Admin.Services;

public sealed class AdminChangeLogsService : IAdminChangeLogsService
{
    private static readonly Regex EntryHeaderRegex = new(
        @"^- \[(?<date>\d{4}-\d{2}-\d{2})\]\s+\[(?<story>[^\]]+)\]\s+(?<title>.+)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _hostEnvironment;
    private readonly ILogger<AdminChangeLogsService> _logger;

    public AdminChangeLogsService(
        IConfiguration configuration,
        IWebHostEnvironment hostEnvironment,
        ILogger<AdminChangeLogsService> logger)
    {
        _configuration = configuration;
        _hostEnvironment = hostEnvironment;
        _logger = logger;
    }

    public async Task<AdminChangeLogsViewModel> BuildViewModelAsync(
        string? searchTerm,
        DateOnly? fromDate,
        DateOnly? toDate,
        CancellationToken cancellationToken = default)
    {
        var changelogPath = ResolveChangelogPath();
        if (!File.Exists(changelogPath))
        {
            return new AdminChangeLogsViewModel
            {
                ChangelogFilePath = changelogPath,
                SearchTerm = searchTerm,
                FromDate = fromDate,
                ToDate = toDate,
                ErrorMessage = "Arquivo CHANGELOG.md nao encontrado.",
                TotalEntries = 0,
                FilteredEntries = 0,
                GeneratedAtUtc = DateTimeOffset.UtcNow
            };
        }

        var normalizedSearchTerm = string.IsNullOrWhiteSpace(searchTerm)
            ? null
            : searchTerm.Trim();

        if (fromDate.HasValue && toDate.HasValue && fromDate.Value > toDate.Value)
        {
            (fromDate, toDate) = (toDate, fromDate);
        }

        var lines = await File.ReadAllLinesAsync(changelogPath, cancellationToken);
        var allEntries = ParseEntries(lines);
        var filteredEntries = ApplyFilters(allEntries, normalizedSearchTerm, fromDate, toDate);

        return new AdminChangeLogsViewModel
        {
            ChangelogFilePath = changelogPath,
            SearchTerm = normalizedSearchTerm,
            FromDate = fromDate,
            ToDate = toDate,
            TotalEntries = allEntries.Count,
            FilteredEntries = filteredEntries.Count,
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            Entries = filteredEntries
        };
    }

    private IReadOnlyList<AdminChangeLogEntryViewModel> ParseEntries(IReadOnlyList<string> lines)
    {
        var entries = new List<AdminChangeLogEntryViewModel>();
        var section = "Sem secao";
        var index = 0;

        while (index < lines.Count)
        {
            var rawLine = lines[index];
            var line = rawLine.Trim();

            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                section = line[3..].Trim();
                index++;
                continue;
            }

            var match = EntryHeaderRegex.Match(line);
            if (!match.Success)
            {
                index++;
                continue;
            }

            var detailLines = new List<string>();
            index++;

            while (index < lines.Count)
            {
                var nextLine = lines[index].Trim();
                if (EntryHeaderRegex.IsMatch(nextLine) || nextLine.StartsWith("## ", StringComparison.Ordinal))
                {
                    break;
                }

                if (!string.IsNullOrWhiteSpace(nextLine))
                {
                    detailLines.Add(nextLine);
                }

                index++;
            }

            if (!DateOnly.TryParseExact(
                match.Groups["date"].Value,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date))
            {
                continue;
            }

            entries.Add(new AdminChangeLogEntryViewModel
            {
                Date = date,
                StoryId = match.Groups["story"].Value.Trim(),
                Title = match.Groups["title"].Value.Trim(),
                Type = ExtractField(detailLines, "Tipo"),
                Summary = ExtractField(detailLines, "Resumo"),
                MainFiles = ExtractField(detailLines, "Arquivos principais"),
                RiskImpact = ExtractField(detailLines, "Risco/Impacto"),
                Section = section
            });
        }

        return entries;
    }

    private static IReadOnlyList<AdminChangeLogEntryViewModel> ApplyFilters(
        IReadOnlyList<AdminChangeLogEntryViewModel> entries,
        string? searchTerm,
        DateOnly? fromDate,
        DateOnly? toDate)
    {
        IEnumerable<AdminChangeLogEntryViewModel> query = entries;

        if (fromDate.HasValue)
        {
            query = query.Where(entry => entry.Date >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(entry => entry.Date <= toDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(entry => ContainsSearchTerm(entry, searchTerm));
        }

        return query.ToArray();
    }

    private static bool ContainsSearchTerm(AdminChangeLogEntryViewModel entry, string searchTerm)
    {
        return entry.Title.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
               || entry.StoryId.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
               || entry.Type.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
               || entry.Summary.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
               || entry.MainFiles.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
               || entry.RiskImpact.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
               || entry.Section.Contains(searchTerm, StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractField(IReadOnlyList<string> lines, string fieldName)
    {
        var prefix = $"{fieldName}:";
        foreach (var line in lines)
        {
            if (!line.StartsWith("-", StringComparison.Ordinal))
            {
                continue;
            }

            var normalizedLine = line.TrimStart('-').Trim();
            if (!normalizedLine.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return normalizedLine[prefix.Length..].Trim();
        }

        return string.Empty;
    }

    private string ResolveChangelogPath()
    {
        var configured = _configuration["AdminWiki:ChangelogFilePath"]?.Trim();
        if (!string.IsNullOrWhiteSpace(configured))
        {
            var configuredPath = Path.IsPathRooted(configured)
                ? configured
                : Path.GetFullPath(Path.Combine(_hostEnvironment.ContentRootPath, configured));

            if (File.Exists(configuredPath))
            {
                return configuredPath;
            }

            _logger.LogWarning(
                "AdminWiki:ChangelogFilePath configurado, mas arquivo nao encontrado. Path={ConfiguredPath}",
                configuredPath);
        }

        var current = new DirectoryInfo(_hostEnvironment.ContentRootPath);
        while (current != null)
        {
            var candidate = Path.Combine(current.FullName, "Documentacao", "ADMIN_PORTAL", "CHANGELOG", "CHANGELOG.md");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        return Path.Combine(_hostEnvironment.ContentRootPath, "Documentacao", "ADMIN_PORTAL", "CHANGELOG", "CHANGELOG.md");
    }
}
