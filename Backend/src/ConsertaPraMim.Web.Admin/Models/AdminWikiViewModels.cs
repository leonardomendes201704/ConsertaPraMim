namespace ConsertaPraMim.Web.Admin.Models;

public sealed class AdminWikiViewModel
{
    public string DocumentationRootPath { get; init; } = string.Empty;
    public string? SelectedDocumentPath { get; init; }
    public string? ErrorMessage { get; init; }
    public int TotalDocuments { get; init; }
    public DateTimeOffset GeneratedAtUtc { get; init; }
    public AdminWikiDocumentViewModel? SelectedDocument { get; init; }
    public IReadOnlyList<AdminWikiSectionViewModel> Sections { get; init; } = Array.Empty<AdminWikiSectionViewModel>();
}

public sealed class AdminWikiSectionViewModel
{
    public string Name { get; init; } = string.Empty;
    public IReadOnlyList<AdminWikiDocumentListItemViewModel> Documents { get; init; } = Array.Empty<AdminWikiDocumentListItemViewModel>();
}

public sealed class AdminWikiDocumentListItemViewModel
{
    public string RelativePath { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public DateTimeOffset LastModifiedUtc { get; init; }
}

public sealed class AdminWikiDocumentViewModel
{
    public string RelativePath { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string HtmlContent { get; init; } = string.Empty;
    public DateTimeOffset LastModifiedUtc { get; init; }
    public long FileSizeBytes { get; init; }
}
