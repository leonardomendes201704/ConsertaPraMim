using AppMobileCPM.Models;

namespace AppMobileCPM.ViewModels;

public sealed class HomePageViewModel
{
    public required IReadOnlyList<ServiceCategory> Categories { get; init; }
}
