namespace ConsertaPraMim.Web.Admin.Models;

public sealed class AdminDiagramsViewModel
{
    public string DiagramsRootPath { get; init; } = string.Empty;
    public string? SelectedDiagramPath { get; init; }
    public string? ErrorMessage { get; init; }
    public int TotalDiagrams { get; init; }
    public DateTimeOffset GeneratedAtUtc { get; init; }
    public AdminDiagramDocumentViewModel? SelectedDiagram { get; init; }
    public IReadOnlyList<AdminDiagramSectionViewModel> Sections { get; init; } = Array.Empty<AdminDiagramSectionViewModel>();
}

public sealed class AdminDiagramSectionViewModel
{
    public string Name { get; init; } = string.Empty;
    public IReadOnlyList<AdminDiagramListItemViewModel> Diagrams { get; init; } = Array.Empty<AdminDiagramListItemViewModel>();
}

public sealed class AdminDiagramListItemViewModel
{
    public string RelativePath { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public DateTimeOffset LastModifiedUtc { get; init; }
}

public sealed class AdminDiagramDocumentViewModel
{
    public string RelativePath { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string SourceContent { get; init; } = string.Empty;
    public DateTimeOffset LastModifiedUtc { get; init; }
    public long FileSizeBytes { get; init; }
}
