using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using ConsertaPraMim.Web.Admin.Models;
using Microsoft.Extensions.Caching.Memory;

namespace ConsertaPraMim.Web.Admin.Services;

public sealed class AdminDiagramsService : IAdminDiagramsService
{
    private const string CatalogCacheKey = "admin:diagrams:catalog";
    private static readonly TimeSpan CatalogCacheDuration = TimeSpan.FromSeconds(30);
    private static readonly Regex DiagramTitleRegex = new(@"^\s*title\s+(?<title>.+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _hostEnvironment;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<AdminDiagramsService> _logger;

    public AdminDiagramsService(
        IConfiguration configuration,
        IWebHostEnvironment hostEnvironment,
        IMemoryCache memoryCache,
        ILogger<AdminDiagramsService> logger)
    {
        _configuration = configuration;
        _hostEnvironment = hostEnvironment;
        _memoryCache = memoryCache;
        _logger = logger;
    }

    public async Task<AdminDiagramsViewModel> BuildViewModelAsync(string? selectedDiagramPath, CancellationToken cancellationToken = default)
    {
        var catalog = BuildCatalog();
        if (catalog.Diagrams.Count == 0)
        {
            return new AdminDiagramsViewModel
            {
                DiagramsRootPath = catalog.RootPath,
                SelectedDiagramPath = null,
                ErrorMessage = "Nenhum arquivo Mermaid (.mmd) encontrado em Documentacao/DIAGRAMAS.",
                TotalDiagrams = 0,
                GeneratedAtUtc = DateTimeOffset.UtcNow
            };
        }

        var sections = catalog.Diagrams
            .GroupBy(x => x.Section, StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new AdminDiagramSectionViewModel
            {
                Name = group.Key,
                Diagrams = group
                    .OrderBy(item => item.RelativePath, StringComparer.OrdinalIgnoreCase)
                    .Select(item => new AdminDiagramListItemViewModel
                    {
                        RelativePath = item.RelativePath,
                        Title = item.Title,
                        FileName = item.FileName,
                        LastModifiedUtc = item.LastModifiedUtc
                    })
                    .ToArray()
            })
            .ToArray();

        var normalizedSelected = NormalizeRelativePath(selectedDiagramPath);
        DiagramDescriptor? selectedDescriptor = null;
        string? errorMessage = null;

        if (!string.IsNullOrWhiteSpace(normalizedSelected))
        {
            selectedDescriptor = catalog.Diagrams.FirstOrDefault(x =>
                x.RelativePath.Equals(normalizedSelected, StringComparison.OrdinalIgnoreCase));

            if (selectedDescriptor == null)
            {
                errorMessage = $"Diagrama nao encontrado: {normalizedSelected}";
            }
        }

        selectedDescriptor ??= catalog.Diagrams[0];
        var selectedDiagram = await LoadDiagramAsync(selectedDescriptor, cancellationToken);

        return new AdminDiagramsViewModel
        {
            DiagramsRootPath = catalog.RootPath,
            SelectedDiagramPath = selectedDescriptor.RelativePath,
            ErrorMessage = errorMessage,
            TotalDiagrams = catalog.Diagrams.Count,
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            SelectedDiagram = selectedDiagram,
            Sections = sections
        };
    }

    private AdminDiagramCatalog BuildCatalog()
    {
        if (_memoryCache.TryGetValue(CatalogCacheKey, out AdminDiagramCatalog? cached) && cached != null)
        {
            return cached;
        }

        var rootPath = ResolveDiagramsRootPath();
        if (!Directory.Exists(rootPath))
        {
            _logger.LogWarning("Diagrams root path nao encontrado: {RootPath}", rootPath);
            var emptyCatalog = new AdminDiagramCatalog(rootPath, []);
            _memoryCache.Set(CatalogCacheKey, emptyCatalog, TimeSpan.FromSeconds(10));
            return emptyCatalog;
        }

        var files = Directory
            .EnumerateFiles(rootPath, "*.mmd", SearchOption.AllDirectories)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var diagrams = new List<DiagramDescriptor>(files.Length);
        foreach (var file in files)
        {
            var relativePath = Path.GetRelativePath(rootPath, file);
            var normalizedRelativePath = NormalizeRelativePath(relativePath) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedRelativePath))
            {
                continue;
            }

            var section = ResolveSectionName(normalizedRelativePath);
            var fileName = Path.GetFileName(file);
            var title = ExtractTitle(file) ?? Path.GetFileNameWithoutExtension(fileName);
            var info = new FileInfo(file);

            diagrams.Add(new DiagramDescriptor(
                RelativePath: normalizedRelativePath,
                FullPath: file,
                Title: title,
                FileName: fileName,
                Section: section,
                LastModifiedUtc: new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero),
                FileSizeBytes: info.Length));
        }

        var catalog = new AdminDiagramCatalog(rootPath, diagrams);
        _memoryCache.Set(CatalogCacheKey, catalog, CatalogCacheDuration);
        return catalog;
    }

    private async Task<AdminDiagramDocumentViewModel> LoadDiagramAsync(DiagramDescriptor descriptor, CancellationToken cancellationToken)
    {
        var source = await File.ReadAllTextAsync(descriptor.FullPath, Encoding.UTF8, cancellationToken);

        return new AdminDiagramDocumentViewModel
        {
            RelativePath = descriptor.RelativePath,
            Title = descriptor.Title,
            SourceContent = source,
            LastModifiedUtc = descriptor.LastModifiedUtc,
            FileSizeBytes = descriptor.FileSizeBytes
        };
    }

    private string ResolveDiagramsRootPath()
    {
        var configured = _configuration["AdminWiki:DiagramsRootPath"]?.Trim();
        if (!string.IsNullOrWhiteSpace(configured))
        {
            var configuredPath = Path.IsPathRooted(configured)
                ? configured
                : Path.GetFullPath(Path.Combine(_hostEnvironment.ContentRootPath, configured));

            if (Directory.Exists(configuredPath) && ContainsMermaidFiles(configuredPath))
            {
                return configuredPath;
            }

            _logger.LogWarning(
                "AdminWiki:DiagramsRootPath configurado, mas path inexistente ou sem .mmd. Path={ConfiguredPath}",
                configuredPath);
        }

        var current = new DirectoryInfo(_hostEnvironment.ContentRootPath);
        while (current != null)
        {
            var candidate = Path.Combine(current.FullName, "Documentacao", "DIAGRAMAS");
            if (Directory.Exists(candidate) && ContainsMermaidFiles(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        return Path.Combine(_hostEnvironment.ContentRootPath, "Documentacao", "DIAGRAMAS");
    }

    private static bool ContainsMermaidFiles(string rootPath)
    {
        return Directory
            .EnumerateFiles(rootPath, "*.mmd", SearchOption.AllDirectories)
            .Take(1)
            .Any();
    }

    private static string? NormalizeRelativePath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value
            .Replace('\\', '/')
            .Trim()
            .TrimStart('/');

        if (normalized.Contains("..", StringComparison.Ordinal))
        {
            return null;
        }

        return normalized;
    }

    private static string ResolveSectionName(string relativePath)
    {
        var firstSeparator = relativePath.IndexOf('/', StringComparison.Ordinal);
        if (firstSeparator <= 0)
        {
            return "RAIZ";
        }

        return relativePath[..firstSeparator].ToUpperInvariant();
    }

    private static string? ExtractTitle(string filePath)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        for (var i = 0; i < 120; i++)
        {
            var line = reader.ReadLine();
            if (line == null)
            {
                break;
            }

            var titleMatch = DiagramTitleRegex.Match(line);
            if (titleMatch.Success)
            {
                return titleMatch.Groups["title"].Value.Trim();
            }
        }

        return null;
    }

    private sealed record AdminDiagramCatalog(
        string RootPath,
        IReadOnlyList<DiagramDescriptor> Diagrams);

    private sealed record DiagramDescriptor(
        string RelativePath,
        string FullPath,
        string Title,
        string FileName,
        string Section,
        DateTimeOffset LastModifiedUtc,
        long FileSizeBytes);
}
