namespace AppMobileCPM.Services;

public sealed class AdminSiteContentRecord
{
    public int Id { get; init; }
    public required string Key { get; init; }
    public required string Value { get; init; }
    public string Description { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public sealed class AdminSiteContentUpsertRequest
{
    public required string Key { get; init; }
    public required string Value { get; init; }
    public string Description { get; init; } = string.Empty;
    public bool IsActive { get; init; }
}
