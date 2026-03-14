namespace AppMobileCPM.Areas.Admin.ViewModels;

public sealed class AdminSiteContentListItemViewModel
{
    public int Id { get; init; }
    public required string FriendlyName { get; init; }
    public required string Key { get; init; }
    public required string Value { get; init; }
    public required string ValuePreview { get; init; }
    public string Description { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}
