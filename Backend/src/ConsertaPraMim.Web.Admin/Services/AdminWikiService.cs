using System.IO;
using System.Text;
using ConsertaPraMim.Web.Admin.Models;
using Markdig;
using Microsoft.Extensions.Caching.Memory;

namespace ConsertaPraMim.Web.Admin.Services;

public sealed class AdminWikiService : IAdminWikiService
{
    private const string CatalogCacheKey = "admin:wiki:catalog";
    private static readonly TimeSpan CatalogCacheDuration = TimeSpan.FromSeconds(30);
    private static readonly MarkdownPipeline MarkdownPipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .DisableHtml()
        .Build();

    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _hostEnvironment;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<AdminWikiService> _logger;

    public AdminWikiService(
        IConfiguration configuration,
        IWebHostEnvironment hostEnvironment,
        IMemoryCache memoryCache,
        ILogger<AdminWikiService> logger)
    {
        _configuration = configuration;
        _hostEnvironment = hostEnvironment;
        _memoryCache = memoryCache;
        _logger = logger;
    }

    public async Task<AdminWikiViewModel> BuildViewModelAsync(string? selectedDocumentPath, CancellationToken cancellationToken = default)
    {
        var catalog = BuildCatalog();
        if (catalog.Documents.Count == 0)
        {
            return new AdminWikiViewModel
            {
                DocumentationRootPath = catalog.RootPath,
                SelectedDocumentPath = null,
                ErrorMessage = "Nenhum arquivo markdown encontrado na documentacao.",
                TotalDocuments = 0,
                GeneratedAtUtc = DateTimeOffset.UtcNow
            };
        }

        var sections = catalog.Documents
            .GroupBy(x => x.Section, StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new AdminWikiSectionViewModel
            {
                Name = group.Key,
                Documents = group
                    .OrderBy(item => item.RelativePath, StringComparer.OrdinalIgnoreCase)
                    .Select(item => new AdminWikiDocumentListItemViewModel
                    {
                        RelativePath = item.RelativePath,
                        Title = item.Title,
                        FileName = item.FileName,
                        LastModifiedUtc = item.LastModifiedUtc
                    })
                    .ToArray()
            })
            .ToArray();

        var normalizedSelected = NormalizeRelativePath(selectedDocumentPath);
        WikiDocumentDescriptor? selectedDescriptor = null;
        string? errorMessage = null;

        if (!string.IsNullOrWhiteSpace(normalizedSelected))
        {
            selectedDescriptor = catalog.Documents.FirstOrDefault(x =>
                x.RelativePath.Equals(normalizedSelected, StringComparison.OrdinalIgnoreCase));

            if (selectedDescriptor == null)
            {
                errorMessage = $"Documento nao encontrado: {normalizedSelected}";
            }
        }

        selectedDescriptor ??= catalog.Documents[0];
        var selectedDocument = await LoadDocumentAsync(selectedDescriptor, cancellationToken);

        return new AdminWikiViewModel
        {
            DocumentationRootPath = catalog.RootPath,
            SelectedDocumentPath = selectedDescriptor.RelativePath,
            ErrorMessage = errorMessage,
            TotalDocuments = catalog.Documents.Count,
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            SelectedDocument = selectedDocument,
            Sections = sections
        };
    }

    private AdminWikiCatalog BuildCatalog()
    {
        if (_memoryCache.TryGetValue(CatalogCacheKey, out AdminWikiCatalog? cached) && cached != null)
        {
            return cached;
        }

        var rootPath = ResolveDocumentationRootPath();
        if (!Directory.Exists(rootPath))
        {
            _logger.LogWarning("Wiki docs root path nao encontrado: {RootPath}", rootPath);
            var emptyCatalog = new AdminWikiCatalog(rootPath, []);
            _memoryCache.Set(CatalogCacheKey, emptyCatalog, TimeSpan.FromSeconds(10));
            return emptyCatalog;
        }

        var files = Directory
            .EnumerateFiles(rootPath, "*.md", SearchOption.AllDirectories)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var documents = new List<WikiDocumentDescriptor>(files.Length);
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

            documents.Add(new WikiDocumentDescriptor(
                RelativePath: normalizedRelativePath,
                FullPath: file,
                Title: title,
                FileName: fileName,
                Section: section,
                LastModifiedUtc: new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero),
                FileSizeBytes: info.Length));
        }

        var catalog = new AdminWikiCatalog(rootPath, documents);
        _memoryCache.Set(CatalogCacheKey, catalog, CatalogCacheDuration);
        return catalog;
    }

    private async Task<AdminWikiDocumentViewModel> LoadDocumentAsync(WikiDocumentDescriptor descriptor, CancellationToken cancellationToken)
    {
        var markdown = await File.ReadAllTextAsync(descriptor.FullPath, Encoding.UTF8, cancellationToken);
        var html = Markdown.ToHtml(markdown, MarkdownPipeline);

        return new AdminWikiDocumentViewModel
        {
            RelativePath = descriptor.RelativePath,
            Title = descriptor.Title,
            HtmlContent = html,
            LastModifiedUtc = descriptor.LastModifiedUtc,
            FileSizeBytes = descriptor.FileSizeBytes
        };
    }

    private string ResolveDocumentationRootPath()
    {
        var configured = _configuration["AdminWiki:DocumentationRootPath"]?.Trim();
        if (!string.IsNullOrWhiteSpace(configured))
        {
            var configuredPath = Path.IsPathRooted(configured)
                ? configured
                : Path.GetFullPath(Path.Combine(_hostEnvironment.ContentRootPath, configured));

            if (Directory.Exists(configuredPath))
            {
                if (ContainsMarkdownFiles(configuredPath))
                {
                    return configuredPath;
                }

                _logger.LogWarning(
                    "AdminWiki:DocumentationRootPath configurado sem arquivos markdown. Path={ConfiguredPath}",
                    configuredPath);
            }
        }

        var current = new DirectoryInfo(_hostEnvironment.ContentRootPath);
        while (current != null)
        {
            var candidate = Path.Combine(current.FullName, "Documentacao");
            if (Directory.Exists(candidate) && ContainsMarkdownFiles(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        return Path.Combine(_hostEnvironment.ContentRootPath, "Documentacao");
    }

    private static bool ContainsMarkdownFiles(string rootPath)
    {
        return Directory
            .EnumerateFiles(rootPath, "*.md", SearchOption.AllDirectories)
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

        for (var i = 0; i < 80; i++)
        {
            var line = reader.ReadLine();
            if (line == null)
            {
                break;
            }

            var trimmed = line.Trim();
            if (!trimmed.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            var title = trimmed.TrimStart('#').Trim();
            if (!string.IsNullOrWhiteSpace(title))
            {
                return title;
            }
        }

        return null;
    }

    private sealed record AdminWikiCatalog(
        string RootPath,
        IReadOnlyList<WikiDocumentDescriptor> Documents);

    private sealed record WikiDocumentDescriptor(
        string RelativePath,
        string FullPath,
        string Title,
        string FileName,
        string Section,
        DateTimeOffset LastModifiedUtc,
        long FileSizeBytes);
}
