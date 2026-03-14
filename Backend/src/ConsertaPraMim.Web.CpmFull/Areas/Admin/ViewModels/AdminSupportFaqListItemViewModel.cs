namespace AppMobileCPM.Areas.Admin.ViewModels;

public sealed class AdminSupportFaqListItemViewModel
{
    public int Id { get; init; }
    public required string Question { get; init; }
    public required string Answer { get; init; }
    public required string AnswerPreview { get; init; }
    public bool IsActive { get; init; }
    public int SortOrder { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}
