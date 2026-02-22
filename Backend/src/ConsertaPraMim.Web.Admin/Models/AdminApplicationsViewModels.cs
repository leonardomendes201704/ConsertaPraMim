namespace ConsertaPraMim.Web.Admin.Models;

public sealed class AdminApplicationsViewModel
{
    public string FileserverBaseUrl { get; init; } = string.Empty;
    public IReadOnlyList<AdminApplicationCardViewModel> Applications { get; init; } = Array.Empty<AdminApplicationCardViewModel>();
    public DateTimeOffset? LatestPublishedAtUtc { get; init; }
}

public sealed class AdminApplicationCardViewModel
{
    public string AppName { get; init; } = string.Empty;
    public string Variant { get; init; } = string.Empty;
    public string RelativePath { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public string DownloadUrl { get; init; } = string.Empty;
    public bool IsDebug { get; init; }
    public DateTimeOffset? LastPublishedAtUtc { get; set; }
}
