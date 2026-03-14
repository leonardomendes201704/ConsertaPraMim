using AppMobileCPM.Models;

namespace AppMobileCPM.ViewModels;

public sealed class SupportPageViewModel
{
    public required SupportRequestInputModel Form { get; init; }
    public required IReadOnlyList<string> CategoryOptions { get; init; }
    public required IReadOnlyList<FaqItem> FaqItems { get; init; }
    public bool IsSubmitted { get; init; }
    public string SubmittedName { get; init; } = string.Empty;
}
