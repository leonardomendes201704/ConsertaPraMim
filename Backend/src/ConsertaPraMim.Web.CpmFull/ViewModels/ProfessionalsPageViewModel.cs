using AppMobileCPM.Models;

namespace AppMobileCPM.ViewModels;

public sealed class ProfessionalsPageViewModel
{
    public required string SearchTerm { get; init; }
    public required IReadOnlyList<Professional> Professionals { get; init; }
}
