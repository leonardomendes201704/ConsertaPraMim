namespace AppMobileCPM.Services;

public sealed class AdminSupportFaqRecord
{
    public int Id { get; init; }
    public required string Question { get; init; }
    public required string Answer { get; init; }
    public bool IsActive { get; init; }
    public int SortOrder { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public sealed class AdminSupportFaqUpsertRequest
{
    public required string Question { get; init; }
    public required string Answer { get; init; }
    public bool IsActive { get; init; }
    public int SortOrder { get; init; }
}
